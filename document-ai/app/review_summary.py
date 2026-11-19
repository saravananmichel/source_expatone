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
