"""Synthetic passport interpretation regression tests."""
from app.postprocessing import enrich
from app.schemas import SemanticDocument


def test_passport_mixed_dates_are_not_deadlines():
    values = [
        ('Date of Birth', '09/11/1983 M'),
        ('Date of Expiry', '23/12/2021 22/12/2031'),
        ('Place of Birth', 'SATTUR, TAMIL NADU'),
        ('Place of Issue', 'CHENNAI'),
        ('Date of Issue', '23/12/2021'),
    ]
    semantic = SemanticDocument.model_validate({
        'documentCategory': 'Passport',
        'classificationConfidence': .9,
        'classificationEvidenceIds': ['e1'],
        'evidence': [{'id': 'e1', 'page': 1, 'sourceText': 'Synthetic passport'}],
        'statements': [
            {'id': f's{i}', 'kind': 'date', 'label': label,
             'text': f'{label}: {value}', 'originalValue': value,
             'confidence': .7, 'evidenceIds': ['e1']}
            for i, (label, value) in enumerate(values)
        ]
    })
    result = enrich(semantic)
    kinds = {s.label: s.kind for s in result.statements}
    assert kinds['Date of Birth'] == 'fact'
    assert kinds['Date of Expiry'] == 'fact'
    assert kinds['Place of Birth'] == 'fact'
    assert kinds['Place of Issue'] == 'fact'
    assert kinds['Date of Issue'] == 'date'


def test_invalid_calendar_date_is_not_a_deadline():
    from app.postprocessing import _valid_single_date
    assert not _valid_single_date('31/02/2027')
    assert not _valid_single_date('2026-13-01')
    assert _valid_single_date('22/12/2031')
    assert _valid_single_date('30 MAR 2027')


def test_passport_number_alias_is_not_reported_missing():
    from app.pipeline import missing_information
    from app.schemas import Statement
    document = SemanticDocument.model_validate({
        'documentCategory': 'Passport',
        'classificationConfidence': .9,
        'classificationEvidenceIds': ['e1'],
        'evidence': [{'id': 'e1', 'page': 1, 'sourceText': 'Passport No: X1234567'}],
        'statements': [
            {'id': 's1', 'kind': 'fact', 'label': 'Passport No',
             'text': 'Passport No: X1234567.', 'originalValue': 'X1234567',
             'confidence': .9, 'evidenceIds': ['e1']}
        ]
    })
    assert not any('Passport number' in warning for warning in missing_information(document, []))


def test_passport_summary_uses_short_verified_fields():
    from app.pipeline import render
    from app.schemas import Page
    doc = SemanticDocument.model_validate({
        'documentCategory': 'Passport',
        'classificationConfidence': .9,
        'classificationEvidenceIds': ['e1'],
        'evidence': [{'id': 'e1', 'page': 1, 'sourceText': 'Passport No: X1234567'}],
        'statements': [
            {'id': 's1', 'kind': 'fact', 'label': 'Passport No',
             'text': 'Passport No: X1234567.', 'originalValue': 'X1234567',
             'confidence': .9, 'evidenceIds': ['e1'], 'supportStatus': 'SUPPORTED'},
            {'id': 's2', 'kind': 'date', 'label': 'Date of Expiry',
             'text': 'Date of Expiry: 22/12/2031.', 'originalValue': '22/12/2031',
             'confidence': .9, 'evidenceIds': ['e1'], 'supportStatus': 'SUPPORTED'}
        ]
    })
    pages = [Page(page=1, text='Passport No: X1234567',
                  width=600, height=800, extraction='native', blocks=[])]
    output = render(doc, pages, 'test-model')
    assert 'Passport No: X1234567' in output['summary']
    assert output['summary'].count('22/12/2031') == 1
    assert not any('Passport number' in item for item in output['missingInformation'])
