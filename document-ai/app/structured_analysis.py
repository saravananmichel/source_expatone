"""Evidence-backed structured field suggestions for the analysis response.

Never turn a suggestion into a verified fact, reminder or persisted identity.
All values must be literal substrings of their source line.
"""
import re
from .schemas import Page

DATE = r"(?:\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/.]\d{1,2}[-/.]\d{2,4}|\d{1,2}\s+[A-Za-z]{3,12}\s+\d{4})"
AMOUNT = r"(?:(?:RM|MYR|USD|SGD|GBP|EUR|INR|AED|\$|£|€)\s*)?(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{1,2})?"
PATTERNS = (
    ("account_number", r"(?:account\s*(?:no\.?|number|#)|no\.?\s*akaun|nombor\s+akaun)", r"[A-Z0-9][A-Z0-9/-]{3,34}"),
    ("invoice_number", r"(?:invoice\s*(?:no\.?|number|#)|no\.?\s*invois)", r"[A-Z0-9][A-Z0-9/-]{3,34}"),
    ("amount_due", r"(?:amount\s+due|total\s+(?:due|payable)|balance\s+due|jumlah\s+(?:perlu\s+dibayar|bayaran|tertunggak)|amaun\s+perlu\s+dibayar)", AMOUNT),
    ("due_date", r"(?:payment\s+due\s+date|due\s+date|tarikh\s+(?:akhir\s+bayaran|akhir\s+pembayaran|bayaran\s+akhir))", DATE),
    ("billing_period", r"(?:billing\s+period|tempoh\s+bil)", r"[^\n]{3,55}"),
    ("passport_number", r"(?:passport\s*(?:no\.?|number|#)|no\.?\s*pasport|nombor\s+pasport)", r"[A-Z0-9]{6,12}"),
    ("expiry_date", r"(?:date\s+of\s+expiry|expiry\s+date|date\s+of\s+expiration|tarikh\s+luput)", DATE),
    ("date_of_birth", r"(?:date\s+of\s+birth|tarikh\s+lahir)", DATE),
    ("issue_date", r"(?:date\s+of\s+issue|issue\s+date|tarikh\s+dikeluarkan)", DATE),
)
RULES = tuple((field, re.compile(r"(?<!\w)(?:"+label+r")\s*[:#-]?\s*("+value+r")", re.I)) for field,label,value in PATTERNS)

# Passport OCR often puts the label and value on separate lines. Only accept
# short, immediate neighbours; never infer a number from an unlabelled line.
PASSPORT_LABEL = re.compile(r"(?<!\w)(?:passport\s*(?:no\.?|number|#)|no\.?\s*pasport|nombor\s+pasport)\b", re.I)
PASSPORT_VALUE = re.compile(r"^[A-Z][0-9]{6,8}$|^[0-9]{7,9}$", re.I)
DATE_LABELS = {
    "date_of_birth": re.compile(r"(?:date\s+of\s+birth|tarikh\s+lahir)", re.I),
    "issue_date": re.compile(r"(?:date\s+of\s+issue|tarikh\s+dikeluarkan)", re.I),
    "expiry_date": re.compile(r"(?:date\s+of\s+expiry|date\s+of\s+expiration|tarikh\s+luput)", re.I),
}
DATE_VALUE = re.compile(DATE, re.I)
HISTORICAL_PASSPORT = re.compile(r"\b(?:old|previous|prior|former|cancelled|canceled)\s+passport\b", re.I)


# Strict label-grounded identity extraction. These are suggestions, never verified data.
PASSPORT_CONTEXT = re.compile(r"\b(?:passport|republic\s+of\s+india)\b", re.I)
IDENTITY_LABELS = {
    "surname": re.compile(r"^(?:surname|family\s+name)\s*[:/-]?\s*$", re.I),
    "given_names": re.compile(r"^(?:given\s+names?|first\s+names?)\s*[:/-]?\s*$", re.I),
    "nationality": re.compile(r"^nationality\s*[:/-]?\s*$", re.I),
    "place_of_birth": re.compile(r"^place\s+of\s+birth\s*[:/-]?\s*$", re.I),
    "place_of_issue": re.compile(r"^place\s+of\s+issue\s*[:/-]?\s*$", re.I),
    "sex": re.compile(r"^(?:sex|gender)\s*[:/-]?\s*$", re.I),
}
IDENTITY_INLINE = {
    field: re.compile(label.pattern.replace(r"\s*[:/-]?\s*$", r"\s*[:/-]\s*(.+)$"), re.I)
    for field, label in IDENTITY_LABELS.items()
}
NAME_VALUE = re.compile(r"^[A-Za-z][A-Za-z .'-]{1,65}$")
SEX_VALUE = re.compile(r"^[MFX]$", re.I)


def _identity_value(field: str, candidate: str) -> bool:
    value = candidate.strip()
    if field == "sex":
        return bool(SEX_VALUE.fullmatch(value))
    if not NAME_VALUE.fullmatch(value):
        return False
    if re.search(r"\b(?:passport|date|surname|nationality|birth|issue|expiry|given|place|sex)\b", value, re.I):
        return False
    return True


