"""Evidence-backed structured field suggestions for the analysis response.

Never turn a suggestion into a verified fact, reminder or persisted identity.
All values must be literal substrings of their source line.
"""
import re
from datetime import datetime
from .schemas import Page
from .passport_mrz import mrz_fields

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
HISTORICAL_PASSPORT = re.compile(r"\b(?:old|previous|prior|former|cancelled|canceled)\s*passport\b", re.I)


# Strict label-grounded identity extraction. These are suggestions, never verified data.
PASSPORT_CONTEXT = re.compile(r"\b(?:passport|republic\s+of\s+india)\b", re.I)
# Header OCR is often fragmented by security printing. This hint is only used
# to attempt evidence-backed extraction; it never verifies a document category.
PASSPORT_HEADER_FRAGMENT = re.compile(
    r"\brepublic\s*of\s*[|:/-]?\s*india\b|\bpassport\s*(?:no|number)\b", re.I)


def _passport_context(lines: list[str]) -> bool:
    text = " ".join(lines[:35])
    if re.search(r"\b(?:e\s*-?\s*pass|employment\s+pass|electronic\s+pass)\b", text, re.I):
        return False
    return bool(PASSPORT_CONTEXT.search(text) or PASSPORT_HEADER_FRAGMENT.search(text))


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
PLACE_VALUE = re.compile(r"^[A-Za-z][A-Za-z ,.'-]{1,85}$")
SEX_VALUE = re.compile(r"^[MFX]$", re.I)


def _identity_value(field: str, candidate: str) -> bool:
    value = candidate.strip()
    if field == "sex":
        return bool(SEX_VALUE.fullmatch(value))
    if not (PLACE_VALUE if field in ('place_of_birth', 'place_of_issue') else NAME_VALUE).fullmatch(value):
        return False
    if re.search(r"\b(?:passport|date|surname|nationality|birth|issue|expiry|given|place|sex)\b", value, re.I):
        return False
    return True


def _passport_identity_fields(lines: list[str], page: int) -> list[dict]:
    if not _passport_context(lines):
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


def _compact_passport_identity_layout(page: Page) -> bool:
    """Require independent identity labels, not nationality alone."""
    if page.page != 1 or page.extraction != "ocr":
        return False
    text = page.text
    if re.search(r"\b(?:e\s*-?\s*pass|employment\s+pass|electronic\s+pass|certificate|visa)\b", text, re.I):
        return False
    labels = set()
    for block in page.blocks:
        for field, pattern in (*SPATIAL_IDENTITY_SUFFIX.items(), *SPATIAL_DATE_SUFFIX.items()):
            if pattern.search(block.text.strip()):
                labels.add(field)
    return {"surname", "given_names", "nationality", "date_of_birth",
            "issue_date", "expiry_date"}.issubset(labels)


def passport_category(pages: list[Page], fields: list[dict]) -> bool:
    """Conservative fallback hint; do not confuse immigration passes with passports."""
    text = " ".join(page.text for page in pages)
    if re.search(r"\b(?:e\s*-?\s*pass|employment\s+pass|electronic\s+pass)\b", text, re.I):
        return False
    first_page = pages[0].text if pages else ""
    header = first_page[:700]
    explicit = bool(re.search(r"\bpassport\b", header, re.I))
    mrz_prefix = bool(re.search(r"(?m)^\s*P<[A-Z<]{3}", first_page, re.I))
    identity_labels = sum(bool(re.search(pattern, first_page, re.I)) for pattern in (
        r"\bsurname\b", r"\bgiven\s+names?\b",
        r"\bdate\s+of\s+birth\b", r"\bplace\s+of\s+birth\b",
        r"\bdate\s+of\s+issue\b", r"\bdate\s+of\s+expiry\b"))
    names = {f["field"] for f in fields if f["page"] == pages[0].page} if pages else set()
    dated = len(names.intersection({"date_of_birth", "issue_date", "expiry_date", "passport_number"}))
    # OCR may break the country header or the word PASSPORT. Require several
    # independent passport-specific labels before suggesting the category.
    header_fragment = bool(PASSPORT_HEADER_FRAGMENT.search(header))
    passport_number_label = bool(PASSPORT_LABEL.search(first_page))
    compact_layout = bool(pages and _compact_passport_identity_layout(pages[0]))
    return ((compact_layout and {"surname", "given_names", "date_of_birth", "issue_date", "expiry_date"}.issubset(names))
            or (explicit and (dated >= 2 or identity_labels >= 3))
            or (mrz_prefix and identity_labels >= 2)
            or (identity_labels >= 4 and dated >= 1)
            or (header_fragment and passport_number_label and identity_labels >= 2))


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

