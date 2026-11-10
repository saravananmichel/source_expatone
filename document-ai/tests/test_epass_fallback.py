"""Source-only ePASS fallback must never invent fields or values."""
from app.private_fallback import _epass_fields
from app.schemas import Page


def page(text):
    return Page(page=1, text=text, width=595, height=842,
                extraction='native', blocks=[])


def test_epass_extracts_explicit_fields():
    source = """Electronic Pass
ePASS Government of Malaysia
EMPLOYMENT PASS Category (I)
Name : SAMPLE PERSON
Gender : MALE
Passport No : A1234567
Nationality : INDIA
With : EXAMPLE COMPANY SDN BHD
Date Of Issue : 07 APR 2025
Good for journeys to Malaysia until 30 Mar 2027
"""
    found = {label: value for label, value, _, _ in _epass_fields([page(source)])}
    assert found['Holder name'] == 'SAMPLE PERSON'
    assert found['Passport number'] == 'A1234567'
    assert found['Employer'] == 'EXAMPLE COMPANY SDN BHD'
    assert found['Issue date'] == '07 APR 2025'
    assert found['Valid until'] == '30 Mar 2027'


def test_no_classification_from_unrelated_document():
    assert _epass_fields([page('Invoice Name: SAMPLE PERSON Passport No: A1234567')]) == []


def test_missing_field_is_not_invented():
    source = 'ePASS Government of Malaysia\nEMPLOYMENT PASS\nName : SAMPLE PERSON'
    labels = {label for label, _, _, _ in _epass_fields([page(source)])}
    assert 'Holder name' in labels
    assert 'Passport number' not in labels
    assert 'Employer' not in labels
