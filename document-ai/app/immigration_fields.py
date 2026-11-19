"""Review-only extraction for conservatively routed immigration documents."""
import re

LABELS = {
    "person_name": r"(?:applicant(?:'s)? name|name of applicant|name)",
    "passport_number": r"passport\s*(?:no\.?|number)",
    "nationality": r"nationality",
    "employer": r"(?:employer|company name)",
    "designation": r"(?:designation|position|job title)",
    "sponsor": r"(?:sponsor|sponsor name)",
    "relationship": r"relationship",
    "application_type": r"application type",
    "application_category": r"application category",
    "application_duration": r"(?:application duration|requested duration)",
    "reference_number": r"(?:application|immigration|reference)\s*(?:no\.?|number)",
    "pass_number": r"pass\s*(?:no\.?|number)",
    "issue_date": r"(?:date of issue|issue date)",
    "expiry_date": r"(?:date of expiry|expiry date|valid until)",
    "letter_validity": r"(?:letter validity|letter valid until)",
}
APPLICATION_ONLY = {"application_type", "application_category", "application_duration",
                    "sponsor", "relationship", "letter_validity"}
ISSUED_ONLY = {"issue_date", "expiry_date", "pass_number", "employer", "designation"}


def immigration_fields(pages, route):
    if route is None or route.subtype not in {
        "dependant_pass_application", "employment_pass_with_visa"
    }:
        return []
    excluded = (ISSUED_ONLY if route.subtype == "dependant_pass_application"
                else APPLICATION_ONLY)
    groups = {}
    for page in pages:
        if page.extractionConfidence < .80:
            continue
        for line in page.text.splitlines():
            source = line.strip()
            for field, label in LABELS.items():
                if field in excluded:
                    continue
                match = re.fullmatch(r"(?:" + label + r")\s*:\s*(.{1,200})", source, re.I)
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
    return [items[0] for items in groups.values()
            if len({item["value"] for item in items}) == 1]
