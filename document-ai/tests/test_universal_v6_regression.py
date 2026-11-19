from types import SimpleNamespace
from app.general_classification import general_classification
from app.semantic_candidates import semantic_candidates
from app.immigration_fields import immigration_fields
from app.review_summary import display_review_value, review_summary
from app.review_reconciliation import reconcile_review_fields, append_review_statements

def page(text, confidence=.97):
    return SimpleNamespace(page=1, text=text, extractionConfidence=confidence)

def test_appraisal_without_heading():
    p = page("Your Annual Performance appraisal process is completed.\nSalary hasbeen revised and is given below.\nYour annual CTC asof June 1st2020 is Rs.5,00,000.00/-")
    route = general_classification([p])
    assert route.subtype == "performance_appraisal"
    fields = semantic_candidates([p])
    values = {f["field"]: f["value"] for f in fields}
    assert values["effective_date"] == "June 1st2020"
    assert values["revised_annual_ctc"] == "Rs.5,00,000.00"
    assert all(f["sourceText"] in p.text for f in fields)
    assert "Revised annual cost to company" in review_summary(route, fields, [p])

def test_single_appraisal_cue_is_insufficient():
    assert general_classification([page("Your Annual Performance appraisal process is completed.")]) is None

def test_invalid_month_first_date():
    assert not any(f["field"] == "effective_date" for f in semantic_candidates([page("Your annual CTC asof February 31st2020 is RM 1000")]))

def test_immigration_issue_place():
    p = page("Place Of IsSue:EXAMPLE IMMIGRATION OFFICE")
    for subtype in ("employment_pass_with_visa", "dependant_pass_with_visa"):
        fields = immigration_fields([p], SimpleNamespace(subtype=subtype))
        assert fields[0]["field"] == "place_of_issue"
        assert fields[0]["value"] == "EXAMPLE IMMIGRATION OFFICE"

def test_display_only_spacing():
    assert display_review_value("employment_restriction", "ANYFORMOFEMPLOYMENTISSTRICTLYPROHIBITED.") == "Any form of employment is strictly prohibited"
    assert display_review_value("person_name", "EXAMPLEPERSON") == "EXAMPLEPERSON"

def test_review_statement_source_and_idempotence():
    p = page("Designation: Example Engineer")
    f = {"field": "designation", "value": "Example Engineer", "page": 1,
         "sourceText": p.text, "requiresConfirmation": True}
    bad = {**f, "field": "employer", "value": "Invented"}
    result = {"structuredFields": [f, bad]}
    append_review_statements(result, [p])
    append_review_statements(result, [p])
    assert len(result["statements"]) == len(result["evidence"]) == 1
    assert result["statements"][0]["supportStatus"] == "UNCERTAIN"
    assert result["evidence"][0]["sourceText"] == p.text
    assert result["qualityDiagnostics"]["uncertainClaims"] == 1

def test_conflicts_not_synchronized():
    p = page("Role: Engineer\nRole: Manager")
    fields = [{"field": "designation", "value": v, "page": 1,
               "sourceText": "Role: " + v} for v in ("Engineer", "Manager")]
    result, conflicts = reconcile_review_fields([p], fields, [])
    assert result == []
    assert conflicts == ["designation"]
