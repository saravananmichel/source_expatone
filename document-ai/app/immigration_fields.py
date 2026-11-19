"""Review-only extraction for conservatively routed immigration documents."""
import re

LABELS = {
    "person_name": r"(?:applicant(?:'s)? name|name of applicant(?:\s*/\s*dependant)?|name)",
    "passport_number": r"passport\s*(?:no\.?|number)",
    "nationality": r"nationality",
    "employer": r"(?:employer|company name|with)",
    "designation": r"(?:designation|position|job title)",
    "sponsor": r"(?:sponsor|sponsor name|name of foreign knowledge worker)",
    "relationship": r"relationship",
    "application_type": r"application type",
    "application_category": r"application category",
    "application_duration": r"(?:application duration|requested duration|duration)",
    "reference_number": r"(?:(?:application|immigration|reference|ref)\s*(?:no\.?|number)|our reference)",
    "pass_number": r"(?:pass\s*(?:no\.?|number)|vp\s*no\.?)",
    "issue_date": r"(?:date\s*of\s*issue|issue date)",
    "expiry_date": r"(?:date of expiry|expiry date|valid until)",
    "letter_validity": r"(?:letter validity|letter valid until)",
}
APPLICATION_ONLY = {"application_type", "application_category", "application_duration",
                    "sponsor", "relationship", "letter_validity"}
ISSUED_ONLY = {"issue_date", "expiry_date", "pass_number", "employer", "designation"}


def immigration_fields(pages, route):
    if route is None or route.subtype not in {
        "dependant_pass_application", "employment_pass_with_visa", "dependant_pass_with_visa"
    }:
        return []
    excluded = (ISSUED_ONLY if route.subtype == "dependant_pass_application"
                else APPLICATION_ONLY)
    groups = {}
    for page in pages:
        if page.extractionConfidence < .80:
            continue
        sources = [m.group() for m in re.finditer(r"(?m)^[^\n:：]+(?:[ \t]*\n[ \t]*)?[:：][ \t]*[^\n]+", page.text)]
        for line in sources:
            source = line.strip()
            for field, label in LABELS.items():
                if field in excluded:
                    continue
                match = re.fullmatch(r"(?:" + label + r")\s*[:：]\s*(.{1,200})", source, re.I)
                if not match:
                    continue
                value = match.group(1).strip()
                if not value or value.lower() in {"n/a", "none", "unknown", "-"}:
                    continue
                if field == "passport_number" and not re.fullmatch(r"[A-Z0-9]{5,15}", value, re.I):
                    continue
                groups.setdefault(field, []).append({
                    "field": field, "value": value, "page": page.page,
                    "sourceText": source, "requiresConfirmation": True,
                })
        if route.subtype.endswith("_with_visa"):
            # Only the explicit stay condition, not the multiple-entry visa line.
            pattern = r"(?im)^Condition[ \t]*\n(?:[：:]?[ \t]*PERMITTED[ \t]*TO[ \t]*ENTER[ \t]*AND[ \t]*REMAIN[^\n]*\n)?UNTIL[ \t\r\n]*(\d{1,2}[ \t]*[A-Z]{3}[ \t]*\d{4})\b"
            for match in re.finditer(pattern, page.text):
                groups.setdefault("expiry_date", []).append({
                    "field": "expiry_date", "value": match.group(1),
                    "page": page.page, "sourceText": match.group(),
                    "requiresConfirmation": True,
                })
            if route.subtype == "dependant_pass_with_visa":
                for match in re.finditer(r"(?im)^ANY[ \t]*FORM[ \t]*OF[ \t]*EMPLOYMENT[ \t]*IS[ \t]*STRICTLY[ \t]*PROHIBITED\.?$", page.text):
                    groups.setdefault("employment_restriction", []).append({
                        "field": "employment_restriction", "value": match.group(),
                        "page": page.page, "sourceText": match.group(),
                        "requiresConfirmation": True,
                    })
    return [items[0] for items in groups.values()
            if len({item["value"] for item in items}) == 1]
