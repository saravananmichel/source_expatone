from types import SimpleNamespace
from app.general_classification import general_classification
from app.semantic_candidates import semantic_candidates
from app.structured_analysis import passport_reverse_fields
from app.passport_date_guard import guard_passport_response
from app.review_summary import immigration_summary


def page(text, number=1, confidence=.97):
    return SimpleNamespace(text=text, page=number, extractionConfidence=confidence)


def test_untitled_offer_requires_two_cues():
    text = "We are pleased to offer you the position of Analyst."
    assert general_classification([page(text)]) is None
    route = general_classification([page(text + " Terms and conditions of your employment follow.")])
    assert route.subtype == "employment_offer"


def test_written_notice_and_conditions():
    p = page("Notice period is ninety days.\nYou shall work eight hours per day.\nYour appointment is subject to medical fitness.")
    values = {f["field"]: f["value"] for f in semantic_candidates([p])}
    assert values["notice_period"] == "ninety days"
    assert values["working_hours"] == "eight hours per day"
    assert "medical fitness" in values["medical_condition"]
    p.extractionConfidence = .5
    assert not semantic_candidates([p])


def test_reverse_page_separates_historical_fields():
    p = page("Name of mother: EXAMPLE PERSON\nAddress: 12 Example Road\nOld passport number: X1234567\nOld passport issue date: 01/02/2020", 2)
    fields = passport_reverse_fields([page("Identity page"), p])
    values = {f["field"]: f["value"] for f in fields}
    assert values["previous_passport_number"] == "X1234567"
    assert values["previous_passport_issue_date"] == "01/02/2020"
    assert values["residential_address"] == "12 Example Road"
    assert "passport_number" not in values
    assert all(f["sourceText"] in p.text and f["requiresConfirmation"] for f in fields)


def test_unclassified_expiry_suppressed(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_UNIVERSAL_V2", "1")
    result = {"documentCategory": "Unknown", "structuredFields": [
        {"field": "expiry_date", "value": "01/02/2020"}]}
    guard_passport_response(result, [page("Unreadable identity scan")])
    assert not result["structuredFields"]


def test_immigration_summary_omits_unsupported_value():
    p = page("Employer: EXAMPLE COMPANY")
    fields = [
        {"field": "employer", "value": "EXAMPLE COMPANY", "sourceText": p.text, "page": 1},
        {"field": "expiry_date", "value": "01/02/2030", "sourceText": "Until 01/02/2030", "page": 1},
    ]
    summary = immigration_summary("Employment Pass", fields, [p])
    assert "EXAMPLE COMPANY" in summary
    assert "2030" not in summary
    assert "does not establish legal status" in summary
