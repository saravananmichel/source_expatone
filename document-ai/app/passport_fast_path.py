"""DEV-only eligibility gate; no inferred or automatically verified identities."""
from datetime import datetime
from .passport_mrz import mrz_fields
from .structured_analysis import passport_category

REQUIRED = {"passport_number", "surname", "given_names", "date_of_birth",
            "issue_date", "expiry_date"}

def eligible_passport(pages, fields):
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
    if not any(f["value"] == values["passport_number"] for f in mrz_fields([first])):
        return False
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
    return dates[0] < dates[1] < dates[2]
