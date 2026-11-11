"""Synthetic ICAO TD3 fixtures. Never use real passport details."""
from app.schemas import Page
from app.passport_mrz import check_digit, mrz_fields
from app.structured_analysis import structured_fields


def synthetic_mrz():
    first = "P<UTO" + "SAMPLE<<TEST<PERSON".ljust(39, "<")
    number = "X1234567<"
    birth = "900101"
    expiry = "300101"
    optional = "<" * 14
    second = (number + check_digit(number) + "UTO" + birth + check_digit(birth)
              + "M" + expiry + check_digit(expiry) + optional + check_digit(optional))
    second += check_digit(second[:10] + second[13:20] + second[21:43])
    assert len(first) == len(second) == 44
    return first, second


def page(text, number=1):
    return Page(page=number, text=text, width=100, height=100, extraction="native", blocks=[])


def test_valid_mrz_number_is_review_only_and_source_grounded():
    first, second = synthetic_mrz()
    fields = structured_fields([page(first + "\n" + second)])
    match = [field for field in fields if field["field"] == "passport_number"]
    assert len(match) == 1
    assert match[0]["value"] == "X1234567"
    assert match[0]["requiresConfirmation"]
    assert match[0]["value"] in match[0]["sourceText"]


def test_corrupted_mrz_checksum_does_not_extract():
    first, second = synthetic_mrz()
    second = second[:9] + ("0" if second[9] != "0" else "1") + second[10:]
    assert mrz_fields([page(first + "\n" + second)]) == []


def test_fragmented_mrz_does_not_guess():
    first, second = synthetic_mrz()
    assert mrz_fields([page(first + "\n" + second[:20] + "\n" + second[20:])]) == []


def test_historical_second_page_mrz_not_promoted():
    first, second = synthetic_mrz()
    fields = structured_fields([page("PASSPORT\nDate of Birth: 01/01/1990"),
                                page("Old Passport\n" + first + "\n" + second, 2)])
    assert not any(field["field"] == "passport_number" for field in fields)
