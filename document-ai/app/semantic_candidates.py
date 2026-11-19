"""Source-backed candidates, not verified interpretations."""
import re
from datetime import datetime
from .document_preprocessing import paragraph_candidates

DATE = r"(?:\d{1,2}(?:st|nd|rd|th)?[ -]*[A-Za-z]{3,9}[ -]*\d{4}|\d{1,2}[/-]\d{1,2}[/-]\d{4})"
RULES = {
    "relieving_date": r"\b(?:relieved from (?:your |the )?duties|relieved from (?:our |the )?services|last working day)\s+(?:with effect from\s+|on\s+|is\s+|was\s+)(?P<value>"+DATE+r")\b",
    "joining_date": r"\b(?:date of joining|joining date)\s*(?:is\s+|:\s*)(?P<value>"+DATE+r")\b",
    "start_date": r"\b(?:employment|appointment)\s+(?:will\s+)?(?:commence[s]?|begin[s]?)\s+(?:on\s+)?(?P<value>"+DATE+r")\b",
    "revised_designation": r"\b(?:your\s*designation\s*has\s*been\s*changed\s*to|your\s*new\s*designation\s*(?:is|:))\s*(?P<value>[A-Za-z][A-Za-z .&/-]{1,79}?)(?=\s*with\s*effect\s*from|\s*effective\s*(?:from|on)|[.\r\n]|$)",
    "previous_designation": r"\b(?:previous|former)\s+designation\s*(?:was\s+|is\s+|:\s*)(?P<value>[A-Za-z][A-Za-z &/-]{1,79})(?=[.\r\n]|$)",
    "continuing_terms": r"\b(?P<value>all\s*the\s*terms\s*and\s*conditions\s*as\s*set\s*forth\s*in\s*your\s*appointment\s*letter\s*will\s*continue\s*to\s*be\s*in\s*force)\b",
    "effective_date": r"\b(?:with\s*effect\s*from|effective\s+from|effective\s+on)\s*(?P<value>"+DATE+r")\b",
    "notice_period": r"\b(?:notice period\s+(?:of\s+|is\s+|shall be\s+)|(?:written\s+)?notice\s+for\s+a\s+period\s+of\s+)(?P<value>(?:\d{1,3}|one|two|three|four|five|six|seven|eight|nine|ten|fifteen|thirty|sixty|ninety)\s+(?:days?|months?|weeks?))\b",
    "working_hours": r"\b(?:required to work|shall work|will work)\s+(?:for\s+)?(?P<value>(?:a minimum of\s+)?(?:\d{1,2}|six|seven|eight|nine|ten|twelve)\s+hours\s+(?:a|per)\s+day)\b",
    "medical_condition": r"\b(?P<value>(?:your\s+)?appointment\s+(?:will be|is)\s+subject to\s+(?:your\s+)?medical fitness)\b",
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
                    normalized = re.sub(r"^(\d{1,2})(?:st|nd|rd|th)?[ -]*([A-Za-z]{3,9})[ -]*(\d{4})$", r"\1 \2 \3", value, flags=re.I)
                    for fmt in ("%d %B %Y", "%d %b %Y", "%d-%b-%Y", "%d-%B-%Y", "%d/%m/%Y", "%d-%m-%Y"):
                        try:
                            datetime.strptime(" ".join(normalized.split()), fmt)
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
