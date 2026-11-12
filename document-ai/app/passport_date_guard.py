"""V5.4 conservative passport date and category display guard.

Never relabel DOB as expiry, split ambiguous source values, or guess identity.
"""
import re

DATE = re.compile(r"(?<!\d)(?:\d{1,2}[-/.]\d{1,2}[-/.]\d{4}|\d{4}[-/]\d{1,2}[-/]\d{1,2})(?!\d)")
PASSPORT = re.compile(r"\bpassport\b|\brepublic\s+of\s+india\b", re.I)
PASS = re.compile(r"\b(?:e\s*-?\s*pass|employment\s+pass|electronic\s+pass)\b", re.I)
FINANCIAL = re.compile(r"\b(?:invoice|bank\s+statement|account\s+statement|salary\s+slip|tax\s+invoice|balance\s+due|amount\s+due)\b", re.I)
DATE_LABEL = re.compile(r"\b(?:date\s+of\s+(?:birth|issue|expiry)|(?:expiry|birth|issue)\s+date)\b", re.I)


def _bad_expiry(value):
    if not isinstance(value, str):
        return False
    dates = DATE.findall(value)
    # A single date with an adjacent sex marker or other OCR column
    # is not a clean, label-grounded expiry date.
    return len(dates) != 1 or DATE.sub("", value).strip(" \t.,:;-/") != ""


def guard_passport_response(result, pages):
    diagnostics = result.setdefault("qualityDiagnostics", {})
    diagnostics["dateGuardRemovedCount"] = 0
    diagnostics["categoryGuard"] = "unchanged"
    if not pages:
        return result
    text = pages[0].text
    passport_context = bool(PASSPORT.search(text[:1200]) and not PASS.search(text))
    if not passport_context:
        return result
    # A passport is not a financial document merely because OCR or the model
    # mistakes the numeric dates for financial facts. Preserve uncertainty.
    if result.get("documentCategory") == "Financial Document" and not FINANCIAL.search(text):
        result["documentCategory"] = "Unknown"
        result["title"] = "Unknown Analysis"
        result["summary"] = ("Document type could not be verified. Passport-related "
                             "text was detected, but the document requires review.")
        diagnostics["categoryGuard"] = "unsupported_financial_classification"
    removed = 0
    def safe_entry(entry):
        nonlocal removed
        label = str(entry.get("label", ""))
        value = entry.get("value", entry.get("date", ""))
        if "expiry" in label.lower() and _bad_expiry(value):
            removed += 1
            return False
        return True
    for key in ("keyInformation", "importantDates"):
        if isinstance(result.get(key), list):
            result[key] = [entry for entry in result[key] if safe_entry(entry)]
    # Statements also drive the detailed UI cards. Suppress malformed expiry
    # statements, but leave the original semantic document for traceability.
    if isinstance(result.get("statements"), list):
        retained = []
        for statement in result["statements"]:
            label = str(statement.get("label", ""))
            value = statement.get("originalValue", "")
            if "expiry" in label.lower() and _bad_expiry(value):
                removed += 1
                continue
            retained.append(statement)
        result["statements"] = retained
        valid_ids = {statement.get("id") for statement in retained}
        for section in result.get("analysisSections", []):
            if isinstance(section.get("statementIds"), list):
                section["statementIds"] = [id for id in section["statementIds"] if id in valid_ids]
    # The original summary can repeat contaminated expiry dates; do not
    # retain a misleading sentence once invalid claims were filtered.
    if removed:
        result["summary"] = ("Passport-related text was detected. Some date fields "
                             "could not be reliably associated with their labels. "
                             "Please verify the original document.")
    diagnostics["dateGuardRemovedCount"] = removed
    return result
