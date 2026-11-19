"""General document hints only; never bypass reasoning or verify facts."""
import re
from .document_router import Route

# Require an explicit title and independent labelled evidence.
RULES = (
    ("Utility Bill", "utility_bill", r"(?:electricity|water|gas|utility)\s+bill", ("account_number", "amount_due")),
    ("Bank Statement", "bank_statement", r"bank\s+statement", ("account_number", "statement_period")),
    ("Offer Letter", "offer_letter", r"(?:employment\s+offer|offer\s+letter|letter\s+of\s+offer)", ("position", "start_date")),
    ("Employment Contract", "employment_contract", r"(?:employment\s+contract|contract\s+of\s+employment)", ("employer", "employee")),
    ("Government Letter", "government_letter", r"government\s+letter", ("reference_number", "letter_date")),
)
LABELS = {
    "account_number": r"account\s*(?:number|no\.?)",
    "amount_due": r"(?:amount|total)\s+due",
    "statement_period": r"statement\s+period",
    "position": r"(?:position|job\s+title)",
    "start_date": r"(?:start|commencement)\s+date",
    "employer": r"employer",
    "employee": r"employee",
    "reference_number": r"reference\s*(?:number|no\.?)",
    "letter_date": r"letter\s+date",
    "salary": r"(?:basic\s+salary|monthly\s+salary)",
    "due_date": r"(?:payment\s+)?due\s+date",
}


def general_document_hints(pages):
    if not pages or pages[0].extractionConfidence < .80:
        return None, []
    first = pages[0]
    lines = [line.strip() for line in first.text.splitlines() if line.strip()]
    groups = {}
    for line in lines:
        for key, label in LABELS.items():
            match = re.fullmatch(r"(?:" + label + r")\s*[:：]\s*(.{1,160})", line, re.I)
            if match and match.group(1).strip():
                value = match.group(1).strip()
                if value.lower() not in {"unknown", "n/a", "none", "-"}:
                    groups.setdefault(key, []).append((value, line))
    unique = {key: items[0] for key, items in groups.items()
              if len({value for value, _ in items}) == 1}
    candidates = []
    for category, subtype, title, required in RULES:
        header = next((line for line in lines[:10] if re.fullmatch(title, line, re.I)), None)
        if header and all(key in unique for key in required):
            candidates.append(Route(category, subtype, first.page,
                                    (header, *(unique[key][1] for key in required))))
    if len(candidates) != 1:
        return None, []
    route = candidates[0]
    allowed = set(next(r[3] for r in RULES if r[1] == route.subtype))
    allowed |= {"utility_bill": {"due_date"}, "offer_letter": {"salary"},
                "employment_contract": {"salary", "start_date"}}.get(route.subtype, set())
    return route, [{"field": key, "value": value, "page": first.page,
                    "sourceText": source, "requiresConfirmation": True}
                   for key, (value, source) in unique.items() if key in allowed]
