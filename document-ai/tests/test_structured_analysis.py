"""Synthetic regression tests: no personal documents or external services."""
from app.schemas import Page
from app.structured_analysis import structured_fields, billing_category

def page(text):
    return Page(page=1, text=text, width=100, height=100, extraction="native", blocks=[])

def test_bilingual_bill_fields_are_grounded():
    pages = [page("Bil Internet\nNo. Akaun: 000123456\nJumlah Perlu Dibayar: RM 1,250.60\nTarikh Akhir Bayaran: 25/11/2026")]
    fields = structured_fields(pages)
    values = {f["field"]: f["value"] for f in fields}
    assert values["account_number"] == "000123456"
    assert values["amount_due"] == "RM 1,250.60"
    assert values["due_date"] == "25/11/2026"
    assert billing_category(pages, fields)
    assert all(f["requiresConfirmation"] and f["value"] in f["sourceText"] for f in fields)

def test_passport_labels_not_unrelated_numbers():
    pages = [page("PASSPORT\nPassport No: A12345678\nDate of Birth: 02/03/1990\nDate of Expiry: 01/12/2030")]
    values = {f["field"]: f["value"] for f in structured_fields(pages)}
    assert values == {"passport_number":"A12345678","date_of_birth":"02/03/1990","expiry_date":"01/12/2030"}

def test_no_invention_without_labels():
    pages = [page("Apple Billing Support - https://apple.com/bill\nService Tax ID: W10-1808-32000489")]
    assert structured_fields(pages) == []
    assert not billing_category(pages, [])

def test_bill_title_alone_does_not_override_category():
    pages = [page("Invoice\nSupport: help.example.test")]
    assert not billing_category(pages, structured_fields(pages))


def test_passport_adjacent_label_and_value():
    fields = structured_fields([page("PASSPORT\nPassport No\nK1234567\nDate of Birth\n09/11/1983")])
    values = {f["field"]: f["value"] for f in fields}
    assert values["passport_number"] == "K1234567"
    assert values["date_of_birth"] == "09/11/1983"
    assert all(f["value"] in f["sourceText"] and f["requiresConfirmation"] for f in fields)


def test_combined_issue_and_expiry_are_separate():
    fields = structured_fields([page("Date of Issue / Date of Expiry 23/12/2021 22/12/2031")])
    values = {f["field"]: f["value"] for f in fields}
    assert values["issue_date"] == "23/12/2021"
    assert values["expiry_date"] == "22/12/2031"


def test_unlabelled_or_distant_numbers_are_not_passport_numbers():
    fields = structured_fields([page("PASSPORT\nSome unrelated heading\n33726657\nOld Passport\n33726657")])
    assert not any(f["field"] == "passport_number" for f in fields)


def test_passport_ocr_paired_dates_on_following_line():
    fields = structured_fields([page("Date of Issue / Date of Expiry\n01/02/2020 31/01/2030")])
    values = {f["field"]: f["value"] for f in fields}
    assert values["issue_date"] == "01/02/2020"
    assert values["expiry_date"] == "31/01/2030"
    assert all(f["value"] in f["sourceText"] for f in fields)


def test_passport_ocr_birth_date_with_sex_suffix():
    fields = structured_fields([page("Date of Birth\n02/03/1990 M")])
    assert any(f["field"] == "date_of_birth" and f["value"] == "02/03/1990" for f in fields)


def test_old_passport_number_never_promoted_as_current():
    for text in (
        "Old Passport No: Z0000000",
        "Old Passport No\nZ0000000",
        "Old Passport\nNo. with Date and Place of issue\nZ0000000 01/02/2010",
    ):
        fields = structured_fields([page(text)])
        assert not any(f["field"] == "passport_number" for f in fields)


def test_current_passport_number_and_historical_number_disambiguated():
    pages = [
        page("PASSPORT\nPassport No\nK1234567"),
        Page(page=2, text="Old Passport No: Z0000000", width=100,
             height=100, extraction="native", blocks=[]),
    ]
    fields = structured_fields(pages)
    numbers = [f["value"] for f in fields if f["field"] == "passport_number"]
    assert numbers == ["K1234567"]


def test_passport_identity_fields_only_when_grounded():
    from app.structured_analysis import passport_category
    pages = [page("REPUBLIC OF INDIA\nPASSPORT\nSurname\nSAMPLE\nGiven Names\nTEST USER\nNationality\nINDIAN\nSex\nM\nPlace of Birth\nTEST CITY\nPlace of Issue\nTEST OFFICE\nDate of Birth\n02/03/1990\nDate of Issue\n01/02/2020\nDate of Expiry\n31/01/2030")]
    fields = structured_fields(pages)
    values = {f["field"]: f["value"] for f in fields}
    assert values["surname"] == "SAMPLE"
    assert values["given_names"] == "TEST USER"
    assert values["nationality"] == "INDIAN"
    assert values["sex"] == "M"
    assert values["place_of_birth"] == "TEST CITY"
    assert values["place_of_issue"] == "TEST OFFICE"
    assert passport_category(pages, fields)
    assert all(f["requiresConfirmation"] and f["value"] in f["sourceText"] for f in fields)


