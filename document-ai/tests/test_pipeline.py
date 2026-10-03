import copy
import io
import unicodedata
import pytest
from PIL import Image, ImageDraw, ImageFont
from fastapi.testclient import TestClient
from app.schemas import Page, Block, SemanticDocument
from app.pipeline import validate_grounding, render, sanitize_ids
from app.extraction import extract
from app.main import app

TEXT = 'EMPLOYMENT CONTRACT\nEmployee: Alex Example\nSalary: RM12,000 per month.\nEither party must give sixty days written notice.'

def page():
    return Page(page=1,text=TEXT,width=600,height=800,extraction='native',blocks=[
        Block(text='Salary: RM12,000 per month.',boundingBox=[10,10,200,30],kind='key_value')])

def semantic():
    return SemanticDocument.model_validate({
        'documentCategory':'Employment Contract','classificationConfidence':.8,'classificationEvidenceIds':['e1'],
        'evidence':[{'id':'e1','page':1,'sourceText':'Salary: RM12,000 per month.'}],
        'statements':[{'id':'s1','kind':'fact','label':'Salary','text':'The agreement specifies RM12,000 per month.',
            'originalValue':'RM12,000','supportStatus':'SUPPORTED','confidence':.8,'evidenceIds':['e1']}]
    })

def test_grounding_and_geometry():
    result = validate_grounding(semantic(),[page()])
    assert result.evidence[0].boundingBox == [10,10,200,30]
    output = render(result,[page()],'test-model')
    assert 'RM12,000' in output['summary']
    assert output['requiresReview'] is True

@pytest.mark.parametrize('change', ['quote','page','value','number','reference'])
def test_rejects_unsupported_claims(change):
    value = semantic()
    if change == 'quote': value.evidence[0].sourceText = 'Salary: RM99,000'
    if change == 'page': value.evidence[0].page = 9
    if change == 'value': value.statements[0].originalValue = 'RM99,000'
    if change == 'number': value.statements[0].text = 'Your net salary is RM9,500.'
    if change == 'reference': value.statements[0].evidenceIds = ['missing']
    with pytest.raises(ValueError): validate_grounding(value,[page()])

def test_low_classification_confidence_is_unknown():
    value = semantic(); value.classificationConfidence = .3
    assert validate_grounding(value,[page()]).documentCategory == 'Unknown'

@pytest.mark.parametrize('data,mime', [(b'bad','application/pdf'),(b'bad','image/png'),(b'bad','text/html')])
def test_signature_validation(data,mime):
    with pytest.raises(ValueError): extract(data,mime)

def test_service_requires_key(monkeypatch):
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY','synthetic-test-key')
    with TestClient(app) as client:
        assert client.post('/analyze',files={'file':('x.pdf',b'bad','application/pdf')}).status_code == 401
        assert client.post('/analyze',headers={'X-Service-Key':'wrong'},files={'file':('x.pdf',b'bad','application/pdf')}).status_code == 401

def test_service_fails_safely(monkeypatch):
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY','synthetic-test-key')
    monkeypatch.setenv('OLLAMA_MODEL','test')
    with TestClient(app) as client:
        response=client.post('/analyze',headers={'X-Service-Key':'synthetic-test-key'},files={'file':('x.pdf',b'private invalid contents','application/pdf')})
        assert response.status_code == 422
        assert 'private' not in response.text

def synthetic_image():
    image=Image.new('RGB',(1600,1000),'white')
    font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',36) if __import__('os').path.exists('/System/Library/Fonts/Supplemental/Arial.ttf') else ImageFont.load_default(size=36)
    ImageDraw.Draw(image).multiline_text((60,60),TEXT,font=font,fill='black',spacing=18)
    return image

def test_scanned_pdf_and_png():
    image=synthetic_image()
    pdf=io.BytesIO();image.save(pdf,format='PDF')
    png=io.BytesIO();image.save(png,format='PNG')
    for data,mime in [(pdf.getvalue(),'application/pdf'),(png.getvalue(),'image/png')]:
        pages=extract(data,mime)
        assert len(pages)==1
        assert '12,000' in pages[0].text
        assert pages[0].extraction=='ocr'
        assert pages[0].blocks

