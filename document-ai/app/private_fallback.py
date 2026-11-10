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


def _plausible_field(label, value):
    """Reject obvious OCR punctuation noise without inventing semantics."""
    letters = sum(char.isalpha() for char in label)
    value_chars = sum(char.isalnum() for char in value)
    return (
        letters >= 2 and letters / max(1, len(label)) >= 0.45
        and value_chars >= 3
        and value_chars / max(1, len(value)) >= 0.6
        and not re.search(r'[<>~\\\\]{2,}', label + value)
        and not re.search(r'[<>|;#]{1,}', label)
        and not re.search(r'[<>|]{1,}', value)
        and len(re.findall(r'[A-Za-z]{2,}', label)) >= 1
    )


def _source_fields(text):
    """Recover literal label/value pairs from native PDF reading order.

    Some PDFs put the label, colon, and value on separate lines. Only accept
    adjacent source fragments; do not infer missing or translated values.
    """
    lines = [line.strip() for line in text.splitlines() if line.strip()]
    for i, line in enumerate(lines):
        match = re.fullmatch(r'([^:\n]{2,60})\s*:\s*(.{1,180})', line)
        if match:
            label, value = match.group(1).strip(), match.group(2).strip()
            if value and not value.startswith(':') and _plausible_field(label, value):
                yield label, value, line
            continue
        if i + 1 >= len(lines):
            continue
        label = line[:-1].strip() if line.endswith(':') else line
        if not (2 <= len(label) <= 60 and re.fullmatch(r'[\w /().-]+', label)):
            continue
        next_line = lines[i + 1]
        if next_line == ':':
            if i + 2 >= len(lines):
                continue
            value = lines[i + 2].lstrip(': ').strip()
            source = '\n'.join(lines[i:i + 3])
        elif next_line.startswith(':'):
            value = next_line[1:].strip()
            source = '\n'.join(lines[i:i + 2])
        else:
            continue
        if value and len(value) <= 180 and ':' not in value and _plausible_field(label, value):
            yield label, value, source


def source_fallback(pages):
    """Exact source-only fallback. Classification remains Unknown."""
    evidence = []
    statements = []
    for page in pages:
        if not page.text.strip():
            continue
        for label, value, quote in _source_fields(page.text):
            if len(statements) >= 200:
                break
            # A short source quote is more useful than an entire page of OCR.
            evidence_id = f'e{len(evidence)}'
            evidence.append(Evidence(id=evidence_id, page=page.page,
                                     sourceText=quote))
            statements.append(Statement(
                id=f'source-{len(statements)}', kind='fact',
                label=label, text=f'{label}: {value}.',
                originalValue=value, confidence=0.7,
                evidenceIds=[evidence_id], supportStatus='SUPPORTED',
                supportReason='Exact label/value quotation from source.'
            ))
        if len(statements) >= 200:
            break
    if not evidence:
        for page in pages:
            if page.text.strip():
                snippet = page.text.strip()[:180]
                evidence.append(Evidence(id='e0', page=page.page,
                                         sourceText=snippet))
                statements.append(Statement(
                    id='source-0', kind='fact', label='Extracted source text',
                    text=f'Extracted source text: {snippet}.',
                    originalValue=snippet, confidence=0.5,
                    evidenceIds=['e0'], supportStatus='SUPPORTED',
                    supportReason='Exact source quotation, not a classified field.'
                ))
                break
    if not evidence:
        raise ValueError('no_source_text')
    return SemanticDocument(documentCategory='Unknown',
        classificationConfidence=0, classificationEvidenceIds=[evidence[0].id],
        statements=statements, evidence=evidence)
