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


def test_real_rapidocr_worker(monkeypatch):
    import io
    import json
    import subprocess
    import sys
    from PIL import ImageDraw, ImageFont
    monkeypatch.setenv('DOCUMENT_OCR_ENGINE', 'rapidocr')
    monkeypatch.setenv('RAPIDOCR_FALLBACK', '0')
    image = Image.new('RGB', (1000, 300), 'white')
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf', 64)
    draw.text((40, 50), 'PASSPORT SAMPLE', font=font, fill='black')
    buf = io.BytesIO()
    image.save(buf, format='PNG')
    result = subprocess.run([sys.executable, '-m', 'app.extract_worker', 'image/png'],
                            input=buf.getvalue(), capture_output=True, timeout=120)
    assert result.returncode == 0, result.stderr.decode(errors='replace')
    pages = json.loads(result.stdout)
    assert 'PASSPORT' in pages[0]['text'].upper()
    assert pages[0]['blocks']
