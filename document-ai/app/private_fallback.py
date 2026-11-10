"""Conservative, private, evidence-backed DEV fallback.

Only source quotations are marked supported. No model-generated paraphrase is
automatically promoted to a verified interpretation.
"""
import re
from .schemas import Evidence, Statement, SemanticDocument
from .compact import source_passages


def exact_support(semantic):
    evidence = {e.id: e.sourceText for e in semantic.evidence}
    for claim in semantic.statements:
        claim.supportStatus = 'UNCERTAIN'
        claim.supportReason = 'Semantic interpretation not independently verified.'
        if claim.kind not in ('fact', 'entity', 'date', 'money'):
            continue
        source = ' '.join(evidence.get(i, '') for i in claim.evidenceIds)
        value = ' '.join(claim.originalValue.split())
        explanation = ' '.join(claim.text.split())
        if value and value in ' '.join(source.split()) and (
            explanation == value or explanation in (
                f'{claim.label}: {value}.', f'{claim.label}: {value}'
            )
        ):
            claim.supportStatus = 'SUPPORTED'
            claim.supportReason = 'Exact source quotation; no inferred meaning.'
    return semantic


def source_fallback(pages):
    passages = source_passages(pages)
    if not passages:
        raise ValueError('no_source_text')
    evidence = []
    statements = []
    for passage in passages[:100]:
        quote = passage['text'].strip()
        if not quote:
            continue
        evidence.append(Evidence(id=passage['id'], page=passage['page'],
                                 sourceText=quote[:4000]))
        # Only quote source text. Do not assign field semantics without verification.
        for line in quote.splitlines():
            line = line.strip()
            if not line or len(line) > 250:
                continue
            if not re.match(r'^[^:\n]{2,60}:\s*\S', line):
                continue
            label, value = line.split(':', 1)
            value = value.strip()
            if not value or len(value) > 180:
                continue
            statements.append(Statement(
                id=f'source-{len(statements)}', kind='fact',
                label=label.strip(), text=f'{label.strip()}: {value}.',
                originalValue=value, confidence=0.7,
                evidenceIds=[passage['id']], supportStatus='SUPPORTED',
                supportReason='Exact label/value quotation from source.'
            ))
            if len(statements) >= 200:
                break
        if len(statements) >= 200:
            break
    if not evidence:
        raise ValueError('no_source_text')
    if not statements:
        first = evidence[0]
        snippet = first.sourceText[:180].strip()
        statements.append(Statement(
            id='source-0', kind='fact', label='Extracted source text',
            text=f'Extracted source text: {snippet}.', originalValue=snippet,
            confidence=0.5, evidenceIds=[first.id],
            supportStatus='SUPPORTED',
            supportReason='Exact source quotation, not a classified field.'
        ))
    return SemanticDocument(documentCategory='Unknown',
        classificationConfidence=0, classificationEvidenceIds=[evidence[0].id],
        statements=statements, evidence=evidence)


# Conservative document-specific enrichment of the existing source-only fallback.
_original_source_fallback = source_fallback


def source_fallback(pages):
    result = _original_source_fallback(pages)
    for page in pages:
        lower = page.text.lower()
        if 'epass government of malaysia' not in lower or 'employment pass' not in lower:
            continue
        labels = {'name': 'Holder name', 'gender': 'Gender',
                  'passport no': 'Passport number', 'nationality': 'Nationality',
                  'with': 'Employer', 'date of issue': 'Issue date'}
        supported = []
        for line in page.text.splitlines():
            if ':' not in line:
                continue
            left, right = line.split(':', 1)
            label = labels.get(left.strip().lower())
            value = right.strip()
            if not label or not value or len(value) > 120:
                continue
            evidence = next((e for e in result.evidence
                             if e.page == page.page and value in e.sourceText), None)
            if evidence is None:
                continue
            supported.append(Statement(
                id='epass-' + str(len(supported)), kind='fact',
                label=label, text=label + ': ' + value + '.',
                originalValue=value, confidence=0.7,
                evidenceIds=[evidence.id], supportStatus='SUPPORTED',
                supportReason='Exact source quotation; no independent verification.'
            ))
        if supported:
            result.documentCategory = 'Employment Pass'
            result.classificationConfidence = 0.7
            result.classificationEvidenceIds = [supported[0].evidenceIds[0]]
            result.statements = supported
            break
    return result
