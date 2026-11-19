"""Conservative first-page document routing, independent of model inference.

Suggestions are not verified document types or proof of immigration status.
Ambiguous or unsupported layouts deliberately abstain.
"""
import re
from dataclasses import dataclass


@dataclass(frozen=True)
class Route:
    category: str
    subtype: str
    page: int
    evidence: tuple[str, ...]


def classify_document(pages) -> Route | None:
    if not pages:
        return None
    page = pages[0]
    if page.extractionConfidence < 0.80:
        return None
    lines = [line.strip() for line in page.text.splitlines() if line.strip()]

    def matching(pattern):
        return next((line for line in lines if re.search(pattern, line, re.I)), None)

    application = matching(r"^application\s*type\s*:\s*depend[ae]nt\s+pass\s*$")
    if application:
        corroboration = matching(r"^application\s*category\s*:\s*\S")
        if corroboration:
            return Route("Immigration Document", "dependant_pass_application", page.page,
                         (application, corroboration))
        return None

    # A reference to a pass in a letter is not an issued pass.
    if matching(r"\b(?:application|approval|supporting)\s+letter\b|^application\s+type\s*:"):
        return None
    employment = matching(r"^employment\s+pass(?:\s*\[[^\]]*\])?(?:\s*category\s*\([^)]*\)|\s*\([^)]*\))?\s*$")
    visa = matching(r"\bmultiple\s+entry\s+visa\b")
    validity = matching(r"\b(?:valid\s+until|expiry\s+date|date\s+of\s+expiry|good for any number of journeys to malaysia until)\b")
    if employment and visa and validity:
        return Route("Employment Pass", "employment_pass_with_visa", page.page,
                     (employment, visa, validity))
    return None
