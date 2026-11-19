from types import SimpleNamespace
import pytest
from app.immigration_review import immigration_review

@pytest.mark.parametrize("title,condition,expected", [
    ("EMPLOYMENT PASS [Reg.9] Category (I)",
     ":PERMITTEDTOENTERANDREMAININWESTMALAYSIA\nUNTIL30MAR2030.FOREMPLOYMENTASANALYST\nWith\n:EXAMPLE COMPANY",
     "Employment Pass"),
    ("DEPENDANT'S PASS [Reg.10]",
     "UNTIL\n30MAR2030\nANYFORMOFEMPLOYMENTISSTRICTLYPROHIBITED.",
     "Dependant Pass"),
])
def test_issued_pass_multiline(title, condition, expected):
    source = ("Electronic Pass\nMULTIPLE ENTRY VISA\n"
              "Good for any number of journeys to Malaysia until 31 Dec 2029\n"
              + title + "\nName\n:EXAMPLE PERSON\nPassport No\n:X1234567\nCondition\n"
              + condition + "\nDateOfIssue\n:07APR2028\n")
    page = SimpleNamespace(page=1, text=source, extractionConfidence=.97)
    semantic, fields = immigration_review([page])
    assert semantic.documentCategory == expected
    values = {f["field"]: f["value"] for f in fields}
    assert values["expiry_date"] == "30MAR2030"
    assert values["issue_date"] == "07APR2028"
    assert all(f["requiresConfirmation"] for f in fields)
    assert all(f["sourceText"] in source for f in fields)
    assert all(s.supportStatus == "UNCERTAIN" for s in semantic.statements)
    # An invalid calendar date must not become a review date.
    page.text = source.replace("30MAR2030", "31FEB2030")
    assert immigration_review([page]) is None
