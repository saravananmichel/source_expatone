from types import SimpleNamespace
from app.general_classification import general_classification
from app.semantic_candidates import semantic_candidates
from app.review_summary import review_summary
from app.structured_analysis import passport_reverse_fields
from app.passport_mrz import mrz_fields, check_digit


def page(text, number=1, confidence=.97):
    return SimpleNamespace(text=text, page=number, extractionConfidence=confidence)


def test_compact_designation_heading_and_fields():
    p = page("Subject-Change in Designation.\nYour designation has been changed toModuleLead\nwitheffectfrom1stJune2010")
    route = general_classification([p])
    assert route.subtype == "designation_change"
    fields = semantic_candidates([p])
    values = {f["field"]: f["value"] for f in fields}
    assert values["revised_designation"] == "ModuleLead"
    assert values["effective_date"] == "1stJune2010"
    assert all(f["sourceText"] in p.text and f["requiresConfirmation"] for f in fields)
    assert "ModuleLead" in review_summary(route, fields, [p])


def test_invalid_compact_date_is_not_accepted():
    assert not semantic_candidates([page("witheffectfrom31stFebruary2025")])


def test_compact_heading_does_not_match_arbitrary_sentence():
    assert general_classification([page("This is not an OfferLetter")]) is None


def test_bilingual_reverse_page_keeps_evidence():
    p = page("Label/NameofMother\nEXAMPLE PERSON\nLabel/NameofSpouse\nANOTHER PERSON", 2)
    fields = passport_reverse_fields([page("Identity"), p])
    assert {f["field"] for f in fields} == {"mother_name", "spouse_name"}
    assert all(f["sourceText"] in p.text and f["requiresConfirmation"] for f in fields)


def mrz(number, birth="900101"):
    first = "P<UTO" + "EXAMPLE<<TEST".ljust(39, "<")
    expiry = "300101"
    second = number + check_digit(number) + "UTO" + birth + check_digit(birth) + "M" + expiry + check_digit(expiry) + "<"*14 + "0"
    return first + "\n" + second + check_digit(second[:10] + second[13:20] + second[21:43])


def test_mrz_whitespace_and_conflicting_identity():
    raw = mrz("X12345678")
    spaced = raw.replace("X12345678", "X123 45678")
    fields = mrz_fields([page(spaced)])
    assert len(fields) == 1
    assert fields[0]["value"] == "X12345678"
    assert fields[0]["sourceText"] in spaced
    assert not mrz_fields([page(raw + "\n" + mrz("X87654321"))])


def test_mrz_rejects_invalid_calendar_date_even_with_checksum():
    assert not mrz_fields([page(mrz("X12345678", "900231"))])


def test_summary_omits_conflicting_designations():
    p = page("New role: Analyst\nNew role: Manager")
    fields = [{"field": "revised_designation", "value": v,
               "sourceText": "New role: " + v, "page": 1}
              for v in ("Analyst", "Manager")]
    route = SimpleNamespace(subtype="designation_change")
    summary = review_summary(route, fields, [p])
    assert "Analyst" not in summary and "Manager" not in summary
