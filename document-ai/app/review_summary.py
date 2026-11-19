"""Summarize source candidates without asserting verified employment terms."""
import re

LABELS = {
    "start_date": "Commencement date",
    "joining_date": "Joining date",
    "relieving_date": "Relieving date",
    "effective_date": "Effective date",
    "annual_compensation": "Annual compensation",
    "notice_period": "Notice period",
    "working_hours": "Working hours",
    "medical_condition": "Medical condition",
}

def review_summary(route, fields, pages):
    source = {p.page: p.text for p in pages if p.extractionConfidence >= .80}
    parts = ["Possible " + route.subtype.replace("_", " ") + "."]
    for field in fields:
        label = LABELS.get(field.get("field"))
        value = field.get("value")
        quote = field.get("sourceText")
        if not label or not isinstance(value, str) or not isinstance(quote, str):
            continue
        if not value.strip() or len(value) > 100 or value not in quote:
            continue
        if quote not in source.get(field.get("page"), ""):
            continue
        parts.append(f"{label} (review): {' '.join(value.split())}.")
    parts.append("Confirm all details against the original. AI interpretation was incomplete.")
    return " ".join(parts)


def immigration_summary(category, fields, pages):
    """Summarize only explicit, non-conflicting stay details for review."""
    labels = {"pass_category": "Pass category", "issue_date": "Issue date",
              "expiry_date": "Stay validity until", "employer": "Employer",
              "designation": "Designation", "employment_restriction": "Employment restriction",
              "letter_validity": "Letter validity"}
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
            parts.append(f"{label} (review): {' '.join(value.split())}.")
    parts.append("Confirm against the original. This analysis does not establish legal status.")
    return " ".join(parts)
