from PIL import Image
import pytest
from app import extraction

def test_rapid_blocks(monkeypatch):
    monkeypatch.setattr(extraction, '_rapid_engine', lambda: lambda image: ([
        ([[10, 10], [90, 10], [90, 25], [10, 25]], 'PASSPORT', 0.99),
        ([[10, 30], [90, 30], [90, 45], [10, 45]], 'noise', 0.1),
    ], None))
    page = extraction.rapid_ocr(Image.new('RGB', (100, 100)), 1)
    assert page.text == 'PASSPORT'
    assert list(page.blocks[0].boundingBox) == [10, 10, 90, 25]

def test_default_route(monkeypatch):
    monkeypatch.delenv('DOCUMENT_OCR_ENGINE', raising=False)
    monkeypatch.setattr(extraction, '_tesseract_ocr', lambda *args: 'legacy')
    assert extraction.ocr(None, 1) == 'legacy'

def test_fallback_opt_in(monkeypatch):
    monkeypatch.setenv('DOCUMENT_OCR_ENGINE', 'rapidocr')
    def fail(*args):
        raise ValueError('no_readable_text')
    monkeypatch.setattr(extraction, 'rapid_ocr', fail)
    monkeypatch.setattr(extraction, '_tesseract_ocr', lambda *args: 'legacy')
    monkeypatch.setenv('RAPIDOCR_FALLBACK', '0')
    with pytest.raises(ValueError):
        extraction.ocr(None, 1)
    monkeypatch.setenv('RAPIDOCR_FALLBACK', '1')
    assert extraction.ocr(None, 1) == 'legacy'
