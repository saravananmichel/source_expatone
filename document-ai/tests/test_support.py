import pytest
import asyncio
import httpx
from app.support import review_support,document_graph
from app.pipeline import render
from test_pipeline import semantic,page

def test_unrelated_true_quote_cannot_support_salary_after_tax(monkeypatch):
    s=semantic();s.statements[0].text='RM12,000 is salary after tax.'
    async def post(*args,**kwargs):
        return httpx.Response(200,request=httpx.Request('POST','http://test/analyze'),json={'message':{'content':'{"verdicts":[{"id":"s1","supportStatus":"UNSUPPORTED","reason":"Evidence states gross amount without tax basis."}]}'}})
    monkeypatch.setattr(httpx.AsyncClient,'post',post);monkeypatch.setenv('OLLAMA_MODEL','test')
    asyncio.run(review_support(s))
    assert s.statements[0].supportStatus=='UNSUPPORTED'
    assert 'after tax' not in render(s,[page()],'test')['summary']
    assert render(s,[page()],'test')['qualityDiagnostics']['unsupportedClaims']==1

def test_missing_or_duplicate_verdicts_fail_closed(monkeypatch):
    async def post(*args,**kwargs):
        return httpx.Response(200,request=httpx.Request('POST','http://test/analyze'),json={'message':{'content':'{"verdicts":[]}'}})
    monkeypatch.setattr(httpx.AsyncClient,'post',post);monkeypatch.setenv('OLLAMA_MODEL','test')
    s=semantic();asyncio.run(review_support(s))
    assert s.statements[0].supportStatus=='UNCERTAIN'
    assert not render(s,[page()],'test')['requiredActions']

def test_graph_preserves_cross_page_evidence():
    s=semantic();s.evidence.append(s.evidence[0].model_copy(update={'id':'e2','page':2}))
    s.statements[0].evidenceIds=['e1','e2'];s.statements[0].kind='relationship'
    graph=document_graph(s)
    assert graph['nodes'][0]['pages']==[1,2]
    assert graph['evidencePages']=={'e1':1,'e2':2}

def test_partial_interpretation_is_excluded_from_narrative_and_actions():
    s=semantic();s.statements[0].kind='action';s.statements[0].supportStatus='PARTIALLY_SUPPORTED'
    assert render(s,[page()],'test')['requiredActions']==[]

def test_conflicts_with_parenthetical_labels_cite_both_pages():
    from app.schemas import Statement,Evidence
    from app.postprocessing import enrich
    s=semantic();s.statements=[];s.evidence=[]
    for i,value in enumerate(['2027-02-01','2027-03-01']):
        s.evidence.append(Evidence(id=f'e{i}',page=i+1,sourceText=f'Start date: {value}'))
        s.statements.append(Statement(id=f's{i}',kind='date',label=f'Start date ({i})',text=value,
            originalValue=value,confidence=.9,evidenceIds=[f'e{i}']))
    warning=next(x for x in enrich(s).statements if x.kind=='warning')
    assert warning.evidenceIds==['e0','e1']
    assert '2027-02-01' in warning.text and '2027-03-01' in warning.text

def test_cross_page_conditional_notice_does_not_calculate_probation_end():
    from app.schemas import Evidence
    from app.postprocessing import enrich
    s=semantic();s.evidence.extend([
      Evidence(id='p',page=2,sourceText='The employee serves a six-month probation period.'),
      Evidence(id='d',page=2,sourceText='During probation, either party may terminate with fourteen days written notice.'),
      Evidence(id='a',page=3,sourceText='After written confirmation, either party may terminate with sixty days written notice.')])
    relationship=next(x for x in enrich(s).statements if x.kind=='relationship')
    assert relationship.evidenceIds==['p','d','a']
    assert 'fourteen days' in relationship.text and 'sixty days' in relationship.text
    assert '2027-07-01' not in relationship.text

def test_corrupt_encrypted_and_excessive_pdf_are_rejected():
    import io
    from app.extraction import extract
    from pypdf import PdfWriter
    from pdf_helpers import native_pdf
    writer=PdfWriter();writer.add_blank_page(width=600,height=800);writer.encrypt('synthetic-only');data=io.BytesIO();writer.write(data)
    for payload in [b'%PDF-1.7\nbroken',data.getvalue(),native_pdf(['Synthetic page']*41)]:
        with pytest.raises(Exception):extract(payload,'application/pdf')

