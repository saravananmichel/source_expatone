from types import SimpleNamespace

from app.semantic_candidates import appraisal_salary_rows, semantic_candidates
from app.review_reconciliation import employment_review_ready, append_review_statements
from app.review_summary import display_review_value
from app.immigration_fields import immigration_fields


def page(text):
    return SimpleNamespace(page=1, text=text, extractionConfidence=.97)


def candidate(field, value, quote):
    return dict(field=field, value=value, sourceText=quote, page=1,
                requiresConfirmation=True)


def test_offer_monthly_pay_and_joining_date():
    p = page("Joining date: 1 Jan 2030. Monthly salary: RM 5000.")
    route = SimpleNamespace(subtype="employment_offer")
    fields = [candidate("joining_date", "1 Jan 2030", "Joining date: 1 Jan 2030"),
              candidate("monthly_compensation", "RM 5000", "Monthly salary: RM 5000")]
    assert employment_review_ready(route, [p], fields)


def test_duplicate_salary_rows_abstain():
    p = page("SalaryBreakup:\nMonthlySalary\nAnnual Salary\nIn Rupees\nin Rupees\n"
             "Basic\n100\n1200\nBasic\n200\n2400")
    assert not appraisal_salary_rows([p])


def test_display_preserves_identity():
    assert display_review_value("designation", "SOFTWAREENGINEER") == "Software Engineer"
    assert display_review_value("person_name", "EXAMPLENAME") == "EXAMPLENAME"
    assert display_review_value("passport_number", "X1234567") == "X1234567"


def test_visa_qualifier_and_separate_stay_date():
    for subtype in ("employment_pass_with_visa", "dependant_pass_with_visa"):
        p = page("Condition\nUNTIL 1 JAN 2030\nMULTIPLE ENTRY VISA\n"
                 "Good for any number of journeys to Malaysia until 31 DEC 2029\n"
                 "provided this passport remains valid.")
        fields = immigration_fields([p], SimpleNamespace(subtype=subtype))
        by_name = {f["field"]: f for f in fields}
        assert by_name["expiry_date"]["value"] == "1 JAN 2030"
        travel = by_name["visa_travel_condition"]
        assert "provided this passport remains valid" in travel["value"]
        assert travel["sourceText"] in p.text
        assert travel["requiresConfirmation"]


def test_invalid_visa_calendar_date():
    p = page("MULTIPLE ENTRY VISA\n"
             "Good for any number of journeys to Malaysia until 31 FEB 2030\n"
             "provided this passport remains valid.")
    fields = immigration_fields([p], SimpleNamespace(subtype="employment_pass_with_visa"))
    assert not any(f["field"] == "visa_travel_condition" for f in fields)


def test_nested_review_graph_sync():
    p = page("Monthly salary: RM 5000")
    result = {"structuredFields": [candidate("monthly_compensation", "RM 5000", p.text)],
              "semanticDocument": {"documentCategory": "Employment"},
              "qualityDiagnostics": {"evidenceCoverage": 0.0}}
    append_review_statements(result, [p])
    nested = result["semanticDocument"]
    assert nested["statements"] == result["statements"]
    assert nested["evidence"] == result["evidence"]
    assert nested["documentCategory"] == "Employment"
    assert nested["statements"][0]["supportStatus"] == "UNCERTAIN"
    assert result["qualityDiagnostics"]["evidenceCoverage"] == 0.0


def test_passport_guard_rejects_birth_as_expiry(monkeypatch):
    from app.passport_date_guard import guard_passport_response
    monkeypatch.setenv("DOCUMENT_AI_UNIVERSAL_V2", "1")
    p = page("PASSPORT\nDate of Birth: 01/01/1990\nDate of Issue: 01/01/2020")
    result = {"documentCategory": "Passport", "expiryDate": "01/01/1990",
              "structuredFields": [
                  candidate("date_of_birth", "01/01/1990", p.text),
                  candidate("issue_date", "01/01/2020", p.text),
                  candidate("expiry_date", "01/01/1990", p.text)],
              "statements": [{"id": "date", "label": "Expiry date",
                              "originalValue": "01/01/1990"}]}
    guard_passport_response(result, [p])
    assert result["expiryDate"] is None
    assert not any(f["field"] == "expiry_date" for f in result["structuredFields"])
    assert not result["statements"]


def test_passport_guard_preserves_ordered_dates(monkeypatch):
    from app.passport_date_guard import guard_passport_response
    monkeypatch.setenv("DOCUMENT_AI_UNIVERSAL_V2", "1")
    p = page("PASSPORT\n01/01/1990\n01/01/2020\n01/01/2030")
    fields = [candidate("date_of_birth", "01/01/1990", p.text),
              candidate("issue_date", "01/01/2020", p.text),
              candidate("expiry_date", "01/01/2030", p.text)]
    result = {"documentCategory": "Passport", "expiryDate": "01/01/2030",
              "structuredFields": fields}
    guard_passport_response(result, [p])
    assert result["structuredFields"] == fields
    assert result["expiryDate"] == "01/01/2030"


def test_passport_grouped_route_rejects_empty_input():
    from app.passport_fast_path import grouped_passport_review_fields
    assert grouped_passport_review_fields([], []) == []
