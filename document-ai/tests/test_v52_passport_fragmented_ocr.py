"""Synthetic V5.2 OCR regressions: never use actual identity documents."""
from app.schemas import Page
from app.structured_analysis import passport_category, structured_fields


def page(text):
    return Page(page=1, text=text, width=900, height=600, extraction="ocr", blocks=[])


def test_fragmented_india_header_still_recovers_labelled_identity():
    scan = page(
        "CART TORTS / REPUBLIC OF | INDIA\n"
        "Passport No\nA1234567\n"
        "Surname\nEXAMPLE\n"
        "Given Name(s)\nTEST\n"
        "Place of Birth\nTEST CITY\n"
        "Date of Birth\n01/02/1990\n"
        "Date of Expiry\n01/02/2030"
    )
    fields = structured_fields([scan])
    values = {field["field"]: field["value"] for field in fields}
    assert values["passport_number"] == "A1234567"
    assert values["surname"] == "EXAMPLE"
    assert values["place_of_birth"] == "TEST CITY"
    assert passport_category([scan], fields)
    assert all(f["requiresConfirmation"] and f["value"] in f["sourceText"] for f in fields)


def test_fragmented_header_without_document_evidence_remains_unknown():
    scan = page("REPUBLIC OF | INDIA\nCertificate of residence\nIssued to TEST PERSON")
    assert not passport_category([scan], structured_fields([scan]))


def test_employment_pass_with_passport_reference_not_passport():
    scan = page(
        "EMPLOYMENT PASS\nREPUBLIC OF | INDIA\nPassport No\nA1234567\n"
        "Surname\nEXAMPLE\nGiven Names\nTEST\nDate of Birth\n01/02/1990"
    )
    assert not passport_category([scan], structured_fields([scan]))


def test_no_unlabelled_passport_number_inference():
    scan = page("REPUBLIC OF | INDIA\nSurname\nEXAMPLE\nGiven Names\nTEST\nA1234567")
    assert not any(f["field"] == "passport_number" for f in structured_fields([scan]))