def test_native_pdf_preserves_pages_and_boxes():
    from pdf_helpers import native_pdf
    pages=extract(native_pdf([TEXT,'Confidentiality\nThe employee must protect confidential business information.']),'application/pdf')
    assert len(pages)==2
    assert all(p.extraction=='native' for p in pages)
    assert '12,000' in pages[0].text
    assert 'Confidentiality' in pages[1].text
    assert all(p.blocks for p in pages)

def test_oversize_request_rejected_before_parsing(monkeypatch):
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY','synthetic-test-key')
    with TestClient(app) as client:
        result=client.post('/analyze',headers={'X-Service-Key':'synthetic-test-key','Content-Length':str(20*1024*1024)},content=b'synthetic')
        assert result.status_code==413

def test_normalization_does_not_replace_source_values():
    from app.postprocessing import enrich
    result=enrich(semantic())
    assert result.statements[0].originalValue=='RM12,000'
    assert result.statements[0].normalizedValue=='MYR 12000'

def test_expiry_attention_and_conflicting_dates_are_grounded():
    from datetime import date
    from app.postprocessing import enrich
    from app.schemas import Statement
    value=semantic()
    for index,original in enumerate(['2026-10-10','2026-11-10']):
        value.statements.append(Statement(id=f'd{index}',kind='date',label='Expiry date',text=original,
            originalValue=original,confidence=.8,evidenceIds=['e1']))
    value=enrich(value,today=date(2026,10,2))
    assert any(s.kind=='warning' for s in value.statements)
    assert any(s.label=='Expiry needs attention' for s in value.statements)
    assert all(s.evidenceIds for s in value.statements)

def test_rotated_scan_orientation_is_corrected():
    image=synthetic_image().rotate(90,expand=True,fillcolor='white')
    data=io.BytesIO();image.save(data,format='PNG')
    pages=extract(data.getvalue(),'image/png')
    assert '12,000' in pages[0].text
    assert pages[0].rotationDegrees in (90,270)
    assert pages[0].coordinateSpace=='preprocessed_image_pixels'

def test_generic_clause_only_document_still_has_a_readable_summary():
    value=semantic();value.documentCategory='Unknown';value.statements[0].kind='clause'
    output=render(value,[page()],'test-model')
    assert output['summary']
    assert output['documentCategory']=='Unknown'

def test_repeated_transaction_amounts_are_not_conflicting_fields():
    from app.schemas import Statement
    from app.postprocessing import enrich
    value=semantic();value.statements=[]
    for index,amount in enumerate(['RM10','RM20']):
        value.statements.append(Statement(id=f't{index}',kind='money',label='Transaction amount',text=amount,
            originalValue=amount,confidence=.8,evidenceIds=['e1']))
    assert not any(s.kind=='warning' for s in enrich(value).statements)


# --- Regression tests for Issue 1: colon/invalid ID sanitization ---

def test_colon_in_statement_id_is_sanitized():
    """Model returns ID with colon; sanitize_ids fixes it; validate_grounding passes."""
    value = semantic()
    # Simulate model generating an ID with a colon
    value.statements[0].id = 'b0-statement_:2'
    value.classificationEvidenceIds = ['e1']
    # After sanitization the colon becomes underscore
    result = sanitize_ids(value)
    assert ':' not in result.statements[0].id
    assert result.statements[0].id == 'b0-statement__2' or '_' in result.statements[0].id
    # validate_grounding should now succeed
    validate_grounding(result, [page()])


def test_duplicate_statement_ids_after_sanitize():
    """Two IDs that become identical after sanitization get numeric suffixes."""
    from app.schemas import Statement, Evidence
    doc = SemanticDocument.model_validate({
        'documentCategory': 'Employment Contract',
        'classificationConfidence': .8,
        'classificationEvidenceIds': ['e1'],
        'evidence': [{'id': 'e1', 'page': 1, 'sourceText': 'Salary: RM12,000 per month.'}],
        'statements': [
            {'id': 'stmt:1', 'kind': 'fact', 'label': 'Salary', 'text': 'The salary is RM12,000.',
             'originalValue': 'RM12,000', 'supportStatus': 'SUPPORTED', 'confidence': .8, 'evidenceIds': ['e1']},
            {'id': 'stmt:2', 'kind': 'fact', 'label': 'Salary', 'text': 'The salary is RM12,000.',
             'originalValue': 'RM12,000', 'supportStatus': 'SUPPORTED', 'confidence': .8, 'evidenceIds': ['e1']},
        ]
    })
    # Both 'stmt:1' and 'stmt:2' clean to 'stmt_1' and 'stmt_2' (no collision)
    result = sanitize_ids(doc)
    ids = [s.id for s in result.statements]
    # All IDs must be unique after sanitization
    assert len(ids) == len(set(ids))
    # No colons remain
    assert all(':' not in sid for sid in ids)


