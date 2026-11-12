"""Optional V5.5 image-only vision diagnostic; never modifies extracted fields."""
import os

from .vision_v55 import VisionUnavailable, probe_image
from .vision_reconciliation_v55 import reconcile_ocr_vision
from .vision_field_review_v55 import field_review

_SUPPORTED = {"image/jpeg", "image/png"}


async def optional_vision_diagnostic(data: bytes, content_type: str, pages) -> dict | None:
    """Return only non-PII review diagnostics, or None when feature is disabled.

    PDF is intentionally excluded until a bounded, isolated page-rendering path
    is designed. Vision errors must not interrupt the existing OCR workflow.
    """
    if os.getenv("DOCUMENT_AI_V55_VISION_ENABLED", "false").lower() != "true":
        return None
    if content_type not in _SUPPORTED:
        return {"status": "unsupported_media", "requiresConfirmation": True}
    if not data or len(data) > 2 * 1024 * 1024:
        return {"status": "image_limit_exceeded", "requiresConfirmation": True}
    try:
        vision_text = await probe_image(data, content_type)
        ocr_text = "\n".join(page.text for page in pages)
        diagnostic = reconcile_ocr_vision(ocr_text, vision_text)
        # Passport identity review uses first-page label-grounded evidence only.
        # Never include the source values in API diagnostics.
        first_page = next((page.text for page in pages if page.page == 1), '')
        diagnostic['passportFieldReview'] = field_review(first_page, vision_text)
        return diagnostic
    except VisionUnavailable:
        return {"status": "vision_unavailable", "requiresConfirmation": True}
    except Exception:
        # Never surface model output, document text, transport errors or prompts.
        return {"status": "vision_unavailable", "requiresConfirmation": True}
