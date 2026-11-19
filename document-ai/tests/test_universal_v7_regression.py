from types import SimpleNamespace
from app.semantic_candidates import appraisal_salary_rows
from app.review_summary import review_summary, display_review_value
from app.review_reconciliation import employment_review_ready, append_review_statements

def page(text, confidence=.97):
    return SimpleNamespace(page=1, text=text, extractionConfidence=confidence)

def candidate(field, value, quote):
    return dict(field=field, value=value, sourceText=quote, page=1,
                requiresConfirmation=True)

HEADER = "SalaryBreakup:\nMonthlySalary\nAnnual Salary\nIn Rupees\nin Rupees\n"

def test_salary_pairs_and_totals():
    p = page(HEADER + "Basic\n100\n1200\nTotal\n100\n1200\nNon-Cash Component:\nGratuity\n20\n240\nTotal\n20\n240")
    fields = appraisal_salary_rows([p])
    values = {f["field"]: f["value"] for f in fields}
    assert values["basic_salary_monthly"] == "100"
    assert values["basic_salary_annual"] == "1200"
    assert values["cash_total_annual"] == "1200"
    assert values["non_cash_total_annual"] == "240"
    assert all(f["sourceText"] in p.text and f["requiresConfirmation"] for f in fields)
    summary = review_summary(SimpleNamespace(subtype="performance_appraisal"), fields, [p])
    assert "Basic salary (review): monthly 100, annual 1200." in summary

def test_salary_missing_or_reversed_header():
    assert not appraisal_salary_rows([page(HEADER + "Basic\n100\nHRA\n20\n240")])
    assert not appraisal_salary_rows([page(HEADER.replace("MonthlySalary\nAnnual Salary", "Annual Salary\nMonthlySalary") + "Basic\n100\n1200")])
    assert not appraisal_salary_rows([page(HEADER + "Basic\n100\n1200", .5)])

def test_offer_readiness_conflict():
    p = page("Effective 1 Jan 2030. Salary RM 12000. Salary RM 13000.")
    route = SimpleNamespace(subtype="employment_offer")
    fields = [candidate("effective_date", "1 Jan 2030", "Effective 1 Jan 2030"),
              candidate("annual_compensation", "RM 12000", "Salary RM 12000")]
    assert employment_review_ready(route, [p], fields)
    assert not employment_review_ready(route, [p], fields + [candidate("annual_compensation", "RM 13000", "Salary RM 13000")])

def test_source_linkage_is_not_verification():
    p = page("Salary RM 12000")
    result = {"structuredFields": [candidate("annual_compensation", "RM 12000", p.text)],
              "qualityDiagnostics": {"evidenceCoverage": 0.0}}
    append_review_statements(result, [p])
    assert result["statements"][0]["supportStatus"] == "UNCERTAIN"
    assert result["qualityDiagnostics"]["sourceLinkedCoverage"] == 1.0
    assert result["qualityDiagnostics"]["evidenceCoverage"] == 0.0

def test_display_only_spacing():
    assert display_review_value("employment_restriction", "ANYFORMOFEMPLOYMENTISSTRICTLYPROHIBITED.") == "Any form of employment is strictly prohibited"
    assert display_review_value("passport_number", "X1234567") == "X1234567"
