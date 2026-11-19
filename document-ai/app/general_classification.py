"""Review-only document category and subtype hints."""
import re
from .document_router import Route

RULES = (
    ("Offer Letter", "employment_offer", r"(?:offer of employment|employment offer(?: letter)?|offer letter|letter of offer)"),
    ("Other", "designation_change", r"(?:change in designation|designation change)(?: letter)?"),
    ("Other", "relieving_letter", r"(?:relieving letter|letter of relieving)"),
    ("Financial Document", "account_opening", r"(?:(?:bank|personal|corporate|savings|current)\s+)?account opening(?: application)? form"),
    ("Other", "consent_authorization", r"(?:background verification|background check)\s+(?:consent|authorization|authorisation)(?: form| letter)?"),
    ("Employment Contract", "employment_contract", r"(?:employment contract|contract of employment)"),
    ("Bank Statement", "bank_statement", r"bank statement"),
)

def general_classification(pages):
    if not pages or pages[0].extractionConfidence < .80:
        return None
    page = pages[0]
    lines = [line.strip() for line in page.text.splitlines() if line.strip()][:20]
    candidates = {}
    for line in lines:
        title = re.sub(r"^subject\s*:\s*", "", line, flags=re.I)
        for category, subtype, pattern in RULES:
            if re.fullmatch(pattern, title, re.I):
                candidates.setdefault(subtype, Route(category, subtype, page.page, (line,)))
    return next(iter(candidates.values())) if len(candidates) == 1 else None
