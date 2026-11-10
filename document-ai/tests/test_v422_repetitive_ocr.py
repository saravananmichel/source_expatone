from app.extraction import repetitive_ocr_noise, corrupted_text_layer, prefer_native_pdf_text


def test_repeated_jim_watermark_rejected_as_native_text():
    text = "MALAYSIA IMMIGRATION EMPLOYMENT PASS " + ("JIM" * 25) + " Name: EXAMPLE PERSON"
    assert repetitive_ocr_noise(text)
    assert corrupted_text_layer(text)
    assert not prefer_native_pdf_text(text, has_images=True)


def test_legitimate_identifiers_not_rejected():
    text = ("MALAYSIA IMMIGRATION DEPARTMENT\\n"
            "ePASS EMPLOYMENT PASS\\n"
            "Passport No: A12345678\\n"
            "Pass No: EP2026000123\\n"
            "Issue Date: 11/11/2026\\n"
            "Valid Until: 11/11/2028")
    assert not repetitive_ocr_noise(text)
    assert not corrupted_text_layer(text)


def test_short_repetition_is_not_a_corruption_signal():
    assert not repetitive_ocr_noise("AAAAAA")
    assert not repetitive_ocr_noise("ABCABC")
