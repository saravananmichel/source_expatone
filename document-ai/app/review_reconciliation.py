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