# OCR often prepends untranslated script or decorative characters before
# English passport labels. Anchor to the *end* of the line, not its start.
OCR_IDENTITY_SUFFIX = {
    "surname": re.compile(r"\b(?:surname|family\s+name)\s*[:/-]?\s*$", re.I),
    "given_names": re.compile(r"\b(?:given\s+name\s*\(s\)|given\s+names?|first\s+names?)\s*[:/-]?\s*$", re.I),
    "nationality": re.compile(r"\bnationality\s*[:/-]?\s*$", re.I),
    "place_of_birth": re.compile(r"\bplace\s+of\s+birth\s*[:/-]?\s*$", re.I),
    "place_of_issue": re.compile(r"\bplace\s+of\s+issue\s*[:/-]?\s*$", re.I),
    "sex": re.compile(r"\b(?:sex|gender)\s*[:/-]?\s*$", re.I),
}
OCR_DATE_SUFFIX = {
    "date_of_birth": re.compile(r"\bdate\s+of\s+birth\s*[:/-]?\s*$", re.I),
    "issue_date": re.compile(r"\bdate\s+of\s+issue\s*[:/-]?\s*$", re.I),
    "expiry_date": re.compile(r"\bdate\s+of\s+expiry\s*[:/-]?\s*$", re.I),
}


SPATIAL_IDENTITY_SUFFIX = {
    "surname": re.compile(r"\b(?:surname|suname|family\s+name)\s*[:/-]?\s*$", re.I),
    "given_names": re.compile(r"\b(?:given\s*name\s*\(s\)|given\s*names?|first\s+names?)\s*[:/-]?\s*$", re.I),
    "nationality": re.compile(r"\bnationality\s*[:/-]?\s*$", re.I),
    "place_of_birth": re.compile(r"\bplace\s*of\s*(?:birth|binh)\s*[:/-]?\s*$", re.I),
    "place_of_issue": re.compile(r"\bplace\s*of\s*issue\s*[:/-]?\s*$", re.I),
    "sex": re.compile(r"\b(?:sex|gender)\s*[:/-]?\s*$", re.I),
}
SPATIAL_DATE_SUFFIX = {
    "date_of_birth": re.compile(r"\bdate\s*of\s*birth\s*[:/-]?\s*$", re.I),
    "issue_date": re.compile(r"\bdate\s*of\s*[il]ssue\s*[:/-]?\s*$", re.I),
    "expiry_date": re.compile(r"\bdate\s*of\s*expiry\s*[:/-]?\s*$", re.I),
}


def _passport_ocr_suffix_fields(lines: list[str], page: int) -> list[dict]:
    """Use source-grounded OCR label suffixes; never infer missing values."""
    if page != 1 or not _passport_context(lines):
        return []
    results = []
    for index, line in enumerate(lines[:-1]):
        if len(line) > 130:
            continue
        candidate = lines[index + 1].strip()
        for field, pattern in OCR_IDENTITY_SUFFIX.items():
            if pattern.search(line) and _identity_value(field, candidate):
                results.append({"field": field, "value": candidate, "page": page,
                                "sourceText": lines[index + 1], "requiresConfirmation": True})
        for field, pattern in OCR_DATE_SUFFIX.items():
            if pattern.search(line):
                match = DATE_VALUE.match(candidate)
                if match and candidate[match.end():].strip() in ("", "M", "F", "X"):
                    results.append({"field": field, "value": match.group(0), "page": page,
                                    "sourceText": lines[index + 1], "requiresConfirmation": True})
    return results



