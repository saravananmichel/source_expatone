"""Review-only OCR/vision reconciliation tests."""
from app.vision_reconciliation_v55 import reconcile_ocr_vision


def test_missing_or_unreadable_vision():
    for vision in ("", "UNREADABLE", None):
        result = reconcile_ocr_vision("PASSPORT NUMBER ABC123", vision)
        assert result["status"] == "unavailable"
        assert result["requiresConfirmation"] is True


def test_overlap_never_promotes_identity():
    ocr = "PASSPORT DOCUMENT NUMBER ABC123456 COUNTRY MALAYSIA"
    vision = "PASSPORT DOCUMENT NUMBER ABC123456 COUNTRY MALAYSIA"
    result = reconcile_ocr_vision(ocr, vision)
    assert result["status"] == "high_overlap_review_only"
    assert result["identityFieldsVerified"] is False
    assert result["automaticFieldUpdates"] is False
    assert "ABC123456" not in str(result)
    assert "MALAYSIA" not in str(result)


def test_conflict_does_not_expose_values():
    result = reconcile_ocr_vision(
        "PASSPORT NUMBER ABC123456 BIRTH 010101",
        "PASSPORT NUMBER XYZ999999 BIRTH 020202",
    )
    assert result["status"] in ("low_overlap", "partial_overlap")
    assert result["requiresConfirmation"] is True
    assert "ABC123456" not in str(result)
    assert "XYZ999999" not in str(result)


def test_short_tokens_do_not_count():
    result = reconcile_ocr_vision("A B C", "A B C")
    assert result["status"] == "insufficient_evidence"
