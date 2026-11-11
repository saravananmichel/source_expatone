from app.field_candidates import field_candidates
from app.schemas import Page


def page(text, number=1):
    return Page(page=number, text=text, width=100, height=100,
                extraction="native", blocks=[])


def test_bill_candidates_preserve_exact_source_and_leading_zeroes():
    pages = [page("Electricity Bill\nAccount Number: 00012345\nDue Date: 15/12/2026\nAmount Due: RM 1,250.60")]
    found = field_candidates(pages)
    values = {item["field"]: item["value"] for item in found}
    assert values == {
        "account_number": "00012345",
        "due_date": "15/12/2026",
        "amount_due": "RM 1,250.60",
    }
    assert all(item["page"] == 1 and item["requiresConfirmation"] for item in found)
    assert all(item["value"] in item["sourceText"] for item in found)


def test_challan_reference_and_multiple_pages():
    found = field_candidates([page("Traffic Notice", 1),
                              page("Challan No: AB-00123\nPayment Due Date: 2026-12-31", 2)])
    assert [(item["field"], item["page"]) for item in found] == [
        ("reference_number", 2), ("due_date", 2)
    ]


def test_no_inference_from_unlabelled_or_empty_text():
    assert field_candidates([page("RM 125.60\n2026-12-15\nPay promptly")]) == []
    assert field_candidates([page("")]) == []


def test_expiry_does_not_generate_reminder():
    found = field_candidates([page("Expiry Date: 01 Jan 2027")])
    assert found[0]["value"] == "01 Jan 2027"
    assert found[0]["requiresConfirmation"] is True