def _passport_ocr_additional_fields(lines: list[str], page: int) -> list[dict]:
    """Conservative first-page passport recovery, without guessed identity."""
    if page != 1 or not _passport_context(lines):
        return []
    found = []
    for index, line in enumerate(lines):
        # Sex is often printed on the same OCR row as DOB; do not use
        # an unlabelled standalone letter elsewhere on the page.
        if index and DATE_LABELS["date_of_birth"].search(lines[index - 1]):
            match = re.fullmatch(r"\s*(" + DATE + r")\s+([MFX])\s*", line, re.I)
            if match:
                found.append({"field": "sex", "value": match.group(2),
                              "page": page, "sourceText": line,
                              "requiresConfirmation": True})
        # Only accept a clean, adjacent passport number. Never strip or
        # reconstruct noisy OCR fragments, and never use historical numbers.
        if PASSPORT_LABEL.search(line) and not HISTORICAL_PASSPORT.search(line):
            if any(HISTORICAL_PASSPORT.search(previous)
                   for previous in lines[max(0, index - 3):index]):
                continue
            if index + 1 < len(lines):
                candidate = lines[index + 1].strip()
                if PASSPORT_VALUE.fullmatch(candidate):
                    found.append({"field": "passport_number", "value": candidate,
                                  "page": page, "sourceText": lines[index + 1],
                                  "requiresConfirmation": True})
    return found


def _passport_spatial_fields(page: Page) -> list[dict]:
    """Recover unambiguous, source-grounded OCR values from nearby text blocks."""
    if page.page != 1 or page.extraction != "ocr" or not page.blocks:
        return []
    if not (_passport_context(page.text.splitlines()) or _compact_passport_identity_layout(page)):
        return []
    found = []
    for label_block in page.blocks:
        label_text = label_block.text.strip()
        labels = [field for field, pattern in SPATIAL_IDENTITY_SUFFIX.items()
                  if pattern.search(label_text)]
        labels += [field for field, pattern in SPATIAL_DATE_SUFFIX.items()
                   if pattern.search(label_text)]
        if PASSPORT_LABEL.search(label_text) and not HISTORICAL_PASSPORT.search(label_text):
            labels.append("passport_number")
        if len(labels) != 1:
            continue
        field = labels[0]
        lx0, ly0, lx1, ly1 = label_block.boundingBox
        height = max(1, ly1 - ly0)
        candidates = []
        for block in page.blocks:
            if block is label_block:
                continue
            x0, y0, x1, y1 = block.boundingBox
            below = (y0 >= ly1 - height * 0.35 and y0 <= ly1 + height * 2
                     and abs(x0 - lx0) <= height * 2)
            right = (x0 >= lx1 and x0 - lx1 <= height * 7
                     and abs(y0 - ly0) <= height * 0.6)
            if not (below or right):
                continue
            value = block.text.strip()
            if field == "nationality" and "/" in value:
                value = value.rsplit("/", 1)[-1].strip()
            if field == "passport_number":
                valid = bool(PASSPORT_VALUE.fullmatch(value))
            elif field in SPATIAL_DATE_SUFFIX:
                valid = bool(DATE_VALUE.fullmatch(value))
            else:
                valid = _identity_value(field, value)
            if not valid:
                continue
            # Never associate another recognised label as a value.
            if any(p.search(value) for p in (*SPATIAL_IDENTITY_SUFFIX.values(), *SPATIAL_DATE_SUFFIX.values())):
                continue
            distance = (max(0, y0 - ly1) + abs(x0 - lx0)) if below else (x0 - lx1)
            candidates.append((distance, block))
        # Prefer the directly aligned column over a different-column date.
        if field in SPATIAL_DATE_SUFFIX:
            aligned = [(d, b) for d, b in candidates
                       if abs(b.boundingBox[0] - lx0) <= height
                       and b.boundingBox[1] >= ly1 - height * 0.35]
            if aligned:
                candidates = aligned
        # A competing plausible value must be shown as missing, not guessed.
        if len(candidates) != 1:
            continue
        block = candidates[0][1]
        extracted = block.text.strip()
        if field == "nationality" and "/" in extracted:
            extracted = extracted.rsplit("/", 1)[-1].strip()
        found.append({"field": field, "value": extracted,
                      "page": page.page, "sourceText": block.text,
                      "requiresConfirmation": True})
    return found


