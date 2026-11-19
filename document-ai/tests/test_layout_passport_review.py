from app.schemas import Page, Block
from app.structured_analysis import structured_fields, passport_category, _passport_layout_number
from app.passport_fast_path import eligible_passport, passport_review_result


def layout(extra=None, title="REPUBLIC OFINDIA"):
    rows = [
        (title, 10, 10, 300, 18),
        ("x/Type", 50, 60, 60, 18),
        ("x/Code", 180, 60, 60, 18),
        ("x/Nationality", 320, 60, 120, 18),
        ("P", 55, 82, 15, 18),
        ("IND", 185, 82, 40, 18),
        ("OCR/INDIAN", 320, 75, 110, 18),
        ("A1234567", 490, 82, 120, 18),
        ("x/Suname", 50, 140, 100, 18),
        ("EXAMPLE", 50, 162, 100, 18),
        ("x/GivenName(s)", 50, 210, 150, 18),
        ("SAMPLE", 50, 232, 100, 18),
        ("x/DateofBirth", 50, 280, 150, 18),
        ("01/02/1980", 50, 302, 100, 18),
        ("x/PlaceofBinh", 50, 350, 150, 18),
        ("SAMPLE CITY", 50, 363, 120, 18),
        ("x/DateofExpiry", 450, 420, 150, 18),
        ("x/Dateoflssue", 50, 435, 150, 18),
        ("01/02/2030", 450, 438, 110, 18),
        ("02/02/2020", 50, 457, 110, 18),
    ] + (extra or [])
    blocks = [Block(text=t, boundingBox=[x,y,x+w,y+h], kind="paragraph")
              for t,x,y,w,h in rows]
    return Page(page=1, text="\n".join(b.text for b in blocks), width=900,
                height=600, extraction="ocr", extractionConfidence=.9, blocks=blocks)


def test_layout_review_opt_in():
    page = layout()
    fields = structured_fields([page])
    values = {(f["field"], f["value"]) for f in fields}
    assert ("passport_number", "A1234567") in values
    assert ("nationality", "INDIAN") in values
    assert ("place_of_birth", "SAMPLE CITY") in values
    assert ("issue_date", "02/02/2020") in values
    assert passport_category([page], fields)
    assert not eligible_passport([page], fields)
    assert eligible_passport([page], fields, allow_layout=True)
    result = passport_review_result([page], fields, allow_layout=True)
    assert result._quality["supportMethod"] == "source-layout-review-only"
    assert all(s.supportStatus == "UNCERTAIN" for s in result.statements)


def test_two_header_numbers_rejected():
    page = layout([("B1234567", 650, 82, 120, 18)])
    assert not _passport_layout_number(page)
    assert not eligible_passport([page], structured_fields([page]), allow_layout=True)


def test_employment_pass_layout_rejected():
    page = layout(title="Employment Pass")
    assert not eligible_passport([page], structured_fields([page]), allow_layout=True)


def test_low_confidence_and_conflicting_dates_rejected():
    page = layout()
    fields = structured_fields([page])
    page.extractionConfidence = .79
    assert not eligible_passport([page], fields, allow_layout=True)
    page.extractionConfidence = .9
    fields.append(dict(field="issue_date", value="03/02/2020",
                       sourceText="03/02/2020", page=1, requiresConfirmation=True))
    assert not eligible_passport([page], fields, allow_layout=True)


def test_reverse_page_not_number_source():
    page = layout()
    page.page = 2
    assert not _passport_layout_number(page)
