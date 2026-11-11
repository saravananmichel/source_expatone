"""Synthetic regression tests: no personal documents or external services."""
from app.schemas import Page
from app.structured_analysis import structured_fields, billing_category

def page(text):
    return Page(page=1, text=text, width=100, height=100, extraction="native", blocks=[])

def test_bilingual_bill_fields_are_grounded():
    pages = [page("Bil Internet\nNo. Akaun: 000123456\nJumlah Perlu Dibayar: RM 1,250.60\nTarikh Akhir Bayaran: 25/11/2026")]
    fields = structured_fields(pages)
    values = {f["field"]: f["value"] for f in fields}
    assert values["account_number"] == "000123456"
    assert values["amount_due"] == "RM 1,250.60"
    assert values["due_date"] == "25/11/2026"
    assert billing_category(pages, fields)
    assert all(f["requiresConfirmation"] and f["value"] in f["sourceText"] for f in fields)

def test_passport_labels_not_unrelated_numbers():
    pages = [page("PASSPORT\nPassport No: A12345678\nDate of Birth: 02/03/1990\nDate of Expiry: 01/12/2030")]
    values = {f["field"]: f["value"] for f in structured_fields(pages)}
    assert values == {"passport_number":"A12345678","date_of_birth":"02/03/1990","expiry_date":"01/12/2030"}

def test_no_invention_without_labels():
    pages = [page("Apple Billing Support - https://apple.com/bill\nService Tax ID: W10-1808-32000489")]
    assert structured_fields(pages) == []
    assert not billing_category(pages, [])

def test_bill_title_alone_does_not_override_category():
    pages = [page("Invoice\nSupport: help.example.test")]
    assert not billing_category(pages, structured_fields(pages))


def test_passport_adjacent_label_and_value():
    fields = structured_fields([page("PASSPORT\\nPassport No\\nK1234567\\nDate of Birth\\n09/11/1983")])
    values = {f["field"]: f["value"] for f in fields}
    assert values["passport_number"] == "K1234567"
    assert values["date_of_birth"] == "09/11/1983"
    assert all(f["value"] in f["sourceText"] and f["requiresConfirmation"] for f in fields)


def test_combined_issue_and_expiry_are_separate():
    fields = structured_fields([page("Date of Issue / Date of Expiry 23/12/2021 22/12/2031")])
    values = {f["field"]: f["value"] for f in fields}
    assert values["issue_date"] == "23/12/2021"
    assert values["expiry_date"] == "22/12/2031"


def test_unlabelled_or_distant_numbers_are_not_passport_numbers():
    fields = structured_fields([page("PASSPORT\\nSome unrelated heading\\n33726657\\nOld Passport\\n33726657")])
    assert not any(f["field"] == "passport_number" for f in fields)