def _passport_layout_number(page: Page) -> list[dict]:
    """Review-only number in the identity header row; never inspect reverse pages."""
    if not _compact_passport_identity_layout(page):
        return []
    blocks = page.blocks
    def header(pattern):
        return [b for b in blocks if re.search(pattern, b.text.strip(), re.I)]
    types = header(r"(?:^|/)\s*type$")
    codes = header(r"(?:^|/)\s*code$")
    nations = header(r"(?:^|/)\s*nationality$")
    if any(len(group) != 1 for group in (types, codes, nations)):
        return []
    t, c, n = types[0], codes[0], nations[0]
    tx, ty, _, tb = t.boundingBox
    cx, cy, _, cb = c.boundingBox
    nx, ny, nr, nb = n.boundingBox
    h = max(1, nb - ny)
    if not (tx < cx < nx and ny < page.height * .35
            and abs(ty - ny) <= h and abs(cy - ny) <= h):
        return []
    def row_value(label, pattern):
        x, y, right, bottom = label.boundingBox
        return [b for b in blocks if x-h <= b.boundingBox[0] <= right
                and bottom-h*.35 <= b.boundingBox[1] <= bottom+h*1.5
                and re.fullmatch(pattern, b.text.strip())]
    if len(row_value(t, r"P")) != 1 or len(row_value(c, r"[A-Z]{3}")) != 1:
        return []
    candidates = [b for b in blocks if b.boundingBox[0] >= nr
                  and nb-h*.35 <= b.boundingBox[1] <= nb+h*1.5
                  and PASSPORT_VALUE.fullmatch(b.text.strip())]
    if len(candidates) != 1:
        return []
    b = candidates[0]
    return [{"field": "passport_number", "value": b.text.strip(),
             "page": page.page, "sourceText": b.text,
             "requiresConfirmation": True}]


def _passport_grouped_rows(page: Page) -> list[dict]:
    """Recover explicit label runs followed by equally ordered value runs."""
    if page.page != 1 or page.extraction != "ocr" or page.extractionConfidence < .80:
        return []
    lines = page.text.splitlines()
    if not re.search(r"(?im)^\s*P<[A-Z]{3}", page.text) or not any(PASSPORT_LABEL.search(l) for l in lines):
        return []
    labels = {
        "date_of_birth": r"date\s*of\s*birth",
        "sex": r"sex",
        "nationality": r"national(?:i)?ty",
        "issue_date": r"date\s*of\s*[il]ssue",
        "expiry_date": r"date\s*of\s*expiry",
    }
    found = []
    for fields in (("date_of_birth", "sex", "nationality"), ("issue_date", "expiry_date")):
        size = len(fields)
        matches = []
        for i in range(len(lines)-2*size+1):
            if not all(re.search(r"\b"+labels[f]+r"\s*$", lines[i+j], re.I) for j,f in enumerate(fields)):
                continue
            values = [l.strip() for l in lines[i+size:i+2*size]]
            valid = True
            dates = []
            for field, value in zip(fields, values):
                if field.endswith("_date") or field == "date_of_birth":
                    parsed = None
                    for fmt in ("%d/%m/%Y", "%d-%m-%Y", "%d.%m.%Y", "%Y-%m-%d"):
                        try:
                            parsed = datetime.strptime(value, fmt)
                            break
                        except ValueError:
                            pass
                    valid &= parsed is not None
                    dates.append(parsed)
                elif field == "sex":
                    valid &= bool(SEX_VALUE.fullmatch(value))
                else:
                    valid &= bool(re.fullmatch(r"[A-Z]{3,35}", value))
            if not valid or (len(dates)==2 and dates[0]>=dates[1]):
                continue
            matches.append((values, "\n".join(lines[i:i+2*size])))
        if len(matches)==1:
            values, quote = matches[0]
            found.extend({"field": f, "value": v, "page": 1, "sourceText": quote,
                          "requiresConfirmation": True} for f,v in zip(fields,values))
    return found


