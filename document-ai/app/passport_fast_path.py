"""DEV-only eligibility gate; no inferred or automatically verified identities."""
from datetime import datetime
from .passport_mrz import mrz_fields
from .structured_analysis import passport_category, _compact_passport_identity_layout, _passport_layout_number, _passport_spatial_fields

REQUIRED = {"passport_number", "surname", "given_names", "date_of_birth",
            "issue_date", "expiry_date"}

OPTIONAL = {"nationality", "sex", "place_of_birth", "place_of_issue"}

def eligible_passport(pages, fields, allow_layout=False):
    if not pages or len(pages) > 2 or not passport_category(pages, fields):
        return False
    first = pages[0]
    if first.extraction != "ocr" or first.extractionConfidence < 0.80:
        return False
    groups = {}
    for field in fields:
        if field["page"] != first.page or field["field"] not in REQUIRED:
            continue
        if field["value"] not in field["sourceText"]:
            return False
        groups.setdefault(field["field"], set()).add(field["value"])
    if not REQUIRED.issubset(groups) or any(len(v) != 1 for v in groups.values()):
        return False
    values = {k: next(iter(v)) for k, v in groups.items()}
    dates = []
    for key in ("date_of_birth", "issue_date", "expiry_date"):
        parsed = None
        for fmt in ("%d/%m/%Y", "%d-%m-%Y", "%Y-%m-%d", "%d.%m.%Y"):
            try:
                parsed = datetime.strptime(values[key], fmt).date()
                break
            except ValueError:
                pass
        if parsed is None:
            return False
        dates.append(parsed)
    if not dates[0] < dates[1] < dates[2]:
        return False
    mrz = mrz_fields([first])
    if not mrz and allow_layout:
        if not _compact_passport_identity_layout(first):
            return False
        spatial = _passport_spatial_fields(first) + _passport_layout_number(first)
        return all(any(f["field"] == key and f["value"] == values[key]
                       and f["sourceText"] in first.text for f in spatial)
                   for key in REQUIRED)
    # Cross-check visible dates against the same checksum-valid MRZ.
    return any(f["value"] == values["passport_number"]
               and f["sourceText"][13:19] == dates[0].strftime("%y%m%d")
               and f["sourceText"][21:27] == dates[2].strftime("%y%m%d")
               for f in mrz_fields([first]))


def passport_review_result(pages, fields, allow_layout=False):
    """Return review-only candidates without promoting identity to supported facts."""
    from .schemas import Evidence, Statement, SemanticDocument
    if not eligible_passport(pages, fields, allow_layout=allow_layout):
        raise ValueError("passport_fast_path_ineligible")
    first = pages[0]
    optional_values = {}
    for f in fields:
        if f["page"] == first.page and f["field"] in OPTIONAL:
            optional_values.setdefault(f["field"], set()).add(f["value"])
    spatial = _passport_spatial_fields(first)
    optional = {f["field"] for f in spatial
                if f["field"] in optional_values
                and optional_values[f["field"]] == {f["value"]}
                and f["value"] in f["sourceText"] and f["sourceText"] in first.text}
    evidence = []
    statements = []
    seen = set()
    for field in fields:
        if field["page"] != pages[0].page or field["field"] not in (REQUIRED | optional):
            continue
        if field["value"] not in field["sourceText"] or field["sourceText"] not in first.text:
            continue
        if field["field"] in seen:
            continue
        seen.add(field["field"])
        eid = f"e{len(evidence)}"
        evidence.append(Evidence(id=eid, page=field["page"], sourceText=field["sourceText"]))
        label = field["field"].replace("_", " ").capitalize()
        statements.append(Statement(id=f"passport-{len(statements)}",
            kind="fact", label=label, text=f"{label}: {field['value']}.",
            originalValue=field["value"], confidence=0.7,
            evidenceIds=[eid], supportStatus="UNCERTAIN",
            supportReason="Source-grounded candidate; requires user confirmation."))
    semantic = SemanticDocument(documentCategory="Passport", classificationConfidence=0.8,
        classificationEvidenceIds=[e.id for e in evidence], statements=statements, evidence=evidence)
    semantic._quality = {"reasoningStatus": "skipped: deterministic passport",
        "supportMethod": ("source-and-mrz-review-only" if eligible_passport(pages, fields) else "source-layout-review-only"), "verdictCoverage": 0.0}
    return semantic
