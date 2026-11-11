"""Conservative reconciliation of conflicting passport classifications.

Never use a weak document-type suggestion as a verified classification.
No source text or personal values are added to diagnostics.
"""
import re


def reconcile_passport_result(result, pages, fields, model):
    diagnostics = result.setdefault("qualityDiagnostics", {})
    diagnostics["classificationSource"] = (
        "source_fallback" if model == "source-only-private-fallback" else "model"
    )
    diagnostics["classificationReconciliation"] = "unchanged"
    diagnostics["structuredFieldCount"] = len(fields)
    suggested = result.get("suggestedDocumentCategory")
    original = result.get("documentCategory", "Unknown")
    diagnostics["classificationConflict"] = bool(
        suggested and original not in (suggested, "Unknown")
    )
    diagnostics["passportSuggestionNeedsReview"] = bool(
        suggested == "Passport" and original != "Passport"
    )
    if not pages or suggested != "Passport":
        return result
    text = pages[0].text
    if re.search(r"\b(?:e\s*-?\s*pass|employment\s+pass|electronic\s+pass)\b", text, re.I):
        diagnostics["classificationReconciliation"] = "blocked_pass_document"
        return result
    if re.search(r"\b(?:birth|marriage|death|residence|education)\s+certificate\b", text, re.I):
        diagnostics["classificationReconciliation"] = "blocked_certificate_title"
        return result
    field_names = {f.get("field") for f in fields if f.get("page") == pages[0].page}
    dates = len(field_names & {"date_of_birth", "issue_date", "expiry_date"})
    identity = len(field_names & {"surname", "given_names", "nationality", "place_of_birth"})
    header = bool(re.search(r"\bpassport\b", text[:700], re.I))
    strong = (header and dates >= 2 and identity >= 2
              and "passport_number" in field_names)
    if not strong:
        diagnostics["classificationReconciliation"] = "insufficient_evidence"
        return result
    if result.get("documentCategory") not in ("Unknown", "Certificate"):
        return result
    result["documentCategory"] = "Passport"
    result["title"] = "Passport Analysis"
    result["summary"] = (
        "This document appears to be a passport based on multiple extracted "
        "identity fields. Verify all details against the original document."
    )
    diagnostics["classificationSource"] = "passport_evidence_reconciliation"
    diagnostics["classificationReconciliation"] = "strong_evidence"
    # Preserve the model's original semantic assessment separately for audit.
    # Reconciliation is explicitly recorded in diagnostics.
    return result
