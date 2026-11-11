"""Regression tests for conservative scan cleanup; synthetic text only."""
from PIL import Image
import pytest
from app import extraction


def ocr_result(text, confidence):
    return {
        'text': [text], 'conf': [str(confidence)],
        'block_num': [1], 'par_num': [1], 'line_num': [1],
        'left': [10], 'top': [20], 'width': [200], 'height': [30],
    }


@pytest.mark.parametrize(
    'original,cleaned,expected,expected_calls',
    [
        (96, 99, 96, 1),    # Clear scans skip preprocessing.
        (70, 75, 75, 5),    # Low confidence and meaningful gain: accept cleanup.
        (70, 71, 70, 5),    # Marginal gain: preserve original.
        (70, 35, 70, 5),    # Cleanup damages OCR: preserve original.
    ],
)
def test_ocr_cleanup_confidence_gate(monkeypatch, original, cleaned, expected, expected_calls):
    calls = []
    monkeypatch.setattr(
        extraction.pytesseract, 'image_to_osd',
        lambda *args, **kwargs: (_ for _ in ()).throw(RuntimeError('no OSD')),
    )

    def fake_data(image, **kwargs):
        calls.append(image.mode)
        if len(calls) == 1:
            return ocr_result('DUE DATE 2026-12-15 RM125.60', original)
        if len(calls) == 2:
            assert image.mode == 'L'
            return ocr_result('DUE DATE 2026-12-15 RM125.60', cleaned)
        # The three orientation retries should never improve the chosen score.
        return ocr_result('DUE DATE 2026-12-15 RM125.60', 0)

    monkeypatch.setattr(extraction.pytesseract, 'image_to_data', fake_data)
    page = extraction.ocr(Image.new('RGB', (800, 600), 'white'), 1)
    assert page.extraction == 'ocr'
    assert page.extractionConfidence == pytest.approx(expected / 100)
    assert '2026-12-15' in page.text
    assert 'RM125.60' in page.text
    assert page.blocks[0].boundingBox == [10, 20, 210, 50]
    assert len(calls) == expected_calls


def test_cleanup_error_keeps_original_ocr(monkeypatch):
    calls = []
    monkeypatch.setattr(
        extraction.pytesseract, 'image_to_osd',
        lambda *args, **kwargs: (_ for _ in ()).throw(RuntimeError('no OSD')),
    )

    def fake_data(image, **kwargs):
        calls.append(image.mode)
        if len(calls) == 2:
            raise RuntimeError('cleanup OCR unavailable')
        return ocr_result('ACCOUNT 00012345', 75 if len(calls) == 1 else 0)

    monkeypatch.setattr(extraction.pytesseract, 'image_to_data', fake_data)
    page = extraction.ocr(Image.new('RGB', (800, 600), 'white'), 1)
    assert '00012345' in page.text
    assert page.extractionConfidence == pytest.approx(0.75)
