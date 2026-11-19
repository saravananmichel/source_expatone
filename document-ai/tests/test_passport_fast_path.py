from app.schemas import Page
from app.passport_mrz import check_digit
from app.passport_fast_path import eligible_passport, passport_review_result


def fixture():
    first = "P<XXXEXAMPLE<<SAMPLE".ljust(44, "<")
    number = "A1234567<"
    dob, expiry = "800201", "300201"
    optional = "<" * 14
    second = (number + check_digit(number) + "XXX" + dob + check_digit(dob)
              + "M" + expiry + check_digit(expiry) + optional + check_digit(optional))
    second += check_digit(second[:10] + second[13:20] + second[21:43])
    values = {"passport_number": "A1234567", "surname": "EXAMPLE",
              "given_names": "SAMPLE", "date_of_birth": "01/02/1980",
              "issue_date": "02/02/2020", "expiry_date": "01/02/2030"}
    text = "Passport\nSurname\nGiven names\nDate of birth\n" + "\n".join(values.values()) + "\n" + first + "\n" + second
    page = Page(page=1, text=text, width=900, height=600, extraction="ocr",
                extractionConfidence=0.9, blocks=[])
    fields = [dict(field=k, value=v, sourceText=v, page=1, requiresConfirmation=True)
              for k, v in values.items()]
    return [page], fields


def test_valid_passport_review_only():
    pages, fields = fixture()
    assert eligible_passport(pages, fields)
    semantic = passport_review_result(pages, fields)
    assert semantic.documentCategory == "Passport"
    assert all(s.supportStatus == "UNCERTAIN" for s in semantic.statements)


def test_mrz_date_mismatch_blocks_bypass():
    pages, fields = fixture()
    fields[-1].update(value="02/02/2030", sourceText="02/02/2030")
    assert not eligible_passport(pages, fields)


def test_conflicting_number_blocks_bypass():
    pages, fields = fixture()
    fields.append(dict(field="passport_number", value="B1234567",
                       sourceText="B1234567", page=1, requiresConfirmation=True))
    assert not eligible_passport(pages, fields)


def test_missing_mrz_blocks_bypass():
    pages, fields = fixture()
    pages[0].text = "\n".join(pages[0].text.splitlines()[:-2])
    assert not eligible_passport(pages, fields)
