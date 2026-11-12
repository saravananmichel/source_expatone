"""V5.5 conservative TD3 MRZ audit; never auto-populates identity fields.

Use only after OCR. Invalid, partial, or conflicting MRZ data is not trusted.
Dates retain YYMMDD because century cannot be established from MRZ alone.
"""
import re
from .passport_mrz import MRZ_LINE, check_digit


def audit_td3(pages):
    """Return a review-only diagnostic for the first valid TD3 MRZ on page 1.

    No raw document text is logged or persisted by this helper.
    """
    for page in pages:
        if page.page != 1:
            continue
        lines = [line.strip().upper() for line in page.text.splitlines()]
        for first, second in zip(lines, lines[1:]):
            if not (MRZ_LINE.fullmatch(first) and MRZ_LINE.fullmatch(second)
                    and first.startswith("P<")):
                continue
            checks = {
                "passport_number": second[9] == check_digit(second[:9]),
                "birth_date": second[19] == check_digit(second[13:19]),
                "expiry_date": second[27] == check_digit(second[21:27]),
                "composite": second[43] == check_digit(
                    second[:10] + second[13:20] + second[21:43]),
            }
            if not all(checks.values()):
                return {"status": "invalid_checksum", "checks": checks,
                        "requiresConfirmation": True}
            number = second[:9].rstrip("<")
            if not re.fullmatch(r"[A-Z0-9]{6,9}", number):
                return {"status": "invalid_number", "requiresConfirmation": True}
            return {
                "status": "valid_review_only",
                "page": page.page,
                "passportNumber": number,
                "birthYYMMDD": second[13:19],
                "expiryYYMMDD": second[21:27],
                "nationalityCode": second[10:13],
                "checks": checks,
                "requiresConfirmation": True,
                "datesCenturyResolved": False,
            }
    return {"status": "not_found", "requiresConfirmation": True}
