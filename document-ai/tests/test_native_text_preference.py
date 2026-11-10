"""Regression checks for readable native text on image-bearing immigration PDFs."""
from app.extraction import prefer_native_pdf_text


def test_epass_with_portrait_and_qr_prefers_native_text():
    text = (
        "Electronic Pass ePASS Government of Malaysia "
        "MALAYSIA IMMIGRATION MULTIPLE ENTRY VISA "
        "EMPLOYMENT PASS Category (I) "
        "ePass Holder Information Name: SAMPLE PERSON Gender: MALE "
        "Passport No: A1234567 Nationality: INDIA "
        "Date Of Issue: 07 APR 2025 Place Of Issue: MDEC "
    )
    assert prefer_native_pdf_text(text, has_images=True)


def test_scanned_pdf_with_only_searchable_heading_uses_ocr():
    assert not prefer_native_pdf_text("Electronic Pass ePASS Government of Malaysia", has_images=True)


def test_text_only_pdf_prefers_native_text():
    assert prefer_native_pdf_text("Invoice number 1234 due on 1 April 2026.", has_images=False)


def test_image_pdf_without_text_uses_ocr():
    assert not prefer_native_pdf_text("  ", has_images=True)


def test_multicountry_native_text_without_english_visa_keywords():
    samples = (
        "Republic of India\\n" + "Full Name Document Reference Date of Birth Issuing Office " * 4,
        "Singapore\\n" + "Holder Identification Validity Employer Issued At Conditions " * 4,
        "United Arab Emirates\\n" + "Residence Permit Holder Sponsor Issue Expiry Reference " * 4,
        "China\\n" + "中华人民共和国 护照 签发日期 有效期至 姓名 性别 国籍 证件号码 " * 4,
        "Vanuatu\\n" + "Department of Immigration Permit Holder Date Issued Valid Until " * 4,
    )
    for sample in samples:
        assert prefer_native_pdf_text(sample, has_images=True)


def test_short_image_bearing_text_layer_remains_ocr_candidate():
    assert not prefer_native_pdf_text("Name: A\\nCountry: B", has_images=True)


def test_garbled_native_layer_is_not_trusted_even_if_long():
    from app.extraction import corrupted_text_layer
    garbage = ("0\\ 3: V1 1-1. J n A: x:: Vol m. 0 en -0 ~ N ~ 3: " * 12)
    assert corrupted_text_layer(garbage)
    assert not prefer_native_pdf_text(garbage, has_images=True)
    assert not prefer_native_pdf_text(garbage, has_images=False)


def test_legitimate_passport_and_cjk_native_text_are_retained():
    from app.extraction import corrupted_text_layer
    passport = ("PASSPORT REPUBLIC OF EXAMPLE\\nName: SAMPLE PERSON\\n"
                "Date of Birth: 09 NOV 1983\\nPassport No: X1234567\\n"
                "Place of Issue: EXAMPLE CITY\\nDate of Expiry: 22 DEC 2031\\n")
    assert not corrupted_text_layer(passport)
    assert prefer_native_pdf_text(passport, has_images=True)
    chinese = "中华人民共和国 护照 签发日期 有效期至 姓名 性别 国籍 证件号码 " * 5
    assert not corrupted_text_layer(chinese)
    assert prefer_native_pdf_text(chinese, has_images=True)
