"""V5.4 regression tests with synthetic passport text only."""
from types import SimpleNamespace
from app.passport_date_guard import guard_passport_response


def test_rejects_merged_dates_and_sex_suffix_without_guessing():
    result = {
        "documentCategory": "Financial Document",
        "title": "Financial Document Analysis",
        "summary": "This Financial Document Date of Expiry: 04/10/2024 03/10/2034.",
        "keyInformation": [
            {"label": "Date of Expiry", "value": "04/10/2024 03/10/2034"},
            {"label": "Date of Expiry", "value": "30/05/1986 F"},
            {"label": "Date of Expiry", "value": "03/10/2034"},
        ],
        "importantDates": [],
        "statements": [
            {"id": "1", "label": "Date of Expiry", "originalValue": "04/10/2024 03/10/2034"},
            {"id": "2", "label": "Date of Expiry", "originalValue": "03/10/2034"},
        ],
        "analysisSections": [{"statementIds": ["1", "2"]}],
    }
    out = guard_passport_response(result, [
        SimpleNamespace(text="REPUBLIC OF INDIA PASSPORT\nSurname\nEXAMPLE\nDate of Expiry")
    ])
    assert out["documentCategory"] == "Unknown"
    assert [x["value"] for x in out["keyInformation"]] == ["03/10/2034"]
    assert [x["id"] for x in out["statements"]] == ["2"]
    assert out["analysisSections"][0]["statementIds"] == ["2"]
    assert out["qualityDiagnostics"]["dateGuardRemovedCount"] == 3
    assert "04/10/2024 03/10/2034" not in out["summary"]


def test_real_financial_document_is_not_reclassified():
    out = guard_passport_response({
        "documentCategory": "Financial Document", "title": "Financial Document Analysis",
        "summary": "Invoice", "keyInformation": [], "importantDates": [],
    }, [SimpleNamespace(text="INVOICE\nAmount Due 200\nPassport number provided")])
    assert out["documentCategory"] == "Financial Document"


def test_passport_with_explicit_employment_pass_is_not_modified():
    out = guard_passport_response({
        "documentCategory": "Financial Document", "title": "Financial Document Analysis",
        "summary": "Original", "keyInformation": [],
    }, [SimpleNamespace(text="PASSPORT number\nEMPLOYMENT PASS\nDate of Expiry")])
    assert out["documentCategory"] == "Financial Document"