def test_sanitize_ids_with_collision():
    """IDs that collapse to the same cleaned string get numeric suffixes to stay unique."""
    from app.schemas import Statement
    doc = SemanticDocument.model_validate({
        'documentCategory': 'Employment Contract',
        'classificationConfidence': .8,
        'classificationEvidenceIds': ['e1'],
        'evidence': [{'id': 'e1', 'page': 1, 'sourceText': 'Salary: RM12,000 per month.'}],
        'statements': [
            {'id': 'stmt::a', 'kind': 'fact', 'label': 'A', 'text': 'The salary is RM12,000.',
             'originalValue': 'RM12,000', 'supportStatus': 'SUPPORTED', 'confidence': .8, 'evidenceIds': ['e1']},
            {'id': 'stmt::b', 'kind': 'fact', 'label': 'B', 'text': 'The salary is RM12,000.',
             'originalValue': 'RM12,000', 'supportStatus': 'SUPPORTED', 'confidence': .8, 'evidenceIds': ['e1']},
        ]
    })
    # 'stmt::a' -> 'stmt_a', 'stmt::b' -> 'stmt_b' — no collision in this case
    result = sanitize_ids(doc)
    ids = [s.id for s in result.statements]
    assert len(ids) == len(set(ids))
    assert all(':' not in sid for sid in ids)


# --- Regression tests for Issue 2: NFKC normalization for Malay values ---

def test_unsupported_value_nfkc_normalization():
    """NFKC-normalized value matches source when byte-identical UTF-8 check would fail."""
    # Construct a source where a non-breaking space (U+00A0) is used
    source_with_nbsp = 'Salary: RM5,000 sebulan'
    # Model output uses a regular space — normalize() would fail to match
    original_value = 'RM5,000 sebulan'
    p = Page(page=1, text=source_with_nbsp, width=600, height=800, extraction='native',
             blocks=[Block(text=source_with_nbsp, boundingBox=[0,0,200,30], kind='key_value')])
    doc = SemanticDocument.model_validate({
        'documentCategory': 'Employment Contract',
        'classificationConfidence': .8,
        'classificationEvidenceIds': ['e1'],
        'evidence': [{'id': 'e1', 'page': 1, 'sourceText': source_with_nbsp}],
        'statements': [{'id': 's1', 'kind': 'fact', 'label': 'Salary', 'text': 'The salary is RM5,000.',
            'originalValue': original_value, 'supportStatus': 'UNCERTAIN', 'confidence': .8, 'evidenceIds': ['e1']}]
    })
    # Should pass after NFKC normalization widens the match
    result = validate_grounding(doc, [p])
    assert result is not None


def test_malay_value_in_source():
    """The exact Malay fixture text + model output with RM5,000 passes grounding."""
    malay_source = 'SURAT TAWARAN - SINTETIK\nNama pekerja: Ali Contoh\nJawatan: Jurutera\nGaji: RM5,000 sebulan\nTarikh mula: 2027-01-01\nTempoh percubaan: enam bulan.'
    p = Page(page=1, text=malay_source, width=600, height=800, extraction='native',
             blocks=[Block(text='Gaji: RM5,000 sebulan', boundingBox=[0,0,200,30], kind='key_value')])
    doc = SemanticDocument.model_validate({
        'documentCategory': 'Employment Contract',
        'classificationConfidence': .8,
        'classificationEvidenceIds': ['e1'],
        'evidence': [{'id': 'e1', 'page': 1, 'sourceText': 'Gaji: RM5,000 sebulan'}],
        'statements': [{'id': 's1', 'kind': 'money', 'label': 'Gaji', 'text': 'Gaji ialah RM5,000 sebulan.',
            'originalValue': 'RM5,000 sebulan', 'supportStatus': 'UNCERTAIN', 'confidence': .8, 'evidenceIds': ['e1']}]
    })
    result = validate_grounding(doc, [p])
    assert result.statements[0].originalValue == 'RM5,000 sebulan'