def test_passport_identity_fields_reject_unrelated_lines():
    fields = structured_fields([page("PASSPORT\nSurname\nDate of Birth\nNationality\nPassport No\nOld Passport No: Z0000000")])
    assert not any(f["field"] in ("surname", "nationality", "passport_number") for f in fields)


def test_passport_hint_does_not_override_employment_pass():
    from app.structured_analysis import passport_category
    pages = [page("Employment Pass\nPassport No: K1234567\nDate of Birth: 02/03/1990\nDate of Expiry: 01/12/2030")]
    assert not passport_category(pages, structured_fields(pages))


def test_no_passport_hint_without_strong_field_evidence():
    from app.structured_analysis import passport_category
    pages = [page("PASSPORT\nRandom administrative text")]
    assert not passport_category(pages, structured_fields(pages))


def test_passport_spatial_ocr_right_hand_field():
    from app.schemas import Block
    blocks = [
        Block(text="PASSPORT", boundingBox=[10, 5, 100, 20]),
        Block(text="Surname", boundingBox=[10, 40, 90, 60]),
        Block(text="SAMPLE", boundingBox=[110, 40, 180, 60]),
    ]
    scanned = Page(page=1, text="PASSPORT\nSurname\nSAMPLE", width=400, height=200,
                   extraction="ocr", blocks=blocks)
    matches = [f for f in structured_fields([scanned]) if f["field"] == "surname"]
    assert len(matches) >= 1
    assert matches[0]["value"] == "SAMPLE"
    assert matches[0]["value"] in matches[0]["sourceText"]


def test_passport_spatial_ocr_ambiguous_candidates_are_ignored():
    from app.schemas import Block
    blocks = [
        Block(text="PASSPORT", boundingBox=[10, 5, 100, 20]),
        Block(text="Surname", boundingBox=[10, 40, 90, 60]),
        Block(text="SAMPLE", boundingBox=[110, 40, 180, 60]),
        Block(text="OTHER", boundingBox=[115, 41, 185, 61]),
    ]
    scanned = Page(page=1, text="PASSPORT\nSurname\nSAMPLE\nOTHER", width=400, height=200,
                   extraction="ocr", blocks=blocks)
    assert not any(f["field"] == "surname" and f["value"] == "OTHER"
                   for f in structured_fields([scanned]))


def test_passport_json_ocr_bilingual_prefix_labels():
    # Synthetic OCR response shape; no real identity information.
    sample = Page(page=1, text=(
        "REPUBLIC OF EXAMPLE\n"
        "PASSPORT\n"
        "sample text / Surname\n"
        "EXAMPLE\n"
        "untranslated text / Given Name(s)\n"
        "TESTUSER\n"
        "foreign label / Date of Birth\n"
        "02/03/1990 M\n"
        "foreign label / Place of Birth\n"
        "TESTCITY\n"
        "Date of Issue / Date of Expiry\n"
        "01/02/2020 31/01/2030"
    ), width=1200, height=1700, extraction="ocr", blocks=[])
    fields = structured_fields([sample])
    values = {field["field"]: field["value"] for field in fields}
    assert values["surname"] == "EXAMPLE"
    assert values["given_names"] == "TESTUSER"
    assert values["date_of_birth"] == "02/03/1990"
    assert values["place_of_birth"] == "TESTCITY"
    assert values["issue_date"] == "01/02/2020"
    assert values["expiry_date"] == "31/01/2030"
    assert all(field["requiresConfirmation"] and field["value"] in field["sourceText"]
               for field in fields)


def test_passport_json_ocr_does_not_promote_old_passport():
    sample = Page(page=2, text="Old Passport\nNo. with Date and Place of issue\nZ0000000 01/02/2010",
                  width=1200, height=1700, extraction="ocr", blocks=[])
    assert not any(field["field"] == "passport_number" for field in structured_fields([sample]))


def test_passport_v50_sex_from_dob_row_only():
    fields = structured_fields([page("PASSPORT\nDate of Birth\n02/03/1990 F\nPlace of Birth\nTEST CITY")])
    values = {field["field"]: field["value"] for field in fields}
    assert values["sex"] == "F"
    assert values["date_of_birth"] == "02/03/1990"
    assert all(field["requiresConfirmation"] and field["value"] in field["sourceText"]
               for field in fields)


def test_passport_v50_no_unlabelled_sex_guess():
    fields = structured_fields([page("PASSPORT\nF\nDate of Birth\n02/03/1990")])
    assert not any(field["field"] == "sex" for field in fields)


def test_passport_v50_no_reconstruction_from_noisy_number():
    fields = structured_fields([page("PASSPORT\nPassport No\nAN v 5073795")])
    assert not any(field["field"] == "passport_number" for field in fields)
