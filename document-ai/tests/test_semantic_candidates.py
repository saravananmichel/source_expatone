from app.semantic_candidates import semantic_candidates
from app.schemas import Page

def page(text):
    return Page(page=1, text=text, width=600, height=800, extraction="native", blocks=[])

def test_multiline_employment_candidates():
    p = page("Your employment commences\non 10 June 2030.\nNotice period of 90 days.\nAnnual compensation of INR 42,00,000.")
    result = semantic_candidates([p])
    assert {f["field"]: f["value"] for f in result} == {
        "start_date": "10 June 2030", "notice_period": "90 days",
        "annual_compensation": "INR 42,00,000"}
    assert all(f["sourceText"] in p.text and f["requiresConfirmation"] for f in result)

def test_conflicting_and_invalid_dates_abstain():
    assert not semantic_candidates([page("Employment begins on 31 February 2030.")])
    assert not semantic_candidates([page("Employment begins on 10 June 2030. Employment begins on 11 June 2030.")])
