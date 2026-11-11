"""Evidence-backed structured field suggestions for the analysis response.

Never turn a suggestion into a verified fact, reminder or persisted identity.
All values must be literal substrings of their source line.
"""
import re
from .schemas import Page

DATE = r"(?:\\d{4}[-/]\\d{1,2}[-/]\\d{1,2}|\\d{1,2}[-/.]\\d{1,2}[-/.]\\d{2,4}|\\d{1,2}\\s+[A-Za-z]{3,12}\\s+\\d{4})"
AMOUNT = r"(?:(?:RM|MYR|USD|SGD|GBP|EUR|INR|AED|\\$|£|€)\\s*)?(?:\\d{1,3}(?:,\\d{3})+|\\d+)(?:\\.\\d{1,2})?"
PATTERNS = (
    ("account_number", r"(?:account\\s*(?:no\\.?|number|#)|no\\.?\\s*akaun|nombor\\s+akaun)", r"[A-Z0-9][A-Z0-9/-]{3,34}"),
    ("invoice_number", r"(?:invoice\\s*(?:no\\.?|number|#)|no\\.?\\s*invois)", r"[A-Z0-9][A-Z0-9/-]{3,34}"),
    ("amount_due", r"(?:amount\\s+due|total\\s+(?:due|payable)|balance\\s+due|jumlah\\s+(?:perlu\\s+dibayar|bayaran|tertunggak)|amaun\\s+perlu\\s+dibayar)", AMOUNT),
    ("due_date", r"(?:payment\\s+due\\s+date|due\\s+date|tarikh\\s+(?:akhir\\s+bayaran|akhir\\s+pembayaran|bayaran\\s+akhir))", DATE),
    ("billing_period", r"(?:billing\\s+period|tempoh\\s+bil)", r"[^\\n]{3,55}"),
    ("passport_number", r"(?:passport\\s*(?:no\\.?|number|#)|no\\.?\\s*pasport|nombor\\s+pasport)", r"[A-Z0-9]{6,12}"),
    ("expiry_date", r"(?:date\\s+of\\s+expiry|expiry\\s+date|date\\s+of\\s+expiration|tarikh\\s+luput)", DATE),
    ("date_of_birth", r"(?:date\\s+of\\s+birth|tarikh\\s+lahir)", DATE),
    ("issue_date", r"(?:date\\s+of\\s+issue|issue\\s+date|tarikh\\s+dikeluarkan)", DATE),
)
RULES = tuple((field, re.compile(r"(?<!\\w)(?:"+label+r")\\s*[:#-]?\\s*("+value+r")", re.I)) for field,label,value in PATTERNS)

def structured_fields(pages: list[Page]) -> list[dict]:
    results = []
    seen = set()
    for page in pages:
        for raw in page.text.splitlines():
            line = raw.strip()
            if not line or len(line) > 300:
                continue
            for field, pattern in RULES:
                match = pattern.search(line)
                if not match:
                    continue
                value = match.group(1).strip().rstrip(" .,:;-")
                if not value or (field == "amount_due" and not re.search(r"\\d", value)):
                    continue
                key = (field, value, page.page)
                if key in seen:
                    continue
                seen.add(key)
                results.append({"field":field, "value":value, "page":page.page,
                                "sourceText":line, "requiresConfirmation":True})
    return results

def billing_category(pages: list[Page], fields: list[dict]) -> bool:
    """Suggest Utility Bill only with explicit billing context AND payment/account evidence."""
    text = " ".join(page.text for page in pages).lower()
    has_bill_title = bool(re.search(r"\\b(?:invoice|billing statement|utility bill|broadband bill|internet bill|bil internet|bil bulanan|bil utiliti)\\b", text))
    has_payment_field = any(f["field"] in ("amount_due","due_date") for f in fields)
    return has_bill_title and has_payment_field
