"""Synthetic rotation tests: no real passport images or identity information."""
import pytest
from PIL import Image, ImageDraw
from app import extraction


def ocr_data(text, confidence):
    return {
        "text": text.split(), "conf": [str(confidence)] * len(text.split()),
        "block_num": [1] * len(text.split()),
        "par_num": [1] * len(text.split()),
        "line_num": [1] * len(text.split()),
        "left": [10] * len(text.split()),
        "top": [20] * len(text.split()),
        "width": [30] * len(text.split()),
        "height": [20] * len(text.split()),
    }


def synthetic_page():
    image = Image.new("RGB", (400, 240), "white")
    ImageDraw.Draw(image).rectangle((10, 10, 55, 45), fill="red")
    return image


def upright_marker(image):
    if image.mode != "RGB":
        return False
    # The marker identifies the synthetic page's upright orientation.
    corners = ((20, 20), (image.width - 20, 20),
               (20, image.height - 20), (image.width - 20, image.height - 20))
    return image.getpixel(corners[0])[0] > 180 and image.getpixel(corners[0])[1] < 80


@pytest.mark.parametrize("angle", [0, 90, 180, 270])
def test_passport_rotation_candidates_use_same_original(monkeypatch, angle):
    monkeypatch.setattr(
        extraction.pytesseract, "image_to_osd",
        lambda *args, **kwargs: (_ for _ in ()).throw(RuntimeError("no OSD")),
    )
    calls = []
    def fake_data(image, **kwargs):
        calls.append((image.size, image.mode))
        if upright_marker(image):
            return ocr_data("PASSPORT SURNAME GIVEN BIRTH EXPIRY", 82)
        return ocr_data("UNREADABLE TEXT", 60)
    monkeypatch.setattr(extraction.pytesseract, "image_to_data", fake_data)
    image = synthetic_page().rotate(-angle, expand=True)
    page = extraction.ocr(image, 1)
    assert "PASSPORT" in page.text
    assert page.rotationDegrees == ((360 - angle) % 360)
    assert page.extractionConfidence == pytest.approx(0.82)
    if angle:
        assert len(calls) == 5  # initial, cleanup, three independent rotations
    else:
        assert len(calls) == 5
