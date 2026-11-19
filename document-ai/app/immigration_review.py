"""Experimental review-only interpretation; never establishes legal status."""
from datetime import datetime
from .document_router import classify_document
from .immigration_fields import immigration_fields
from .schemas import Evidence, Statement, SemanticDocument


def _date(value):
    for fmt in ("%d/%m/%Y", "%d-%m-%Y", "%Y-%m-%d"):
        try:
            return datetime.strptime(value, fmt).date()
        except ValueError:
            continue
    return None


def immigration_review(pages):
    """Return (semantic, fields) or None when evidence is insufficient."""
    if not pages or len(pages) > 3 or any(p.extractionConfidence < .80 for p in pages):
        return None
    route = classify_document(pages)
    if route is None:
        return None
    fields = immigration_fields(pages, route)
    # Do not rely on generic name or passport labels on a reverse/attachment page.
    fields = [f for f in fields if f["page"] == route.page]
    values = {f["field"]: f["value"] for f in fields}
    required = {"person_name", "passport_number"}
    if route.subtype == "employment_pass_with_visa":
        required |= {"expiry_date", "employer"}
    else:
        required |= {"application_type", "application_category", "reference_number"}
    if not required <= values.keys():
        return None
    dates = {}
    for key in ("issue_date", "expiry_date", "letter_validity"):
        if key in values:
            dates[key] = _date(values[key])
            if dates[key] is None:
                return None
    if "issue_date" in dates and "expiry_date" in dates:
        if dates["issue_date"] >= dates["expiry_date"]:
            return None
    first = pages[0]
    if any(source not in first.text for source in route.evidence):
        return None
    evidence = [Evidence(id=f"route-{i}", page=route.page, sourceText=source)
                for i, source in enumerate(route.evidence)]
    category_ids = [e.id for e in evidence]
    statements = []
    for i, field in enumerate(fields):
        if field["sourceText"] not in first.text or field["value"] not in field["sourceText"]:
            return None
        eid = f"field-{i}"
        evidence.append(Evidence(id=eid, page=route.page, sourceText=field["sourceText"]))
        label = field["field"].replace("_", " ").capitalize()
        statements.append(Statement(
            id=f"review-{i}", kind="fact", label=label,
            text=f"{label}: {field['value']}", originalValue=field["value"],
            evidenceIds=[eid], confidence=.7, supportStatus="UNCERTAIN",
            supportReason="Source candidate; requires user confirmation."
        ))
    semantic = SemanticDocument(documentCategory=route.category,
        classificationConfidence=.8, classificationEvidenceIds=category_ids,
        statements=statements, evidence=evidence)
    semantic._quality = {"reasoningStatus": "skipped: deterministic immigration",
        "supportMethod": "source-label-review-only", "verdictCoverage": 0.0,
        "immigrationSubtype": route.subtype}
    return semantic, fields
