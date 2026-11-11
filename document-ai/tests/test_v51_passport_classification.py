"""Synthetic passport classification regression tests; no real identity data."""
from app.schemas import Page
from app.structured_analysis import passport_category, structured_fields


def page(text):
    return Page(page=1, text=text, width=900, height=600, extraction="ocr", blocks=[])


def test_passport_without_ocr_header_but_with_mrz_and_labels():
    sample = page("REPUBLIC OF SAMPLELAND\nSurname\nEXAMPLE\nGiven Names\nTEST\nDate of Birth\n01/02/1990\nPlace of Birth\nTEST CITY\nP<SAMEXAMPLE<<TEST<<<<<<<<<<<<<<<<<<<<")
    assert passport_category([sample], structured_fields([sample]))


def test_passport_with_multiple_labels_and_date_without_header():
    sample = page("Surname\nEXAMPLE\nGiven Names\nTEST\nDate of Birth\n01/02/1990\nPlace of Birth\nTEST CITY\nDate of Issue\n02/03/2020\nDate of Expiry\n01/02/2030")
    assert passport_category([sample], structured_fields([sample]))


def test_certificate_with_personal_details_not_passport():
    sample = page("BIRTH CERTIFICATE\nName: EXAMPLE\nDate of Birth: 01/02/1990\nPlace of Birth: TEST CITY")
    assert not passport_category([sample], structured_fields([sample]))


def test_employment_pass_not_misclassified():
    sample = page("EMPLOYMENT PASS\nPassport\nSurname\nEXAMPLE\nGiven Names\nTEST\nDate of Birth\n01/02/1990\nDate of Expiry\n01/02/2030")
    assert not passport_category([sample], structured_fields([sample]))