def _passport_identity_fields(lines: list[str], page: int) -> list[dict]:
    if not PASSPORT_CONTEXT.search(" ".join(lines[:30])):
        return []
    found = []
    for index, raw in enumerate(lines):
        line = raw.strip()
        if len(line) > 100:
            continue
        for field, label in IDENTITY_LABELS.items():
            candidate = None
            source = line
            inline = IDENTITY_INLINE[field].fullmatch(line)
            if inline:
                candidate = inline.group(1).strip()
            elif label.fullmatch(line) and index + 1 < len(lines):
                candidate = lines[index + 1].strip()
                source = lines[index + 1]
            if candidate and _identity_value(field, candidate):
                found.append({"field": field, "value": candidate, "page": page,
                              "sourceText": source, "requiresConfirmation": True})
    return found


def passport_category(pages: list[Page], fields: list[dict]) -> bool:
    """Conservative fallback hint; do not confuse immigration passes with passports."""
    text = " ".join(page.text for page in pages)
    if re.search(r"\b(?:e\s*-?\s*pass|employment\s+pass|electronic\s+pass)\b", text, re.I):
        return False
    header = " ".join(page.text[:350] for page in pages[:1])
    if not re.search(r"\bpassport\b", header, re.I):
        return False
    names = {f["field"] for f in fields if f["page"] == pages[0].page} if pages else set()
    return len(names.intersection({"date_of_birth", "issue_date", "expiry_date", "passport_number"})) >= 2


def _passport_neighbour_fields(lines: list[str], page: int) -> list[dict]:
    results = []
    for index, line in enumerate(lines):
        if len(line) > 180:
            continue
        # Require a label, followed by the very next OCR line. Avoid old
        # passport references and long unrelated numbers on the reverse page.
        if (PASSPORT_LABEL.search(line) and not HISTORICAL_PASSPORT.search(line)
                and not any(HISTORICAL_PASSPORT.search(previous) for previous in lines[max(0, index-3):index])
                and index + 1 < len(lines)):
            candidate = lines[index + 1].strip().upper()
            if PASSPORT_VALUE.fullmatch(candidate):
                results.append({"field": "passport_number", "value": candidate,
                                "page": page, "sourceText": lines[index + 1],
                                "requiresConfirmation": True})
        for field, label in DATE_LABELS.items():
            if label.search(line) and index + 1 < len(lines):
                neighbour = lines[index + 1].strip()
                match = DATE_VALUE.match(neighbour)
                if match and neighbour[match.end():].strip() not in ("", "M", "F", "X"):
                    match = None
                if match:
                    results.append({"field": field, "value": match.group(0),
                                    "page": page, "sourceText": lines[index + 1],
                                    "requiresConfirmation": True})
        # A combined issue/expiry label is common on passports. Accept only
        # two immediately adjacent dates in left-to-right label order.
        if DATE_LABELS["issue_date"].search(line) and DATE_LABELS["expiry_date"].search(line):
            dates = DATE_VALUE.findall(line)
            if not dates and index + 1 < len(lines):
                dates = DATE_VALUE.findall(lines[index + 1])
                if len(dates) == 2 and not DATE_VALUE.fullmatch(lines[index + 1].strip()):
                    source_line = lines[index + 1]
                else:
                    source_line = line
            else:
                source_line = line
            if len(dates) == 2:
                for field, value in zip(("issue_date", "expiry_date"), dates):
                    results.append({"field": field, "value": value, "page": page,
                                    "sourceText": source_line, "requiresConfirmation": True})
    return results

def structured_fields(pages: list[Page]) -> list[dict]:
    results = []
    seen = set()
    for page in pages:
        lines = page.text.splitlines()
        for suggestion in _passport_neighbour_fields(lines, page.page) + _passport_identity_fields(lines, page.page):
            key = (suggestion["field"], suggestion["value"], page.page)
            if key not in seen:
                seen.add(key)
                results.append(suggestion)
        for line_index, raw in enumerate(lines):
            line = raw.strip()
            if not line or len(line) > 300:
                continue
            # Paired issue/expiry dates are handled together above; running
            # single-label rules here would mislabel the first date as expiry.
            paired_dates = (DATE_LABELS["issue_date"].search(line)
                            and DATE_LABELS["expiry_date"].search(line)
                            and len(DATE_VALUE.findall(line)) == 2)
            for field, pattern in RULES:
                if paired_dates and field in ("issue_date", "expiry_date"):
                    continue
                if field == "passport_number" and (HISTORICAL_PASSPORT.search(line)
                        or any(HISTORICAL_PASSPORT.search(previous) for previous in lines[max(0, line_index-3):line_index])):
                    continue
                match = pattern.search(line)
                if not match:
                    continue
                value = match.group(1).strip().rstrip(" .,:;-")
                if not value or (field == "amount_due" and not re.search(r"\d", value)):
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
    has_bill_title = bool(re.search(r"\b(?:invoice|billing statement|utility bill|broadband bill|internet bill|bil internet|bil bulanan|bil utiliti)\b", text))
    has_payment_field = any(f["field"] in ("amount_due","due_date") for f in fields)
    return has_bill_title and has_payment_field
