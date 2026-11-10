"""Fallback must not invent field associations from PDF reading order."""
from app.private_fallback import _source_fields


def test_unicode_field_label_is_accepted():
    fields = list(_source_fields("姓名: TEST PERSON"))
    assert fields and fields[0][0] == "姓名"
    assert fields[0][1] == "TEST PERSON"


def test_disconnected_labels_are_not_assigned_values():
    text = "Name\nGender\nPassport No\nSAMPLE USER\nMALE\nA12345678"
    assert list(_source_fields(text)) == []


def test_explicit_colon_pair_is_retained():
    fields = list(_source_fields("Passport No: A12345678\nName: SAMPLE USER"))
    assert ("Passport No", "A12345678", "Passport No: A12345678") in fields


def test_repetitive_ocr_line_is_not_promoted_to_supported_field():
    text = "Name: SAMPLE USER\n" + ("JIM" * 25) + ": GARBLED\nPassport No: A12345678"
    fields = list(_source_fields(text))
    assert len(fields) == 2
    assert [field[0] for field in fields] == ["Name", "Passport No"]
