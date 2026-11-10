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