def test_source_script_identity_is_not_romanized_and_fields_are_typed():
    from app.schemas import Statement
    from app.postprocessing import enrich
    s=semantic();s.statements=[
      Statement(id='name',kind='entity',label='Employee name',text='The employee is Invented Alias (林示例).',originalValue='林示例',confidence=.9,evidenceIds=['e1']),
      Statement(id='pay',kind='entity',label='Salary',text='RM5,000',originalValue='RM5,000',confidence=.9,evidenceIds=['e1']),
      Statement(id='start',kind='entity',label='Start date',text='2027-01-01',originalValue='2027-01-01',confidence=.9,evidenceIds=['e1'])]
    result=enrich(s)
    assert result.statements[0].text=='Employee name: 林示例.'
    assert result.statements[1].kind=='money'
    assert result.statements[2].kind=='date'

def test_probation_relationship_includes_start_date_from_page_one():
    from app.schemas import Evidence
    from app.postprocessing import enrich
    s=semantic();s.evidence.extend([
      Evidence(id='start',page=1,sourceText='Start date: 2027-01-01'),
      Evidence(id='probation',page=2,sourceText='The employee serves a six-month probation period from the start date.'),
      Evidence(id='during',page=2,sourceText='During probation, either party may terminate with fourteen days written notice.'),
      Evidence(id='after',page=3,sourceText='After written confirmation, either party may terminate with sixty days written notice.')])
    relationship=next(x for x in enrich(s).statements if x.kind=='relationship')
    assert 'start' in relationship.evidenceIds
    assert all(v in relationship.text for v in ['2027-01-01','six-month','fourteen days','sixty days'])

def test_reference_to_missing_schedule_does_not_count_as_extracted_compensation():
    from app.pipeline import missing_information
    s=semantic();s.statements[0].kind='clause';s.statements[0].label='Compensation schedule';s.statements[0].originalValue='Compensation will be confirmed in a separate schedule.'
    hints=missing_information(s,[page()])
    assert any('Compensation amount: not found in extracted fields' in h for h in hints)


# --- Support reviewer test suite ---

def _make_mock_post(verdict_id, status, reason='Test reason.'):
    """Return a monkeypatch-compatible async post that returns a single verdict."""
    async def post(*args, **kwargs):
        payload = {'verdicts': [{'id': verdict_id, 'supportStatus': status, 'reason': reason}]}
        return httpx.Response(200, request=httpx.Request('POST', 'http://test/api/chat'),
                              json={'message': {'content': __import__('json').dumps(payload)}})
    return post


def _doc_with_text(claim_text, original_value, evidence_quote, kind='fact'):
    from app.schemas import SemanticDocument
    return SemanticDocument.model_validate({
        'documentCategory': 'Employment Contract',
        'classificationConfidence': .8,
        'classificationEvidenceIds': ['e1'],
        'evidence': [{'id': 'e1', 'page': 1, 'sourceText': evidence_quote}],
        'statements': [{'id': 's1', 'kind': kind, 'label': 'Test', 'text': claim_text,
            'originalValue': original_value, 'supportStatus': 'UNCERTAIN', 'confidence': .8, 'evidenceIds': ['e1']}]
    })


def test_support_exact_fact(monkeypatch):
    """Extracting an exact field value is SUPPORTED."""
    s = _doc_with_text('The salary is RM12,000.', 'RM12,000', 'Salary: RM12,000 per month.')
    monkeypatch.setattr(httpx.AsyncClient, 'post', _make_mock_post('s1', 'SUPPORTED', 'Exact field extraction.'))
    monkeypatch.setenv('OLLAMA_MODEL', 'test')
    asyncio.run(review_support(s))
    assert s.statements[0].supportStatus == 'SUPPORTED'


def test_support_paraphrased_fact(monkeypatch):
    """A paraphrase that preserves exact meaning is SUPPORTED."""
    s = _doc_with_text('The employee earns twelve thousand ringgit monthly.', 'RM12,000',
                       'Salary: RM12,000 per month.')
    monkeypatch.setattr(httpx.AsyncClient, 'post', _make_mock_post('s1', 'SUPPORTED', 'Meaning preserved.'))
    monkeypatch.setenv('OLLAMA_MODEL', 'test')
    asyncio.run(review_support(s))
    assert s.statements[0].supportStatus == 'SUPPORTED'


def test_support_unsupported_addition(monkeypatch):
    """Adding information not in evidence is UNSUPPORTED."""
    s = _doc_with_text('The salary is RM12,000 plus a performance bonus.', 'RM12,000 plus a performance bonus',
                       'Salary: RM12,000 per month.')
    monkeypatch.setattr(httpx.AsyncClient, 'post', _make_mock_post('s1', 'UNSUPPORTED', 'Bonus not in evidence.'))
    monkeypatch.setenv('OLLAMA_MODEL', 'test')
    asyncio.run(review_support(s))
    assert s.statements[0].supportStatus == 'UNSUPPORTED'


