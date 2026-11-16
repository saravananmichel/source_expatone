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


def test_compact_ocr_labels_preserve_spatial_date_columns():
    blocks = [
        block("REPUBLIC OFINDIA", 10, 10, 200),
        block("text/Suname", 20, 50),
        block("EXAMPLE", 20, 72),
        block("text/GivenName(s)", 20, 110, 160),
        block("SAMPLE", 20, 132),
        block("text/DateofBirth", 20, 170, 150),
        block("01/02/1980", 20, 192),
        block("text/DateofExpiry", 350, 250, 150),
        block("text/Dateoflssue", 20, 250, 150),
        block("01/02/2030", 350, 272),
        block("02/02/2020", 20, 272),
    ]
    from app.structured_analysis import _passport_spatial_fields
    fields = _passport_spatial_fields(make_page(blocks))
    values = {(f["field"], f["value"]) for f in fields}
    assert ("surname", "EXAMPLE") in values
    assert ("given_names", "SAMPLE") in values
    assert ("date_of_birth", "01/02/1980") in values
    assert ("expiry_date", "01/02/2030") in values
    assert ("issue_date", "02/02/2020") in values
    assert all(f["value"] in f["sourceText"] and f["requiresConfirmation"] for f in fields)
    assert ("issue_date", "01/02/2030") not in values
    assert ("expiry_date", "02/02/2020") not in values


def test_compact_passport_category_without_country_header():
    from app.structured_analysis import passport_category, _passport_spatial_fields
    blocks = [
        block("Suname", 20, 50),
        block("EXAMPLE", 20, 72),
        block("GivenName(s)", 20, 110, 160),
        block("SAMPLE", 20, 132),
        block("DateofBirth", 20, 170, 150),
        block("01/02/1980", 20, 192),
        block("Nationality", 350, 110, 150),
        block("EXAMPLELAND", 350, 132),
        block("DateofExpiry", 350, 250, 150),
        block("Dateoflssue", 20, 250, 150),
        block("01/02/2030", 350, 272),
        block("02/02/2020", 20, 272),
    ]
    page = make_page(blocks)
    fields = _passport_spatial_fields(page)
    assert passport_category([page], fields)
    visa = make_page([block("Employment Pass", 10, 10, 200)] + blocks)
    assert not passport_category([visa], fields)
    assert not _passport_spatial_fields(visa)
