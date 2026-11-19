"""Review-only reconciliation; never promote candidates to verified facts."""
def reconcile_review_fields(pages, fields, candidates):
    source = {p.page: p.text for p in pages}
    groups = {}
    for field in [*fields, *candidates]:
        name = field.get("field")
        value = field.get("value")
        quote = field.get("sourceText")
        if not all(isinstance(x, str) and x.strip() for x in (name, value, quote)):
            continue
        if quote not in source.get(field.get("page"), "") or value not in quote:
            continue
        groups.setdefault(name, []).append(field)
    result = []
    conflicts = []
    for name, items in groups.items():
        if len({" ".join(i["value"].split()).casefold() for i in items}) != 1:
            conflicts.append(name)
            continue
        result.append({**items[0], "requiresConfirmation": True})
    return result, sorted(conflicts)

def append_review_statements(result, pages):
    """Expose reconciled candidates as uncertain statements, never verified facts."""
    from .schemas import Evidence, Statement
    source = {p.page: p.text for p in pages if p.extractionConfidence >= .80}
    statements = result.setdefault("statements", [])
    evidence = result.setdefault("evidence", [])
    existing = {s.get("id") for s in statements} | {e.get("id") for e in evidence}
    added = 0
    for field in result.get("structuredFields", []):
        name, value, quote = (field.get(k) for k in ("field", "value", "sourceText"))
        if not all(isinstance(x, str) and x.strip() for x in (name, value, quote)):
            continue
        if not field.get("requiresConfirmation") or value not in quote or quote not in source.get(field.get("page"), ""):
            continue
        if len(quote) > 4000 or len(value) > 1500 or len(name) > 180:
            continue
        label = name.replace("_", " ").capitalize()
        if any(s.get("label", "").casefold() == label.casefold() for s in statements):
            continue
        if len(statements) >= 300 or len(evidence) >= 300:
            break
        i = added
        while f"review-e-{i}" in existing or f"review-s-{i}" in existing:
            i += 1
        eid, sid = f"review-e-{i}", f"review-s-{i}"
        e = Evidence(id=eid, page=field["page"], sourceText=quote)
        s = Statement(id=sid, kind="fact", label=label, text=f"{label}: {value}",
                      originalValue=value, confidence=0.7, evidenceIds=[eid],
                      supportStatus="UNCERTAIN", supportReason="Source candidate; requires user confirmation.")
        evidence.append(e.model_dump())
        statements.append(s.model_dump())
        existing.update((eid, sid))
        added += 1
    diagnostics = result.setdefault("qualityDiagnostics", {})
    # Evidence linkage is distinct from verified semantic support.
    valid_ids = {e.get("id") for e in evidence
                 if e.get("sourceText") and e["sourceText"] in source.get(e.get("page"), "")}
    linked = sum(bool(s.get("evidenceIds")) and
                 all(eid in valid_ids for eid in s["evidenceIds"]) for s in statements)
    diagnostics["sourceLinkedClaims"] = linked
    diagnostics["sourceLinkedCoverage"] = linked / len(statements) if statements else 0.0
    # Keep the nested representation aligned with the final review graph.
    # Preserve classification and other semantic metadata.
    semantic = result.get("semanticDocument")
    if isinstance(semantic, dict) and statements and evidence:
        from copy import deepcopy
        semantic["statements"] = deepcopy(statements)
        semantic["evidence"] = deepcopy(evidence)
    diagnostics["reviewStatementsAdded"] = added
    diagnostics["uncertainClaims"] = sum(s.get("supportStatus") == "UNCERTAIN" for s in statements)


def employment_review_ready(route, pages, *groups):
    """Permit partial review only for independently grounded, unambiguous facts."""
    if not pages or any(p.extractionConfidence < .80 for p in pages):
        return False
    required = {
        "employment_offer": {"effective_date", "annual_compensation"},
        "designation_change": {"effective_date", "revised_designation"},
        "performance_appraisal": {"effective_date", "revised_annual_ctc"},
    }.get(route.subtype)
    if required is None:
        return False
    fields, conflicts = reconcile_review_fields(pages, [], [
        f for group in groups for f in group if f.get("requiresConfirmation")
    ])
    names = {f["field"] for f in fields}
    if conflicts:
        return False
    if route.subtype == "employment_offer":
        # Offers often specify a joining or start date rather than an
        # effective date, or monthly pay rather than annual compensation.
        # Do not derive annual pay or silently reconcile competing dates.
        dates = names & {"effective_date", "joining_date", "start_date"}
        pay = names & {"annual_compensation", "monthly_compensation"}
        date_values = {f["value"].strip().casefold() for f in fields
                       if f["field"] in dates}
        return bool(dates and pay and len(date_values) == 1)
    return required <= names
