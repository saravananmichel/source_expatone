from app.document_router import classify_document
from app.immigration_fields import immigration_fields
from app.immigration_review import immigration_review
from app.schemas import Page


def page(text):
    return Page(page=1, text=text, width=600, height=800, extraction="ocr",
                extractionConfidence=.95, blocks=[])


def test_malaysian_heading_and_journey_validity_not_pass_expiry():
    source = ("Electronic Pass\nMULTIPLE ENTRY VISA\n"
              "Good for any number of journeys to Malaysia until 30 Mar 2030\n"
              "EMPLOYMENT PASS [Reg.9] Category (I)\nName: SAMPLE PERSON\n"
              "Passport No.: A1234567\nEmployer: SAMPLE COMPANY")
    pages = [page(source)]
    route = classify_document(pages)
    assert route.category == "Employment Pass"
    assert not any(f["field"] == "expiry_date" for f in immigration_fields(pages, route))
    assert immigration_review(pages) is None


def test_application_roles_and_duration():
    pages = [page("Application Type: DEPENDANT PASS\nApplication Category: NEW/BARU\n"
                  "Name of Applicant / Dependant: SAMPLE APPLICANT\n"
                  "Name of Foreign Knowledge Worker: SAMPLE SPONSOR\n"
                  "Duration: 24 MONTHS\nOur reference: SAMPLE-001\nPassport No.：A1234567")]
    semantic, fields = immigration_review(pages)
    values = {f["field"]: f["value"] for f in fields}
    assert values["person_name"] == "SAMPLE APPLICANT"
    assert values["sponsor"] == "SAMPLE SPONSOR"
    assert values["application_duration"] == "24 MONTHS"
    assert "expiry_date" not in values
    assert semantic.documentCategory == "Immigration Document"
