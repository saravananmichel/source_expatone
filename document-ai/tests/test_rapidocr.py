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
    if result.returncode != 0:
        # Synthetic fixture only: expose full inference exception for diagnosis.
        probe = """
import resource, sys
from app.extraction import extract
limit = int(sys.argv[1]) * 1024**3
resource.setrlimit(resource.RLIMIT_AS, (limit, limit))
pages = extract(sys.stdin.buffer.read(), 'image/png')
assert 'PASSPORT' in pages[0].text.upper()
"""
        diagnostics = []
        for limit in (1, 4):
            comparison = subprocess.run(
                [sys.executable, '-c', probe, str(limit)],
                input=buf.getvalue(), capture_output=True, timeout=120)
            diagnostics.append(
                f'{limit} GiB returncode={comparison.returncode}: '
                + comparison.stderr.decode(errors='replace'))
        pytest.fail(result.stderr.decode(errors='replace') + '\n'.join(diagnostics))

    pages = json.loads(result.stdout)
    assert 'PASSPORT' in pages[0]['text'].upper()
    assert pages[0]['blocks']


@pytest.mark.parametrize("angle", [90, 180, 270])
def test_rapid_page_orientation_osd(monkeypatch, angle):
    monkeypatch.setattr(extraction.pytesseract, 'image_to_osd',
                        lambda *a, **k: {'orientation_conf': 10, 'rotate': angle})
    def engine(image):
        assert image.shape[:2] == (100, 200)
        return [
            ([[20, 60], [100, 60], [100, 80], [20, 80]], 'PASSPORT', .99),
            ([[10, 10], [90, 10], [90, 30], [10, 30]], 'SURNAME', .99),
        ], None
    monkeypatch.setattr(extraction, '_rapid_engine', lambda: engine)
    source = Image.new('RGB', (200, 100)).rotate(angle, expand=True)
    page = extraction.rapid_ocr(source, 1)
    assert page.rotationDegrees == angle
    assert (page.width, page.height) == (200, 100)
    assert page.text.splitlines() == ['SURNAME', 'PASSPORT']
    assert list(page.blocks[0].boundingBox) == [10, 10, 90, 30]


def test_rapid_sideways_retry_without_osd(monkeypatch):
    monkeypatch.setattr(extraction.pytesseract, 'image_to_osd',
                        lambda *a, **k: {'orientation_conf': 0})
    def engine(image):
        vertical = image.shape[0] > image.shape[1]
        result = []
        for i, label in enumerate(['SURNAME', 'GIVEN NAME', 'PASSPORT']):
            x, y = (10 + i*20, 10) if vertical else (10, 10 + i*20)
            w, h = (10, 80) if vertical else (80, 10)
            result.append(([[x,y],[x+w,y],[x+w,y+h],[x,y+h]], label, .9))
        return result, None
    monkeypatch.setattr(extraction, '_rapid_engine', lambda: engine)
    page = extraction.rapid_ocr(Image.new('RGB', (100, 200)), 1)
    assert page.rotationDegrees in (90, 270)
    assert (page.width, page.height) == (200, 100)
    assert page.text.splitlines() == ['SURNAME', 'GIVEN NAME', 'PASSPORT']
