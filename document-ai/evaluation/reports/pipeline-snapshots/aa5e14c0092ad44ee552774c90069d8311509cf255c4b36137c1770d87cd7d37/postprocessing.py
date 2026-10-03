"""Conservative deterministic normalization and attention rules over grounded facts."""
import re
from datetime import date, datetime, timezone
from decimal import Decimal, InvalidOperation
from .schemas import Statement

def enrich(semantic, today=None):
    today=today or datetime.now(timezone.utc).date()
    statements=semantic.statements
    for item in statements:
        original=item.originalValue.strip()
        label=item.label.lower()
        if item.kind in ('fact','entity','date','money'):
            if any(t in label for t in ('date','expiry','valid until')) and re.search(r'\d{4}-\d{2}-\d{2}',original): item.kind='date'
            if re.search(r'(?:RM|MYR|USD|EUR|GBP)\s*\d',original) and any(t in label for t in ('salary','rent','deposit','premium','balance','amount','limit','income','deduct')): item.kind='money'
        # Identity transliterations/aliases cannot be verified merely by quoting an original-script name.
        if any(t in label for t in ('name','employee','candidate','policyholder','account holder','patient','tenant','landlord')) and item.kind in ('fact','entity','relationship'):
            item.text=f'{item.label}: {original}.'

        # Only explicit, unambiguous ISO dates are normalized automatically.
        if re.fullmatch(r'\d{4}-\d{2}-\d{2}',original):
            try: item.normalizedValue=date.fromisoformat(original).isoformat()
            except ValueError: item.normalizedValue=None
        else:
            match=re.fullmatch(r'(RM|MYR|USD|EUR|GBP)\s*(\d{1,3}(?:,\d{3})*(?:\.\d{1,2})?|\d+(?:\.\d{1,2})?)',original)
            if match:
                try:
                    currency='MYR' if match[1]=='RM' else match[1]
                    item.normalizedValue=f'{currency} {Decimal(match[2].replace(",",""))}'
                except InvalidOperation: item.normalizedValue=None
            else:
                # Never trust model normalization that changes an unsupported source value.
                item.normalizedValue=None
    added=[]
    for item in statements:
        if 'expir' in item.label.lower() and item.normalizedValue and re.fullmatch(r'\d{4}-\d{2}-\d{2}',item.normalizedValue):
            expires=date.fromisoformat(item.normalizedValue)
            if expires <= today or (expires-today).days <= 90:
                text='The stated expiry date has passed. Verify whether a newer document is available.' if expires<today else 'The stated expiry date is approaching. Review this date and confirm any next steps with the issuing authority.'
                added.append(Statement(id=f'rule-expiry-{item.id}',kind='finding',label='Expiry needs attention',
                    text=text,originalValue=item.originalValue,confidence=item.confidence,evidenceIds=item.evidenceIds))
    facts=[s for s in statements if s.kind in ('fact','date','money','entity')]
    groups={}
    for item in facts:
        label=re.sub(r'\s*\([^)]*\)', '', item.label.lower()).strip()
        label={'date of expiry':'expiry date','date of issue':'issue date','employment start date':'start date'}.get(label,label)
        groups.setdefault(label,[]).append(item)
    singular_fields={'start date','expiry date','issue date','date of birth','salary','monthly salary',
        'passport number','policy number','employment start','contract expiry'}
    for label,items in groups.items():
        if label not in singular_fields: continue
        if len({i.normalizedValue or i.originalValue for i in items})>1 and len(items)>1:
            added.append(Statement(id=f'rule-conflict-{items[0].id}',kind='warning',label='Potential conflicting values',
                text=f'The document contains different values for {label}: '+ '; '.join(i.originalValue for i in items)+'. Review both cited sources to establish which applies.',
                originalValue=items[0].originalValue,confidence=min(i.confidence for i in items),
                evidenceIds=list(dict.fromkeys(e for i in items for e in i.evidenceIds))))
    if semantic.documentCategory=='Employment Contract':
        evidence=semantic.evidence
        probation=next((e for e in evidence if 'probation period' in e.sourceText.lower()),None)
        during=next((e for e in evidence if 'during probation' in e.sourceText.lower() and 'notice' in e.sourceText.lower()),None)
        after=next((e for e in evidence if 'after written confirmation' in e.sourceText.lower() and 'notice' in e.sourceText.lower()),None)
        if probation and during and after:
            added.append(Statement(id='rule-conditional-notice',kind='relationship',label='Probation and confirmed employment notice',
                text='The notice terms depend on employment stage. '+during.sourceText+' '+after.sourceText+
                    ' Review the probation and confirmation terms together before applying either notice provision.',
                originalValue=probation.sourceText,confidence=min(semantic.classificationConfidence,min(s.confidence for s in statements)),
                evidenceIds=list(dict.fromkeys([probation.id,during.id,after.id]))))
    semantic.statements.extend(added)
    return semantic
