"""V5.5 passport field-level OCR/vision review diagnostics.

Compares only explicit, independently label-grounded first-page values.
No inferred dates, no MRZ century resolution, no automatic field updates,
and no raw values in diagnostics.
"""
import re

from .structured_analysis import RULES

FIELDS = ("passport_number", "date_of_birth", "expiry_date")
_RULES = {field: pattern for field, pattern in RULES if field in FIELDS}


def _candidates(text: str) -> dict[str, set[str]]:
    found = {field: set() for field in FIELDS}
    if not isinstance(text, str):
        return found
    for line in text[:12000].splitlines()[:120]:
        if len(line) > 240:
            continue
        for field, pattern in _RULES.items():
            match = pattern.search(line)
            if not match:
                continue
            value = match.group(1).strip()
            if field == "passport_number":
                if not re.fullmatch(r"[A-Za-z0-9]{6,12}", value):
                    continue
                normalized = value.upper()
            else:
                # Compare only exactly matching printed date representations.
                # No day/month swaps, locale assumptions, or century guesses.
                normalized = re.sub(r"\s+", " ", value).upper()
            found[field].add(normalized)
    return found


def field_review(ocr_text: str, vision_text: str) -> dict:
    ocr = _candidates(ocr_text)
    vision = _candidates(vision_text)
    statuses = {}
    for field in FIELDS:
        left, right = ocr[field], vision[field]
        if len(left) > 1 or len(right) > 1:
            status = "ambiguous"
        elif not left or not right:
            status = "insufficient_evidence"
        elif left == right:
            status = "matching_review_only"
        else:
            status = "conflict_requires_review"
        statuses[field] = status
    return {
        "status": ("conflict_requires_review" if "conflict_requires_review" in statuses.values()
                   else "ambiguous" if "ambiguous" in statuses.values()
                   else "review_only"),
        "fields": statuses,
        "requiresConfirmation": True,
        "identityFieldsVerified": False,
        "automaticFieldUpdates": False,
    }
