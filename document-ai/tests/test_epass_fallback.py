"""Source-only fallback remains country-agnostic and never invents fields."""
from app.private_fallback import source_fallback
from app.schemas import Page


def page(text):
    return Page(page=1, text=text, width=595, height=842,
                extraction='native', blocks=[])


def test_generic_labeled_fields_from_different_countries():
    for text in (
        'Republic of India\nName: SAMPLE PERSON\nDocument Number: A1234567',
        'Singapore\nName: SAMPLE PERSON\nPass Number: S1234567',
        'United Arab Emirates\nName: SAMPLE PERSON\nPermit Number: U1234567',
        'Vanuatu\nName: SAMPLE PERSON\nReference Number: V1234567',
        'China\nName: SAMPLE PERSON\nVisa Number: C1234567',
    ):
        result = source_fallback([page(text)])
        values = {s.label: s.originalValue for s in result.statements}
        assert values['Name'] == 'SAMPLE PERSON'
        assert any(v.endswith('1234567') for v in values.values())
        assert result.documentCategory == 'Unknown'


def test_unlabeled_values_not_assigned_to_fields():
    result = source_fallback([page('Name\nSAMPLE PERSON\nA1234567')])
    assert all(s.label != 'Name' for s in result.statements)


def test_missing_fields_not_invented():
    result = source_fallback([page('Name: SAMPLE PERSON')])
    assert all(s.label != 'Passport number' for s in result.statements)
