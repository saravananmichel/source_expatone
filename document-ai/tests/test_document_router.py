from app.document_router import classify_document
from app.schemas import Page


def page(text, confidence=1):
    return Page(page=1, text=text, width=600, height=800, extraction="native",
                extractionConfidence=confidence, blocks=[])


def test_application_not_issued_pass():
    result = classify_document([page("Application Type: DEPENDANT PASS\nApplication Category: NEW/BARU")])
    assert result.category == "Immigration Document"
    assert result.subtype == "dependant_pass_application"
    assert len(result.evidence) == 2


def test_employment_pass_with_visa():
    result = classify_document([page("Employment Pass\nMultiple Entry Visa\nValid Until: 01/01/2030")])
    assert result.category == "Employment Pass"
    assert result.subtype == "employment_pass_with_visa"


def test_abstains_on_ambiguous_or_low_quality_source():
    assert classify_document([page("Employment Pass")]) is None
    assert classify_document([page("Application Type: DEPENDANT PASS")]) is None
    assert classify_document([page("Employment Pass\nMultiple Entry Visa\nValid Until: 01/01/2030", .5)]) is None
    assert classify_document([page("Approval Letter\nEmployment Pass\nMultiple Entry Visa\nExpiry Date: 01/01/2030")]) is None
    assert classify_document([page("Passport\nSurname: SAMPLE")]) is None
    assert classify_document([]) is None
