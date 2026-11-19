"""DEV-only eligibility gate; no inferred or automatically verified identities."""
from datetime import datetime
import os
import re
from .passport_mrz import mrz_fields
from .structured_analysis import passport_category, _compact_passport_identity_layout, _passport_layout_number, _passport_spatial_fields

REQUIRED = {"passport_number", "surname", "given_names", "date_of_birth",
            "issue_date", "expiry_date"}

OPTIONAL = {"nationality", "sex", "place_of_birth", "place_of_issue"}

def eligible_passport(pages, fields, allow_layout=False):
    v6 = os.getenv("DOCUMENT_AI_UNIVERSAL_V2", "0") == "1"
    if not pages or len(pages) > (3 if v6 else 2) or not passport_category(pages, fields):
        return False
    first = pages[0]
    if first.extraction != "ocr" or first.extractionConfidence < 0.80:
        return False
    groups = {}
    for field in fields:
        if field["page"] != first.page or field["field"] not in REQUIRED:
            continue
        if field["value"] not in field["sourceText"] or field["sourceText"] not in first.text:
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
    if len(pages) == 3:
        # Extra pages may contain a different identity; require checksum-backed
        # first-page evidence and reject competing current identity candidates.
        if not mrz or any(f["page"] != first.page and f["field"] in REQUIRED
                          and f["value"] != values[f["field"]] for f in fields):
            return False
    if not mrz and allow_layout:
        if not _compact_passport_identity_layout(first):
            return False
        spatial = _passport_spatial_fields(first) + _passport_layout_number(first)
        return all(any(f["field"] == key and f["value"] == values[key]
                       and f["sourceText"] in first.text for f in spatial)
                   for key in REQUIRED)
    # Cross-check visible dates against the same checksum-valid MRZ.
    return any(f["value"] == values["passport_number"]
               and re.sub(r"[ \t]+", "", f["sourceText"])[13:19] == dates[0].strftime("%y%m%d")
               and re.sub(r"[ \t]+", "", f["sourceText"])[21:27] == dates[2].strftime("%y%m%d")
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


def grouped_passport_review_fields(pages, fields):
    """Narrow partial route for explicitly grouped first-page identity dates."""
    from .structured_analysis import _passport_grouped_rows
    if not pages or len(pages) > 3 or not passport_category(pages, fields):
        return []
    first = pages[0]
    grouped = _passport_grouped_rows(first)
    dates = {f["field"]: f for f in grouped}
    keys = ("date_of_birth", "issue_date", "expiry_date")
    if not all(k in dates for k in keys):
        return []
    def parse(value):
        for fmt in ("%d/%m/%Y", "%d-%m-%Y", "%d.%m.%Y", "%Y-%m-%d"):
            try:
                return datetime.strptime(value, fmt).date()
            except ValueError:
                pass
        return None
    chronology = [parse(dates[k]["value"]) for k in keys]
    if None in chronology or not chronology[0] < chronology[1] < chronology[2]:
        return []
    numbers = [f for f in fields if f["field"] == "passport_number"
               and f["page"] == first.page and f["value"] in f["sourceText"]
               and f["sourceText"] in first.text]
    if len({f["value"] for f in numbers}) != 1:
        return []
    if any(f["page"] != first.page and f["field"] == "passport_number"
           and f["value"] != numbers[0]["value"] for f in fields):
        return []
    if any(f["field"] in keys and f["page"] == first.page
           and parse(f["value"]) != parse(dates[f["field"]]["value"]) for f in fields):
        return []
    if not all(f["sourceText"] in first.text and f["value"] in f["sourceText"] for f in grouped):
        return []
    return [numbers[0], *grouped]


def grouped_passport_review_result(fields):
    from .schemas import Evidence, Statement, SemanticDocument
    evidence = []
    statements = []
    for field in fields:
        eid = f"e{len(evidence)}"
        label = field["field"].replace("_", " ").capitalize()
        evidence.append(Evidence(id=eid, page=field["page"], sourceText=field["sourceText"]))
        statements.append(Statement(id=f"passport-{len(statements)}", kind="fact",
            label=label, text=f"{label}: {field['value']}.", originalValue=field["value"],
            confidence=0.7, evidenceIds=[eid], supportStatus="UNCERTAIN",
            supportReason="Partial grouped OCR review; verify against original."))
    semantic = SemanticDocument(documentCategory="Passport", classificationConfidence=0.8,
        classificationEvidenceIds=[e.id for e in evidence], statements=statements, evidence=evidence)
    semantic._quality = {"reasoningStatus": "skipped: grouped partial passport review",
                         "supportMethod": "exact-source-only", "verdictCoverage": 0.0}
    return semantic