def structured_fields(pages: list[Page]) -> list[dict]:
    results = []
    seen = set()
    for page in pages:
        lines = page.text.splitlines()
        for suggestion in _passport_neighbour_fields(lines, page.page) + _passport_identity_fields(lines, page.page) + _passport_ocr_suffix_fields(lines, page.page) + _passport_ocr_additional_fields(lines, page.page) + _passport_spatial_fields(page) + _passport_layout_number(page) + _passport_grouped_rows(page):
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
    for suggestion in mrz_fields(pages[:1]):
        key = (suggestion['field'], suggestion['value'], suggestion['page'])
        if key not in seen:
            seen.add(key)
            results.append(suggestion)
    return results

def billing_category(pages: list[Page], fields: list[dict]) -> bool:
    """Suggest Utility Bill only with explicit billing context AND payment/account evidence."""
    text = " ".join(page.text for page in pages).lower()
    has_bill_title = bool(re.search(r"\b(?:invoice|billing statement|utility bill|broadband bill|internet bill|bil internet|bil bulanan|bil utiliti)\b", text))
    has_payment_field = any(f["field"] in ("amount_due","due_date") for f in fields)
    return has_bill_title and has_payment_field


def passport_reverse_fields(pages):
    """Review-only, explicitly labelled reverse-page fields after identity gating."""
    if len(pages) != 2 or pages[1].extractionConfidence < .80:
        return []
    from datetime import datetime
    page = pages[1]
    labels = {
        "father_name": r"name of father(?:\s*/\s*legal guardian)?",
        "mother_name": r"name of mother",
        "spouse_name": r"name of spouse",
        "previous_passport_number": r"(?:old|previous) passport (?:no\.?|number)",
        "previous_passport_issue_date": r"(?:old|previous) passport (?:date of issue|issue date)",
        "previous_passport_place_of_issue": r"(?:old|previous) passport place of issue",
    }
    result = []
    for field, label in labels.items():
        matches = list(re.finditer(r"(?im)^[ \t]*(?:" + label +
                                   r")[ \t]*(?:[:：][ \t]*|\r?\n[ \t]*)([^\r\n]+)$", page.text))
        if len(matches) != 1:
            continue
        match = matches[0]
        value = match.group(1).strip()
        if field == "previous_passport_number":
            valid = PASSPORT_VALUE.fullmatch(value)
        elif field == "previous_passport_issue_date":
            valid = False
            for fmt in ("%d/%m/%Y", "%d-%m-%Y", "%Y-%m-%d", "%d.%m.%Y"):
                try:
                    datetime.strptime(value, fmt)
                    valid = True
                    break
                except ValueError:
                    continue
        elif field == "previous_passport_place_of_issue":
            valid = _identity_value("place_of_issue", value)
        else:
            valid = _identity_value("surname", value) and not re.search(
                r"\b(?:name|father|mother|spouse|guardian|address|none|nil)\b", value, re.I)
        if valid:
            result.append({"field": field, "value": value, "page": page.page,
                           "sourceText": match.group(), "requiresConfirmation": True})
    # Accept address only between explicit labels, never arbitrary trailing lines.
    addresses = list(re.finditer(
        r"(?im)^[ \t]*Address[ \t]*(?:[:：][ \t]*|\r?\n)(?P<value>[^\r\n]+(?:\r?\n[^\r\n]+){0,4}?)(?=\r?\n[ \t]*(?:Old passport|Previous passport|File no\.?|File number)\b)",
        page.text))
    if len(addresses) == 1:
        match = addresses[0]
        value = match.group("value").strip()
        if (8 <= len(value) <= 300 and re.search(r"\d", value)
                and re.search(r"[A-Za-z]", value)
                and not re.search(r"\b(?:passport|date of issue|name of|file number|file no)\b", value, re.I)):
            result.append({"field": "residential_address", "value": value,
                           "page": page.page, "sourceText": match.group(),
                           "requiresConfirmation": True})
    # OCR suffixes may omit spaces and contain bilingual prefixes.
    lines = page.text.splitlines()
    suffixes = {
        "father_name": r"nameoffathe(?:r)?(?:/?(?:l)?egalguardian)?",
        "mother_name": r"(?:name|hame)ofmother",
        "spouse_name": r"nameofspouse",
        "residential_address": r"address",
        "historical_group": r"(?:old|previous)passportno\.?withdateandplaceofissue",
        "passport_file_number": r"f(?:i)?leno\.?",
    }
    anchors = []
    for index, line in enumerate(lines):
        compact = re.sub(r"\s+", "", line).casefold()
        for field, pattern in suffixes.items():
            if re.search(r"(?:^|/)" + pattern + r"$", compact):
                anchors.append((index, field))
    def append(field, value, quote):
        if not any(f["field"] == field for f in result):
            result.append({"field": field, "value": value, "page": page.page,
                           "sourceText": quote, "requiresConfirmation": True})
    for pos, (start, field) in enumerate(anchors):
        if sum(f == field for _, f in anchors) != 1:
            continue
        end = anchors[pos+1][0] if pos+1 < len(anchors) else len(lines)
        if end-start > 6:
            continue
        values = [v.strip() for v in lines[start+1:end] if v.strip()]
        quote = "\n".join(lines[start:end])
        if quote not in page.text:
            continue
        if field in ("father_name", "mother_name", "spouse_name"):
            names = [v for v in values if _identity_value("surname", v)
                     and not re.search(r"address|guardian|passport|mother|father|spouse|none|nil", v, re.I)]
            if len(names) == 1:
                append(field, names[0], quote)
        elif field == "residential_address" and pos+1 < len(anchors):
            value = "\n".join(lines[start+1:end]).strip()
            if (8 <= len(value) <= 300 and re.search(r"\d", value)
                    and re.search(r"[A-Za-z]", value)
                    and not re.search(r"passport|name\s*of|file\s*no", value, re.I)):
                append(field, value, quote)
        elif field == "historical_group" and len(values) == 3:
            numbers = [v for v in values if PASSPORT_VALUE.fullmatch(v)]
            places = [v for v in values if _identity_value("place_of_issue", v)]
            dates = []
            for v in values:
                for fmt in ("%d/%m/%Y", "%d-%m-%Y"):
                    try:
                        datetime.strptime(v, fmt)
                        dates.append(v)
                        break
                    except ValueError:
                        pass
            if len(numbers) == len(places) == len(dates) == 1:
                append("previous_passport_number", numbers[0], quote)
                append("previous_passport_issue_date", dates[0], quote)
                append("previous_passport_place_of_issue", places[0], quote)
        elif field == "passport_file_number" and len(values) == 1:
            if re.fullmatch(r"[A-Z]{2,4}[0-9]{8,18}", values[0]):
                append(field, values[0], quote)
    return result
