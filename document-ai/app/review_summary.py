"""Summarize source candidates without asserting verified employment terms."""
import re

LABELS = {
    "start_date": "Commencement date",
    "joining_date": "Joining date",
    "relieving_date": "Relieving date",
    "effective_date": "Effective date",
    "annual_compensation": "Annual compensation",
    "revised_annual_ctc": "Revised annual cost to company (CTC)",
    "notice_period": "Notice period",
    "working_hours": "Working hours",
    "medical_condition": "Medical condition",
    "revised_designation": "Revised designation",
    "previous_designation": "Previous designation",
    "continuing_terms": "Continuing employment terms",
}

# Only closed, known phrases are expanded. Never segment names or IDs.
DISPLAY_PHRASES = {
    "employment_restriction": {
        "ANYFORMOFEMPLOYMENTISSTRICTLYPROHIBITED": "Any form of employment is strictly prohibited",
    },
    "continuing_terms": {
        "ALLTHETERMSANDCONDITIONSASSETFORTHINYOURAPPOINTMENTLETTERWILLCONTINUETOBEINFORCE":
            "All the terms and conditions as set forth in your appointment letter will continue to be in force",
    },
}

def display_review_value(field, value):
    display = " ".join(value.split()).rstrip(".")
    compact = re.sub(r"\s+", "", display).upper()
    return DISPLAY_PHRASES.get(field, {}).get(compact, display)


def review_summary(route, fields, pages):
    source = {p.page: p.text for p in pages if p.extractionConfidence >= .80}
    parts = ["Possible " + route.subtype.replace("_", " ") + "."]
    for key, label in LABELS.items():
        items = [f for f in fields if f.get("field") == key]
        if len(items) != 1:
            continue
        field = items[0]
        value = field.get("value")
        quote = field.get("sourceText")
        if not label or not isinstance(value, str) or not isinstance(quote, str):
            continue
        if not value.strip() or len(value) > (250 if key == "continuing_terms" else 100) or value not in quote:
            continue
        if quote not in source.get(field.get("page"), ""):
            continue
        parts.append(f"{label} (review): {display_review_value(key, value)}.")
    parts.append("Confirm all details against the original. AI interpretation was incomplete.")
    return " ".join(parts)


def immigration_summary(category, fields, pages):
    """Summarize only explicit, non-conflicting stay details for review."""
    labels = {"pass_category": "Pass category", "issue_date": "Issue date",
              "expiry_date": "Stay validity until", "employer": "Employer",
              "designation": "Designation", "employment_restriction": "Employment restriction",
              "letter_validity": "Letter validity", "place_of_issue": "Place of issue"}
    if category not in ("Employment Pass", "Dependant Pass", "Student Pass", "Work Permit"):
        labels["expiry_date"] = "Document expiry date"
    source = {p.page: p.text for p in pages if p.extractionConfidence >= .80}
    parts = [category + " details extracted for review."]
    for key, label in labels.items():
        items = [f for f in fields if f.get("field") == key]
        if len(items) != 1:
            continue
        f = items[0]
        value, quote = f.get("value"), f.get("sourceText")
        if (isinstance(value, str) and isinstance(quote, str)
                and 0 < len(value.strip()) <= 200 and value in quote
                and quote in source.get(f.get("page"), "")):
            parts.append(f"{label} (review): {display_review_value(key, value)}.")
    parts.append("Confirm against the original. This analysis does not establish legal status.")
    return " ".join(parts)
