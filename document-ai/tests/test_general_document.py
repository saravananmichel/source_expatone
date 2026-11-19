from app.general_document import general_document_hints
from app.schemas import Page


def hints(text, confidence=1):
    return general_document_hints([Page(page=1, text=text, width=600, height=800,
                                 extraction="native", extractionConfidence=confidence, blocks=[])])


def test_document_families():
    samples = [
        ("Electricity Bill\nAccount No: SAMPLE123\nAmount Due: RM 100", "Utility Bill"),
        ("Bank Statement\nAccount Number: SAMPLE123\nStatement Period: January 2030", "Bank Statement"),
        ("Offer Letter\nPosition: Engineer\nStart Date: 01/01/2030", "Offer Letter"),
        ("Employment Contract\nEmployer: SAMPLE COMPANY\nEmployee: SAMPLE PERSON", "Employment Contract"),
        ("Government Letter\nReference Number: SAMPLE123\nLetter Date: 01/01/2030", "Government Letter"),
    ]
    for source, category in samples:
        route, fields = hints(source)
        assert route.category == category
        assert all(f["requiresConfirmation"] and f["sourceText"] in source for f in fields)


def test_abstention():
    assert hints("Bank Statement") == (None, [])
    assert hints("Offer Letter\nPosition: Engineer\nStart Date: 01/01/2030", .5) == (None, [])
    assert hints("Offer Letter\nPosition: Engineer\nPosition: Manager\nStart Date: 01/01/2030") == (None, [])
    assert hints("Offer Letter\nEmployment Contract\nPosition: Engineer\nStart Date: 01/01/2030\nEmployer: SAMPLE\nEmployee: SAMPLE") == (None, [])
