"""V5.4 conservative passport date and category display guard.

Never relabel DOB as expiry, split ambiguous source values, or guess identity.
"""
import re
import os
from datetime import datetime

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
    # In experimental DEV review, do not expose a bare date as expiry
    # when the document type itself could not be established.
    if (os.getenv("DOCUMENT_AI_UNIVERSAL_V2", "0") == "1"
            and result.get("documentCategory") == "Unknown"
            and result.get("suggestedDocumentCategory") in (None, "Passport")
            and not PASS.search(text)):
        fields = result.get("structuredFields", [])
        retained = [f for f in fields if f.get("field") != "expiry_date"]
        if len(retained) != len(fields):
            result["structuredFields"] = retained
            result["expiryDate"] = None
            diagnostics["structuredFieldCount"] = len(retained)
            diagnostics["dateGuardRemovedCount"] = len(fields) - len(retained)
            diagnostics["categoryGuard"] = "unclassified_expiry_suppressed"
            result["summary"] = ("Document type and expiry date could not be reliably "
                                 "established. Verify the original document.")
            return result
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
    # Experimental review: reject conflicting or chronologically impossible
    # identity dates, regardless of whether their OCR tokens are well formed.
    blocked = set()
    if os.getenv("DOCUMENT_AI_UNIVERSAL_V2", "0") == "1":
        keys = ("date_of_birth", "issue_date", "expiry_date")
        groups = {key: set() for key in keys}
        def parse(value):
            for fmt in ("%d/%m/%Y", "%d-%m-%Y", "%d.%m.%Y", "%Y-%m-%d", "%Y/%m/%d"):
                try:
                    return datetime.strptime(str(value), fmt).date()
                except ValueError:
                    pass
            return None
        fields = result.get("structuredFields", [])
        for field in fields:
            key = field.get("field")
            if key in groups:
                groups[key].add(parse(field.get("value")))
        for key, values in groups.items():
            if None in values or len(values) > 1:
                blocked.add(key)
        for i, earlier in enumerate(keys):
            for later in keys[i+1:]:
                if len(groups[earlier]) == len(groups[later]) == 1:
                    a, b = next(iter(groups[earlier])), next(iter(groups[later]))
                    if a is not None and b is not None and a >= b:
                        blocked.update((earlier, later))
        retained = [f for f in fields if f.get("field") not in blocked]
        removed += len(fields) - len(retained)
        result["structuredFields"] = retained
        diagnostics["structuredFieldCount"] = len(retained)
        if "expiry_date" in blocked:
            result["expiryDate"] = None
    def blocked_label(label):
        label = label.lower().replace("_", " ")
        return any(key in blocked and re.search(pattern, label) for key, pattern in (
            ("date_of_birth", r"birth|\\bdob\\b"),
            ("issue_date", r"issue"),
            ("expiry_date", r"expir|valid until")))
    def safe_entry(entry):
        nonlocal removed
        label = str(entry.get("label", ""))
        value = entry.get("value", entry.get("date", ""))
        if blocked_label(label) or ("expiry" in label.lower() and _bad_expiry(value)):
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
            if blocked_label(label) or ("expiry" in label.lower() and _bad_expiry(value)):
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
