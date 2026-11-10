"""Offline regression tests: no Ollama or external AI service required."""
from app.adaptive_profile import selected_profile
from app.private_fallback import exact_support, source_fallback
from app.schemas import Page, SemanticDocument, Evidence, Statement


def page(text):
    return Page(page=1, text=text, width=100, height=100,
                extraction='text', blocks=[], extractionConfidence=1)


def test_dev_profile(monkeypatch):
    monkeypatch.setenv('DOCUMENT_AI_PROFILE', 'development')
    p = selected_profile()
    assert p.model == 'qwen3:0.6b'
    assert p.inference_seconds == 120
    assert p.reasoning_enabled is False


def test_fallback_uses_exact_source_only():
    result = source_fallback([page('Employee: Alex Smith\nSalary: RM 5000')])
    assert result.documentCategory == 'Unknown'
    assert [s.originalValue for s in result.statements] == ['Alex Smith', 'RM 5000']
    assert all(s.supportStatus == 'SUPPORTED' for s in result.statements)
    assert all(s.evidenceIds for s in result.statements)


def test_interpretation_never_auto_approved():
    source = 'Notice: 30 days'
    semantic = SemanticDocument(
        documentCategory='Unknown', classificationConfidence=0,
        classificationEvidenceIds=['e0'],
        evidence=[Evidence(id='e0', page=1, sourceText=source)],
        statements=[
            Statement(id='s0', kind='fact', label='Notice',
                      text='Notice: 30 days.', originalValue='30 days',
                      confidence=.9, evidenceIds=['e0']),
            Statement(id='s1', kind='interpretation', label='Right to leave',
                      text='You can resign immediately.', originalValue='30 days',
                      confidence=.9, evidenceIds=['e0']),
        ],
    )
    exact_support(semantic)
    assert semantic.statements[0].supportStatus == 'SUPPORTED'
    assert semantic.statements[1].supportStatus == 'UNCERTAIN'


def test_empty_document_rejected():
    try:
        source_fallback([page('')])
    except ValueError as exc:
        assert str(exc) == 'no_source_text'
    else:
        raise AssertionError('Empty document should not produce invented facts')
