"""Synthetic OCR geometry tests for conservative passport extraction."""
from app.schemas import Page, Block
from app.structured_analysis import structured_fields


def make_page(blocks):
    return Page(page=1, text="\n".join(b.text for b in blocks), width=900,
                height=600, extraction="ocr", blocks=blocks)


def block(text, x, y, width=100):
    return Block(text=text, boundingBox=[x, y, x + width, y + 18], kind="paragraph")


def test_spatial_date_and_passport_number_from_broken_reading_order():
    blocks = [
        block("REPUBLIC OF | INDIA", 10, 10, 200),
        block("Passport No", 20, 80),
        block("Date of Expiry", 250, 80, 120),
        block("A1234567", 20, 105),
        block("01/02/2030", 250, 105),
    ]
    values = {(f["field"], f["value"]) for f in structured_fields([make_page(blocks)])}
    assert ("passport_number", "A1234567") in values
    assert ("expiry_date", "01/02/2030") in values


def test_spatial_ambiguity_does_not_choose_passport_number():
    blocks = [
        block("REPUBLIC OF | INDIA", 10, 10, 200),
        block("Passport No", 20, 80),
        block("A1234567", 20, 105),
        block("B1234567", 20, 123),
    ]
    values = {(f["field"], f["value"]) for f in structured_fields([make_page(blocks)])}
    # A sequential neighbour can be independently grounded; the spatial
    # candidate selector itself must never select one of two plausible values.
    from app.structured_analysis import _passport_spatial_fields
    assert not any(f["field"] == "passport_number"
                   for f in _passport_spatial_fields(make_page(blocks)))
