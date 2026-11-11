"""Conservative, source-grounded field candidates; no persistence or reminders.

Only values explicitly labelled on a source line are proposed. Dates are left
verbatim because day/month ordering is locale dependent. Callers must request
user confirmation before creating reminders or treating a candidate as verified.
"""
import re
from .schemas import Page

DATE = r"(?:\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/.]\d{1,2}[-/.]\d{2,4}|\d{1,2}\s+[A-Za-z]{3,12}\s+\d{4})"
MONEY = r"(?:(?:RM|MYR|USD|SGD|GBP|EUR|INR|AED|\$|£|€)\s*)?\d{1,3}(?:,\d{3})*(?:\.\d{2})|(?:(?:RM|MYR|USD|SGD|GBP|EUR|INR|AED)\s+)\d+(?:\.\d{2})?"
RULES = (
    ("due_date", re.compile(r"\b(?:payment\s+)?due\s+date\s*[:\-]?\s*("+DATE+r")\b", re.I)),
    ("expiry_date", re.compile(r"\b(?:expiry|expiration|expires?\s+on|valid\s+until)\s*(?:date)?\s*[:\-]?\s*("+DATE+r")\b", re.I)),
    ("amount_due", re.compile(r"\b(?:total\s+)?(?:amount\s+due|outstanding\s+amount|balance\s+due|total\s+payable)\s*[:\-]?\s*("+MONEY+r")\b", re.I)),
    ("account_number", re.compile(r"\b(?:account\s*(?:number|no\.?|#))\s*[:\-]?\s*([A-Z0-9][A-Z0-9 /-]{3,35})", re.I)),
    ("reference_number", re.compile(r"\b(?:reference\s*(?:number|no\.?|#)|challan\s*(?:number|no\.?|#))\s*[:\-]?\s*([A-Z0-9][A-Z0-9 /-]{3,35})", re.I)),
)


def field_candidates(pages: list[Page]) -> list[dict]:
    """Return candidates with page and exact source line, never inferred values."""
    found = []
    for page in pages:
        for line in page.text.splitlines():
            source = line.strip()
            if not source or len(source) > 300:
                continue
            for kind, pattern in RULES:
                match = pattern.search(source)
                if not match:
                    continue
                value = match.group(1).strip().rstrip(" .,:;-")
                if not value:
                    continue
                found.append({
                    "field": kind,
                    "value": value,
                    "page": page.page,
                    "sourceText": source,
                    "requiresConfirmation": True,
                })
    return found
