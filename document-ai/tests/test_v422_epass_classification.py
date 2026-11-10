"""Classification regression: ePASS mentions passport numbers but is not a passport."""
from app.pipeline import validate_grounding
from app.schemas import Page, SemanticDocument, Statement, Evidence


def _classify(text, category="Passport", confidence=0.91):
    page = Page(page=1, text=text, width=100, height=100, extraction="native", blocks=[])
    evidence = Evidence(id="e0", page=1, sourceText=text)
    statement = Statement(id="s0", kind="fact", label="Document text",
                          text="Document text: " + text + ".",
                          originalValue=text, confidence=0.9, evidenceIds=["e0"])
    semantic = SemanticDocument(documentCategory=category,
                                classificationConfidence=confidence,
                                classificationEvidenceIds=["e0"],
                                statements=[statement], evidence=[evidence])
    return validate_grounding(semantic, [page])


def test_malaysian_employment_epass_is_not_passport():
    text = "MALAYSIA IMMIGRATION Electronic Pass ePASS EMPLOYMENT PASS [Reg.9] Passport No: V5073795"
    result = _classify(text)
    assert result.documentCategory == "Employment Pass"


def test_generic_immigration_epass_not_passport():
    result = _classify("ePASS Government of Malaysia IMMIGRATION Passport No: ABC123")
    assert result.documentCategory == "Immigration Document"


def test_genuine_passport_stays_passport():
    result = _classify("PASSPORT Republic of India Passport No: ABC123 Date of Issue: 01 Jan 2020")
    assert result.documentCategory == "Passport"


def test_low_confidence_remains_unknown():
    result = _classify("ePASS EMPLOYMENT PASS Passport No: ABC123", confidence=0.4)
    assert result.documentCategory == "Unknown"
