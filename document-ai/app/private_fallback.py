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


def _epass_fields(pages):
    """Extract only explicit label/value pairs from a Malaysia ePASS text layer.

    This is a conservative source-only parser, not verification of pass validity.
    """
    joined = '\n'.join(p.text for p in pages)
    if not (re.search(r'(?i)ePASS Government of Malaysia', joined)
            and re.search(r'(?i)EMPLOYMENT PASS', joined)):
        return []
    patterns = (
        ('Holder name', r'(?im)^\s*Name\s*:\s*([^\n]+)', 'entity'),
        ('Gender', r'(?im)^\s*Gender\s*:\s*([^\n]+)', 'fact'),
        ('Passport number', r'(?im)^\s*Passport\s*No\s*:\s*([A-Z0-9]+)\s*
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
    # Prefer exact, field-specific source statements for a recognizable ePASS.
    # Do not infer immigration status or invent absent fields.
    epass = _epass_fields(pages)
    if epass:
        structured = []
        for label, value, kind, page_number in epass:
            matching = next((e for e in evidence
                             if e.page == page_number and value in e.sourceText), None)
            if matching is None:
                continue
            structured.append(Statement(
                id=f'epass-{len(structured)}', kind=kind,
                label=label, text=f'{label}: {value}.',
                originalValue=value, confidence=0.7,
                evidenceIds=[matching.id], supportStatus='SUPPORTED',
                supportReason='Exact source value; document not independently verified.'
            ))
        if structured:
            return SemanticDocument(
                documentCategory='Employment Pass',
                classificationConfidence=0.7,
                classificationEvidenceIds=[structured[0].evidenceIds[0]],
                statements=structured, evidence=evidence,
            )
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
, 'fact'),
        ('Nationality', r'(?im)^\s*Nationality\s*:\s*([^\n]+)', 'fact'),
        ('Employer', r'(?im)^\s*With\s*:\s*([^\n]+)', 'entity'),
        ('Issue date', r'(?im)^\s*Date\s*Of\s*Issue\s*:\s*(\d{1,2}\s+[A-Z]{3}\s+\d{4})', 'date'),
        ('Valid until', r'(?i)until\s+(\d{1,2}\s+[A-Za-z]{3}\s+\d{4})', 'date'),
    )
    found = []
    for label, pattern, kind in patterns:
        match = re.search(pattern, joined)
        if not match:
            continue
        value = match.group(1).strip()
        if not value or len(value) > 120:
            continue
        page = next((p for p in pages if value in p.text), None)
        if page is None:
            continue
        found.append((label, value, kind, page.page))
    return found


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
