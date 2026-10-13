"""Source-level regression checks for bounded independent text extraction.

These tests intentionally require no OCR engine or third-party Python packages.
They do not replace runtime tests using real PDFs and images.
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EXTRACTION = (ROOT / "app" / "extraction.py").read_text()
WORKER = (ROOT / "app" / "extract_worker.py").read_text()
MAIN = (ROOT / "app" / "main.py").read_text()


def test_pdf_page_limit_is_configured():
    assert "MAX_PAGES = 40" in EXTRACTION
    assert "len(reader.pages) <= MAX_PAGES" in EXTRACTION


def test_text_limit_applies_after_both_pdf_and_image_paths():
    assert "MAX_TEXT = 100_000" in EXTRACTION
    assert "sum(len(page.text) for page in pages) > MAX_TEXT" in EXTRACTION


def test_pdf_render_dimensions_are_bounded_at_scale_two():
    assert "math.ceil(page.get_width() * 2)" in EXTRACTION
    assert "math.ceil(page.get_height() * 2)" in EXTRACTION
    assert "render_width * render_height > 20_000_000" in EXTRACTION


def test_parser_checks_pdf_and_image_signatures():
    assert "b'%PDF-'" in EXTRACTION
    assert "image/png" in EXTRACTION
    assert "image/jpeg" in EXTRACTION


def test_worker_caps_input_and_serialized_output():
    assert "10*1024*1024+1" in WORKER
    assert "MAX_OUTPUT_BYTES = 8 * 1024 * 1024" in WORKER
    assert "len(output) > MAX_OUTPUT_BYTES" in WORKER
    assert "sys.stdout.buffer.write(output)" in WORKER


def test_api_extraction_endpoint_requires_service_key():
    assert "@app.post('/extract')" in MAIN
    assert "hmac.compare_digest(x_service_key, expected)" in MAIN
    assert "Extraction service busy" in MAIN


def test_extraction_is_separate_from_analysis_endpoint():
    assert "'/extract'" in MAIN
    assert "'/analyze'" in MAIN
    assert "app.extract_worker" in MAIN
