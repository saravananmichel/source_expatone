from app.immigration_review import immigration_review
from app.schemas import Page


def page(text):
    return Page(page=1, text=text, width=600, height=800, extraction="native", blocks=[])


PASS = ("Employment Pass\nMultiple Entry Visa\nValid Until: 01/01/2030\n"
        "Name: SAMPLE PERSON\nPassport No.: A1234567\nEmployer: SAMPLE COMPANY")
APPLICATION = ("Application Type: DEPENDANT PASS\nApplication Category: NEW/BARU\n"
               "Applicant Name: SAMPLE PERSON\nPassport No.: A1234567\n"
               "Reference Number: SAMPLE-001\nLetter Valid Until: 01/01/2030")


def test_pass_review_only():
    semantic, fields = immigration_review([page(PASS)])
    assert semantic.documentCategory == "Employment Pass"
    assert all(s.supportStatus == "UNCERTAIN" for s in semantic.statements)
    assert all(f["requiresConfirmation"] for f in fields)
    assert all(e.sourceText in PASS for e in semantic.evidence)


def test_application_review_never_pass_expiry():
    semantic, fields = immigration_review([page(APPLICATION)])
    assert semantic.documentCategory == "Immigration Document"
    assert not any(f["field"] == "expiry_date" for f in fields)


def test_abstains_when_missing_conflicting_or_invalid():
    assert immigration_review([page(PASS.replace("Employer: SAMPLE COMPANY", ""))]) is None
    assert immigration_review([page(PASS + "\nPassport No.: B1234567")]) is None
    assert immigration_review([page(PASS.replace("01/01/2030", "31/02/2030"))]) is None
    assert immigration_review([page(PASS + "\nIssue Date: 02/01/2030")]) is None
