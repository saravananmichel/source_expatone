from types import SimpleNamespace
from app.review_summary import review_summary
from app.semantic_candidates import semantic_candidates

def test_review_summary_only_uses_grounded_values():
    route = SimpleNamespace(subtype="employment_offer")
    page = SimpleNamespace(page=1, text="Notice period is 90 days.", extractionConfidence=.97)
    fields = [
        {"field":"notice_period", "value":"90 days", "page":1, "sourceText":"Notice period is 90 days."},
        {"field":"annual_compensation", "value":"MYR 999", "page":1, "sourceText":"Annual salary is MYR 999"},
    ]
    summary = review_summary(route, fields, [page])
    assert "90 days" in summary
    assert "999" not in summary
    assert "AI interpretation was incomplete" in summary
    page.extractionConfidence = .5
    assert "90 days" not in review_summary(route, fields, [page])

def test_relieving_date_review_candidate():
    page = SimpleNamespace(page=1, text="Last working day was 1 December 2026.", extractionConfidence=.97)
    fields = semantic_candidates([page])
    assert any(f["field"] == "relieving_date" and f["requiresConfirmation"] for f in fields)
    page.text = "If approved, last working day was 1 December 2026."
    assert not semantic_candidates([page])
