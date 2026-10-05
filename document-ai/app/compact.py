"""Compact inference format; the public SemanticDocument contract is unchanged."""
from pydantic import Field
from .schemas import CATEGORIES, Evidence, SemanticDocument, Statement, StrictModel

StatementKind = Statement.model_fields['kind'].annotation


class CompactStatement(StrictModel):
    kind: StatementKind = Field(alias='k')
    label: str = Field(alias='l', min_length=1, max_length=200)
    text: str = Field(alias='t', min_length=1, max_length=2000)
    original_value: str = Field(alias='v', min_length=1, max_length=4000)
    confidence: float = Field(alias='c', ge=0, le=1)
    evidence_ids: list[str] = Field(alias='e', min_length=1)


class CompactExtraction(StrictModel):
    document_category: CATEGORIES = Field(alias='category')
    classification_confidence: float = Field(alias='confidence', ge=0, le=1)
    classification_evidence_ids: list[str] = Field(alias='refs', min_length=1)
    statements: list[CompactStatement] = Field(alias='items', min_length=1, max_length=300)


def source_passages(pages, max_chars=1000):
    """Assign IDs to exact source slices, preserving all pages and characters.

    Prefer line boundaries. Adjacent passages can be cited together when a
    value or condition crosses a boundary. No model-generated source quotes.
    """
    passages = []
    for page in pages:
        start = 0
        while start < len(page.text):
            end = min(start + max_chars, len(page.text))
            if end < len(page.text):
                boundary = page.text.rfind('\n', start + max_chars // 2, end)
                if boundary >= 0:
                    end = boundary + 1
            passages.append({'id': f'e{len(passages)}', 'page': page.page,
                             'text': page.text[start:end]})
            start = end
    return passages


def expand_extraction(compact, passages):
    """Restore full server-owned IDs and verbatim evidence for existing checks."""
    sources = {p['id']: p for p in passages}
    referenced = list(compact.classification_evidence_ids)
    for item in compact.statements:
        referenced.extend(item.evidence_ids)
    if any(ref not in sources for ref in referenced):
        raise ValueError('missing_evidence')
    evidence = [Evidence(id=ref, page=sources[ref]['page'],
                         sourceText=sources[ref]['text'])
                for ref in dict.fromkeys(referenced)]
    statements = [Statement(id=f's{i}', kind=item.kind, label=item.label,
                            text=item.text, originalValue=item.original_value,
                            confidence=item.confidence,
                            evidenceIds=list(dict.fromkeys(item.evidence_ids)))
                  for i, item in enumerate(compact.statements)]
    return SemanticDocument(documentCategory=compact.document_category,
                            classificationConfidence=compact.classification_confidence,
                            classificationEvidenceIds=list(dict.fromkeys(compact.classification_evidence_ids)),
                            statements=statements, evidence=evidence)