def test_support_contradiction(monkeypatch):
    """A contradiction must stay UNSUPPORTED, not softened to UNCERTAIN."""
    s = _doc_with_text('The salary is RM15,000 per month.', 'RM15,000',
                       'Salary: RM12,000 per month.')
    monkeypatch.setattr(httpx.AsyncClient, 'post', _make_mock_post('s1', 'UNSUPPORTED', 'Amount contradicts evidence.'))
    monkeypatch.setenv('OLLAMA_MODEL', 'test')
    asyncio.run(review_support(s))
    assert s.statements[0].supportStatus == 'UNSUPPORTED'


def test_support_negation(monkeypatch):
    """Inverting a negation is UNSUPPORTED."""
    s = _doc_with_text('Overtime is permitted.', 'Overtime is permitted',
                       'Overtime is not permitted without written approval.')
    monkeypatch.setattr(httpx.AsyncClient, 'post', _make_mock_post('s1', 'UNSUPPORTED', 'Negation inverted.'))
    monkeypatch.setenv('OLLAMA_MODEL', 'test')
    asyncio.run(review_support(s))
    assert s.statements[0].supportStatus == 'UNSUPPORTED'


def test_support_conditional_statement(monkeypatch):
    """Dropping a condition from a conditional statement is UNSUPPORTED."""
    s = _doc_with_text('Either party may terminate with 14 days notice.', '14 days',
                       'During probation, either party may terminate with 14 days written notice.')
    monkeypatch.setattr(httpx.AsyncClient, 'post', _make_mock_post('s1', 'UNSUPPORTED', 'Condition omitted.'))
    monkeypatch.setenv('OLLAMA_MODEL', 'test')
    asyncio.run(review_support(s))
    assert s.statements[0].supportStatus == 'UNSUPPORTED'


def test_support_date(monkeypatch):
    """Exact date extraction is SUPPORTED."""
    s = _doc_with_text('Employment starts on 2027-01-01.', '2027-01-01',
                       'Start date: 2027-01-01', kind='date')
    monkeypatch.setattr(httpx.AsyncClient, 'post', _make_mock_post('s1', 'SUPPORTED', 'Exact date match.'))
    monkeypatch.setenv('OLLAMA_MODEL', 'test')
    asyncio.run(review_support(s))
    assert s.statements[0].supportStatus == 'SUPPORTED'


def test_support_amount(monkeypatch):
    """Exact monetary amount extraction is SUPPORTED."""
    s = _doc_with_text('Monthly salary is RM12,000.', 'RM12,000',
                       'Salary: RM12,000 per month.', kind='money')
    monkeypatch.setattr(httpx.AsyncClient, 'post', _make_mock_post('s1', 'SUPPORTED', 'Exact amount.'))
    monkeypatch.setenv('OLLAMA_MODEL', 'test')
    asyncio.run(review_support(s))
    assert s.statements[0].supportStatus == 'SUPPORTED'


def test_support_identity_name(monkeypatch):
    """Extracting an employee name is SUPPORTED."""
    s = _doc_with_text('The employee is Alex Example.', 'Alex Example',
                       'Employee: Alex Example', kind='entity')
    monkeypatch.setattr(httpx.AsyncClient, 'post', _make_mock_post('s1', 'SUPPORTED', 'Name extracted exactly.'))
    monkeypatch.setenv('OLLAMA_MODEL', 'test')
    asyncio.run(review_support(s))
    assert s.statements[0].supportStatus == 'SUPPORTED'


def test_support_cross_page_relationship(monkeypatch):
    """A cross-page relationship verdict is accepted and applied correctly."""
    from app.schemas import SemanticDocument
    doc = SemanticDocument.model_validate({
        'documentCategory': 'Employment Contract',
        'classificationConfidence': .8,
        'classificationEvidenceIds': ['e1'],
        'evidence': [
            {'id': 'e1', 'page': 1, 'sourceText': 'Employee: Alex Example'},
            {'id': 'e2', 'page': 2, 'sourceText': 'Salary: RM12,000 per month.'},
        ],
        'statements': [{'id': 's1', 'kind': 'relationship', 'label': 'Employment',
            'text': 'Alex Example earns RM12,000 per month.',
            'originalValue': 'Alex Example', 'supportStatus': 'UNCERTAIN', 'confidence': .8,
            'evidenceIds': ['e1', 'e2']}]
    })
    monkeypatch.setattr(httpx.AsyncClient, 'post', _make_mock_post('s1', 'SUPPORTED', 'Cross-page facts corroborated.'))
    monkeypatch.setenv('OLLAMA_MODEL', 'test')
    asyncio.run(review_support(doc))
    assert doc.statements[0].supportStatus == 'SUPPORTED'
