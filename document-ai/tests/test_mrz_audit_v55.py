"""Synthetic-only tests for V5.5 MRZ audit."""
from app.mrz_audit_v55 import audit_td3
from app.passport_mrz import check_digit
from app.schemas import Page


def page(text, number=1):
    return Page(page=number, text=text, width=100, height=100,
                extraction="native", blocks=[])


def fixture():
    first = "P<UTO" + "SAMPLE<<TEST<PERSON".ljust(39, "<")
    number, birth, expiry, optional = "X1234567<", "900101", "300101", "<" * 14
    second = (number + check_digit(number) + "UTO" + birth
              + check_digit(birth) + "M" + expiry
              + check_digit(expiry) + optional + check_digit(optional))
    second += check_digit(second[:10] + second[13:20] + second[21:43])
    assert len(first) == len(second) == 44
    return first, second


def test_valid_mrz_is_review_only_without_century_inference():
    first, second = fixture()
    result = audit_td3([page(first + "\n" + second)])
    assert result["status"] == "valid_review_only"
    assert result["passportNumber"] == "X1234567"
    assert result["birthYYMMDD"] == "900101"
    assert result["expiryYYMMDD"] == "300101"
    assert result["datesCenturyResolved"] is False
    assert result["requiresConfirmation"] is True


def test_invalid_checksum_is_not_accepted():
    first, second = fixture()
    second = second[:9] + ("0" if second[9] != "0" else "1") + second[10:]
    result = audit_td3([page(first + "\n" + second)])
    assert result["status"] == "invalid_checksum"
    assert "passportNumber" not in result


def test_fragmented_or_second_page_mrz_is_not_promoted():
    first, second = fixture()
    assert audit_td3([page(first + "\n" + second[:20] + "\n" + second[20:])])["status"] == "not_found"
    assert audit_td3([page("header"), page(first + "\n" + second, 2)])["status"] == "not_found"
