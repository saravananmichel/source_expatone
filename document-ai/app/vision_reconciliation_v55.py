"""V5.5 review-only OCR/vision comparison.

Never treats vision output as authoritative or promotes extracted identity values.
Diagnostics contain no source text, passport numbers, dates, or personal names.
"""
import re

_TOKEN = re.compile(r"[A-Z0-9]{3,}")
_MAX_TEXT = 12000


def _tokens(value: str) -> set[str]:
    if not isinstance(value, str):
        return set()
    return set(_TOKEN.findall(value[:_MAX_TEXT].upper()))


def reconcile_ocr_vision(ocr_text: str, vision_text: str) -> dict:
    """Conservative overlap signal, not field-level verification.

    A high overlap is *not* evidence that a passport date or number is correct.
    """
    if not isinstance(ocr_text, str) or not isinstance(vision_text, str):
        return {"status": "unavailable", "requiresConfirmation": True}
    if not ocr_text.strip() or not vision_text.strip() or vision_text.strip().upper() == "UNREADABLE":
        return {"status": "unavailable", "requiresConfirmation": True}
    ocr = _tokens(ocr_text)
    vision = _tokens(vision_text)
    if not ocr or not vision:
        return {"status": "insufficient_evidence", "requiresConfirmation": True}
    overlap = len(ocr & vision)
    # Require multiple shared tokens before even reporting an overlap signal.
    if overlap < 3:
        status = "low_overlap"
    elif overlap / max(len(ocr), len(vision)) >= 0.7:
        status = "high_overlap_review_only"
    else:
        status = "partial_overlap"
    return {
        "status": status,
        "requiresConfirmation": True,
        "sharedTokenCount": overlap,
        "ocrTokenCount": len(ocr),
        "visionTokenCount": len(vision),
        "identityFieldsVerified": False,
        "automaticFieldUpdates": False,
    }
