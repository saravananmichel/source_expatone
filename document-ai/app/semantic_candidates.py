"""Source-backed candidates, not verified interpretations."""
import re
from datetime import datetime
from .document_preprocessing import paragraph_candidates

DATE = r"(?:\d{1,2}[ -]+[A-Za-z]{3,9}[ -]+\d{4}|\d{1,2}[/-]\d{1,2}[/-]\d{4})"
RULES = {
    "relieving_date": r"\b(?:relieved from (?:your |the )?duties|relieved from (?:our |the )?services|last working day)\s+(?:with effect from\s+|on\s+|is\s+|was\s+)(?P<value>"+DATE+r")\b",
    "joining_date": r"\b(?:date of joining|joining date)\s*(?:is\s+|:\s*)(?P<value>"+DATE+r")\b",
    "start_date": r"\b(?:employment|appointment)\s+(?:will\s+)?(?:commence[s]?|begin[s]?)\s+(?:on\s+)?(?P<value>"+DATE+r")\b",
    "effective_date": r"\b(?:with effect from|effective from|effective on)\s+(?P<value>"+DATE+r")\b",
    "notice_period": r"\bnotice period\s+(?:of\s+|is\s+|shall be\s+)(?P<value>\d{1,3}\s+(?:days?|months?|weeks?))\b",
    "annual_compensation": r"\bannual\s+(?:compensation|salary)\s*(?:of\s+|is\s+|:\s*)(?P<value>(?:MYR|INR|USD|RM|Rs\.?)\s*\d[\d,]*(?:\.\d{2})?)\b",
}
def semantic_candidates(pages):
    found = {}
    reliable = {p.page for p in pages if p.extractionConfidence >= .80}
    for item in paragraph_candidates(pages):
        if item["page"] not in reliable:
            continue
        for field, pattern in RULES.items():
            for match in re.finditer(pattern, item["text"], re.I):
                # Abstain on conditional or negated clauses, rather than asserting a date.
                prefix = item["text"][max(0, match.start()-120):match.start()]
                if re.search(r"\b(?:not|never|unless|if|subject to|proposed|tentative)\b", prefix, re.I):
                    continue
                value = match.group("value")
                if field.endswith("_date"):
                    for fmt in ("%d %B %Y", "%d %b %Y", "%d-%b-%Y", "%d-%B-%Y", "%d/%m/%Y", "%d-%m-%Y"):
                        try:
                            datetime.strptime(" ".join(value.split()), fmt)
                            break
                        except ValueError:
                            continue
                    else:
                        continue
                found.setdefault(field, []).append({
                    "field": field, "value": value, "page": item["page"],
                    "sourceText": match.group(), "requiresConfirmation": True,
                })
    return [items[0] for items in found.values()
            if items[0]["page"] in reliable
            and len({" ".join(i["value"].split()).casefold() for i in items}) == 1]
