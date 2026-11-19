from app.document_router import classify_document
from app.immigration_fields import immigration_fields
from app.schemas import Page


def extract(text):
    pages = [Page(page=1, text=text, width=600, height=800,
                  extraction="native", blocks=[])]
    return immigration_fields(pages, classify_document(pages))


def test_application_never_maps_pass_expiry():
    text = ("Application Type: DEPENDANT PASS\nApplication Category: NEW/BARU\n"
            "Applicant Name: SAMPLE PERSON\nPassport No.: A1234567\n"
            "Letter Valid Until: 01/01/2030\nExpiry Date: 02/02/2030")
    fields = extract(text)
    mapped = {f["field"]: f["value"] for f in fields}
    assert mapped["person_name"] == "SAMPLE PERSON"
    assert mapped["letter_validity"] == "01/01/2030"
    assert "expiry_date" not in mapped
    assert all(f["requiresConfirmation"] and f["sourceText"] in text for f in fields)


def test_issued_pass_fields_and_conflict():
    text = ("Employment Pass\nMultiple Entry Visa\nValid Until: 01/01/2030\n"
            "Employer: EXAMPLE COMPANY\nPassport No.: A1234567\nPassport No.: B1234567")
    mapped = {f["field"]: f["value"] for f in extract(text)}
    assert mapped["expiry_date"] == "01/01/2030"
    assert mapped["employer"] == "EXAMPLE COMPANY"
    assert "passport_number" not in mapped


def test_unrouted_document_ignored():
    assert extract("Passport No.: A1234567") == []
