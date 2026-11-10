from app.pipeline import render
from app.schemas import Page, SemanticDocument, Evidence, Statement


def test_unverified_summary_does_not_dump_raw_evidence():
    secret = "PRIVATE-SOURCE-EXAMPLE-12345"
    evidence = Evidence(id="e0", page=1, sourceText=secret)
    semantic = SemanticDocument(
        documentCategory="Unknown", classificationConfidence=0,
        classificationEvidenceIds=["e0"], evidence=[evidence],
        statements=[Statement(id="s0", kind="fact", label="Extracted source text",
                              text="Extracted source text: " + secret,
                              originalValue=secret, confidence=0.5,
                              evidenceIds=["e0"], supportStatus="UNCERTAIN")])
    page = Page(page=1, text=secret, width=100, height=100,
                extraction="native", blocks=[])
    result = render(semantic, [page], "source-only-private-fallback")
    assert secret not in result["summary"]
    assert "could not be verified" in result["summary"]


def test_supported_source_only_quote_does_not_leak_into_summary():
    secret = "SENSITIVE-OCR-SAMPLE-98765"
    evidence = Evidence(id="e0", page=1, sourceText=secret)
    semantic = SemanticDocument(
        documentCategory="Unknown", classificationConfidence=0,
        classificationEvidenceIds=["e0"], evidence=[evidence],
        statements=[Statement(id="s0", kind="fact", label="Extracted source text",
                              text="Extracted source text: " + secret + ".",
                              originalValue=secret, confidence=0.5,
                              evidenceIds=["e0"], supportStatus="SUPPORTED")])
    page = Page(page=1, text=secret, width=100, height=100,
                extraction="native", blocks=[])
    result = render(semantic, [page], "source-only-private-fallback")
    assert secret not in result["summary"]
    assert result["keyInformation"][0]["value"] == secret
