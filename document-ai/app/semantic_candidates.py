"""Source-backed candidates, not verified interpretations."""
import re
from datetime import datetime
from .document_preprocessing import paragraph_candidates

DATE = r"(?:[A-Za-z]{3,9}[ -]*\d{1,2}(?:st|nd|rd|th)?[ -]*\d{4}|\d{1,2}(?:st|nd|rd|th)?[ -]*[A-Za-z]{3,9}[ -]*\d{4}|\d{1,2}[/-]\d{1,2}[/-]\d{4})"
MONEY = r"(?:MYR|INR|USD|RM|Rs\.?)\s*(?:\d{1,3}(?:,\d{3})+|\d{1,2}(?:,\d{2})+,\d{3}|\d+)(?:\.\d{2})?"
DURATION = r"(?:\d{1,3}|one|two|three|four|five|six|twelve|thirty|sixty|ninety)\s+(?:days?|weeks?|months?|years?)"
RULES = {
    "monthly_compensation": r"\b(?:monthly\s+(?:basic\s+)?salary|basic\s+salary\s+per\s+month)\s*(?:of\s+|is\s+|:\s*)(?P<value>"+MONEY+r")(?![\d,]|\.\d)",
    "probation_period": r"\b(?:probation\s+period\s*(?:of\s+|is\s+|:\s*)|on\s+probation\s+for\s+)(?P<value>"+DURATION+r")\b",
    "annual_leave": r"\b(?P<value>(?:you\s+(?:will\s+be|are)\s+entitled\s+to\s+)\d{1,2}\s+(?:working\s+|calendar\s+)?days?\s+of\s+(?:paid\s+)?annual\s+leave(?:\s+per\s+(?:year|annum))?)\b",
    "medical_benefits": r"\b(?P<value>you\s+(?:will\s+be|are)\s+entitled\s+to\s+(?:medical|health)\s+(?:insurance|coverage|benefits)[^\n.!?]{0,120})(?=[.!?\n]|$)",
    "confidentiality_clause": r"\b(?P<value>you\s+(?:shall|must)\s+(?:maintain|protect|keep)\s+(?:the\s+)?confidentiality\s+of\s+[^\n.!?]{1,180})(?=[.!?\n]|$)",
    "termination_clause": r"\b(?P<value>(?:either\s+party|the\s+company|the\s+employer)\s+may\s+terminate\s+(?:this|your|the)\s+(?:employment|appointment|agreement)[^\n.!?]{0,180})(?=[.!?\n]|$)",
    "revised_annual_ctc": r"\byour\s*annual\s*CTC\s*as\s*of\s*(?:"+DATE+r")\s*is\s*(?P<value>"+MONEY+r")(?![\d,]|\.\d)",
    "effective_date": r"\b(?:with\s*effect\s*from|effective\s+from|effective\s+on|annual\s*CTC\s*as\s*of)\s*(?P<value>"+DATE+r")\b",
    "relieving_date": r"\b(?:relieved from (?:your |the )?duties|relieved from (?:our |the )?services|last working day)\s+(?:with effect from\s+|on\s+|is\s+|was\s+)(?P<value>"+DATE+r")\b",
    "joining_date": r"\b(?:date of joining|joining date)\s*(?:is\s+|:\s*)(?P<value>"+DATE+r")\b",
    "start_date": r"\b(?:employment|appointment)\s+(?:will\s+)?(?:commence[s]?|begin[s]?)\s+(?:on\s+)?(?P<value>"+DATE+r")\b",
    "revised_designation": r"\b(?:your\s*designation\s*has\s*been\s*changed\s*to|your\s*new\s*designation\s*(?:is|:))\s*(?P<value>[A-Za-z][A-Za-z .&/-]{1,79}?)(?=\s*with\s*effect\s*from|\s*effective\s*(?:from|on)|[.\r\n]|$)",
    "previous_designation": r"\b(?:previous|former)\s+designation\s*(?:was\s+|is\s+|:\s*)(?P<value>[A-Za-z][A-Za-z &/-]{1,79})(?=[.\r\n]|$)",
    "continuing_terms": r"\b(?P<value>all\s*the\s*terms\s*and\s*conditions\s*as\s*set\s*forth\s*in\s*your\s*appointment\s*letter\s*will\s*continue\s*to\s*be\s*in\s*force)\b",
    "notice_period": r"\b(?:notice period\s+(?:of\s+|is\s+|shall be\s+)|(?:written\s+)?notice\s+for\s+a\s+period\s+of\s+)(?P<value>(?:\d{1,3}|one|two|three|four|five|six|seven|eight|nine|ten|fifteen|thirty|sixty|ninety)\s+(?:days?|months?|weeks?))\b",
    "working_hours": r"\b(?:required to work|shall work|will work)\s+(?:for\s+)?(?P<value>(?:a minimum of\s+)?(?:\d{1,2}|six|seven|eight|nine|ten|twelve)\s+hours\s+(?:a|per)\s+day)\b",
    "medical_condition": r"\b(?P<value>(?:your\s+)?appointment\s+(?:will be|is)\s+subject to\s+(?:your\s+)?medical fitness)\b",
    "annual_compensation": r"\bannual\s+(?:compensation|salary)\s*(?:of\s+|is\s+|:\s*)(?P<value>"+MONEY+r")(?![\d,]|\.\d)",
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
                if re.search(r"\b(?:not|never|unless|if|subject to|proposed|tentative)\b", re.split(r"[.!?\n]", prefix)[-1], re.I):
                    continue
                # A short matched entitlement must not omit a condition
                # in the remainder of the same sentence (including wrapped lines).
                if field in {"annual_leave", "probation_period", "notice_period",
                             "working_hours", "medical_benefits",
                             "confidentiality_clause", "termination_clause"}:
                    tail = re.split(r"[.!?]", item["text"][match.end():], maxsplit=1)[0]
                    if tail.strip(" \t\r\n,;"):
                        continue
                value = match.group("value")
                if field.endswith("_date"):
                    normalized = re.sub(r"^(\d{1,2})(?:st|nd|rd|th)?[ -]*([A-Za-z]{3,9})[ -]*(\d{4})$", r"\1 \2 \3", value, flags=re.I)
                    normalized = re.sub(r"^([A-Za-z]{3,9})[ -]*(\d{1,2})(?:st|nd|rd|th)?[ -]*(\d{4})$", r"\2 \1 \3", normalized, flags=re.I)
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
    for candidate in appraisal_salary_rows(pages):
        found.setdefault(candidate["field"], []).append(candidate)
    return [items[0] for items in found.values()
            if items[0]["page"] in reliable
            and len({" ".join(i["value"].split()).casefold() for i in items}) == 1]


def appraisal_salary_rows(pages):
    """Extract explicit monthly/annual row pairs; never calculate missing amounts."""
    labels = {
        "basic": "basic_salary", "hra": "hra",
        "conveyanceallowance": "conveyance_allowance",
        "medicalreimbursement": "medical_reimbursement",
        "foodallowance": "food_allowance", "specialallowance": "special_allowance",
        "company'scontributiontop.f.": "employer_pf",
        "medicalinsurance": "medical_insurance", "gratuity": "gratuity",
    }
    amount = re.compile(r"(?:\d+|\d{1,3}(?:,\d{3})+|\d{1,2}(?:,\d{2})*,\d{3})(?:\.\d{1,2})?")
    compact = lambda s: re.sub(r"\s+", "", s).casefold().rstrip(":")
    result = []
    for page in pages:
        if page.extractionConfidence < .80:
            continue
        lines = page.text.splitlines()
        headers = [i for i in range(len(lines)-4)
                   if [compact(l) for l in lines[i:i+5]] ==
                   ["salarybreakup", "monthlysalary", "annualsalary", "inrupees", "inrupees"]]
        if len(headers) != 1:
            continue
        i = headers[0]+5
        section = "cash"
        page_rows = []
        seen = set()
        ambiguous = False
        while i < len(lines):
            key = compact(lines[i])
            if key == "non-cashcomponent":
                section = "non_cash"
                i += 1
                continue
            field = labels.get(key)
            if key == "total":
                field = section + "_total"
            if not field or i+2 >= len(lines):
                break
            if field in seen:
                ambiguous = True
                break
            seen.add(field)
            values = [l.strip() for l in lines[i+1:i+3]]
            if not all(amount.fullmatch(v) for v in values):
                break
            quote = "\n".join(lines[i:i+3])
            if quote not in page.text:
                break
            for period, value in zip(("monthly", "annual"), values):
                page_rows.append({"field": field+"_"+period, "value": value,
                               "page": page.page, "sourceText": quote,
                               "requiresConfirmation": True})
            i += 3
            if field == "employer_pf" and i < len(lines) and compact(lines[i]) == "(includingadministrativecharges)":
                i += 1
        # Duplicate row labels make the table mapping ambiguous; do not
        # silently select one of the competing amounts.
        if not ambiguous:
            result.extend(page_rows)
    return result
