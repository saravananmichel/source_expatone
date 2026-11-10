"""Synthetic fixtures only; never commit personal immigration documents."""
from app.private_fallback import source_fallback
from app.schemas import Page


def test_native_pdf_split_labels_have_short_exact_evidence():
    text = ("Electronic Permit\nHolder Information\nName\n: EXAMPLE PERSON\n"
            "Nationality\n: EXAMPLELAND\nDocument No\n: TEST1234\n"
            "Issue Date\n: 01 JAN 2026\n")
    result = source_fallback([Page(page=1, text=text, width=600, height=800, extraction='native', blocks=[])])
    values = {s.label: s.originalValue for s in result.statements}
    assert values["Name"] == "EXAMPLE PERSON"
    assert values["Nationality"] == "EXAMPLELAND"
    assert values["Document No"] == "TEST1234"
    assert values["Issue Date"] == "01 JAN 2026"
    assert result.documentCategory == "Unknown"
    assert all(len(e.sourceText) < 100 for e in result.evidence)


def test_inline_and_split_colon_formats():
    text = "Holder: SAMPLE USER\nValid Until\n:\n31 DEC 2027\n"
    result = source_fallback([Page(page=1, text=text, width=600, height=800, extraction='native', blocks=[])])
    values = {s.label: s.originalValue for s in result.statements}
    assert values["Holder"] == "SAMPLE USER"
    assert values["Valid Until"] == "31 DEC 2027"
