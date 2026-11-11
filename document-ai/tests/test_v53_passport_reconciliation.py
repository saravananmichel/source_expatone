"""V5.3 synthetic classification consistency tests."""
from app.schemas import Page
from app.passport_reconciliation import reconcile_passport_result


def page(text):
    return Page(page=1, text=text, width=800, height=600, extraction="ocr", blocks=[])


def fields(*names):
    return [{"field": name, "page": 1, "sourceText": "SYNTHETIC",
             "value": "TEST", "requiresConfirmation": True} for name in names]


def result(category="Certificate"):
    return {"documentCategory": category, "title": category + " Analysis",
            "summary": "Original", "suggestedDocumentCategory": "Passport",
            "qualityDiagnostics": {}}


def test_same_evidence_reconciles_both_model_classifications():
    scan = page("PASSPORT\nPassport No\nA1234567\nSurname\nEXAMPLE\n"
                "Given Names\nTEST\nDate of Birth\n01/01/1990\n"
                "Date of Expiry\n01/01/2030")
    extracted = fields("passport_number", "surname", "given_names",
                       "date_of_birth", "expiry_date")
    outputs = [reconcile_passport_result(result(c), [scan], extracted, "test")
               for c in ("Unknown", "Certificate")]
    assert [x["documentCategory"] for x in outputs] == ["Passport", "Passport"]
    assert all(x["qualityDiagnostics"]["classificationReconciliation"] ==
               "strong_evidence" for x in outputs)


def test_two_fields_do_not_force_passport():
    scan = page("PASSPORT\nGiven Names\nTEST\nPlace of Birth\nTEST CITY")
    output = reconcile_passport_result(
        result(), [scan], fields("given_names", "place_of_birth"), "test")
    assert output["documentCategory"] == "Certificate"
    assert output["qualityDiagnostics"]["classificationReconciliation"] == "insufficient_evidence"


def test_employment_pass_and_certificate_titles_are_protected():
    extracted = fields("passport_number", "surname", "given_names",
                       "date_of_birth", "expiry_date")
    for title in ("EMPLOYMENT PASS", "BIRTH CERTIFICATE"):
        scan = page("PASSPORT\n" + title + "\nSurname\nEXAMPLE")
        output = reconcile_passport_result(result(), [scan], extracted, "test")
        assert output["documentCategory"] == "Certificate"


def test_fallback_diagnostics_contain_no_identity_values():
    output = reconcile_passport_result(
        result("Unknown"), [page("Given Names\nTEST")],
        fields("given_names"), "source-only-private-fallback")
    assert output["qualityDiagnostics"]["classificationSource"] == "source_fallback"
    assert "TEST" not in str(output["qualityDiagnostics"])
