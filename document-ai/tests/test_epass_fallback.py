"""Regression checks for conservative ePASS source fallback."""
from app.private_fallback import source_fallback
from app.schemas import Page


def page(text):
    return Page(page=1, text=text, width=595, height=842,
                extraction='native', blocks=[])


def test_epass_explicit_same_line_fields():
    text = (
        "Electronic Pass ePASS Government of Malaysia\n"
        "EMPLOYMENT PASS Category (I)\n"
        "Name: SAMPLE PERSON\nPassport No: A1234567\n"
        "Nationality: INDIA\nWith: EXAMPLE COMPANY SDN BHD"
    )
    result = source_fallback([page(text)])
    fields = {item.label: item.originalValue for item in result.statements}
    assert result.documentCategory == 'Employment Pass'
    assert fields['Holder name'] == 'SAMPLE PERSON'
    assert fields['Passport number'] == 'A1234567'
    assert fields['Employer'] == 'EXAMPLE COMPANY SDN BHD'


def test_unrelated_document_not_classified_as_epass():
    result = source_fallback([page('Invoice\nName: SAMPLE PERSON\nPassport No: A1234567')])
    assert result.documentCategory == 'Unknown'


def test_missing_fields_are_not_invented():
    text = 'ePASS Government of Malaysia\nEMPLOYMENT PASS\nName: SAMPLE PERSON'
    result = source_fallback([page(text)])
    labels = {item.label for item in result.statements}
    assert 'Holder name' in labels
    assert 'Passport number' not in labels


def test_separate_label_and_value_lines_not_guessed():
    text = 'ePASS Government of Malaysia\nEMPLOYMENT PASS\nName\nPassport No\nSAMPLE PERSON\nA1234567'
    result = source_fallback([page(text)])
    assert result.documentCategory == 'Unknown'
