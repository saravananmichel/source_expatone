"""Strict ICAO TD3 MRZ candidates from OCR text. No inferred values.

Only accept two intact 44-character lines with valid check digits.
MRZ fields are review-only suggestions and never override visible fields.
"""
import re

MRZ_LINE = re.compile(r"^[A-Z0-9<]{44}$")
WEIGHTS = (7, 3, 1)


def check_digit(value: str) -> str:
    total = 0
    for index, char in enumerate(value):
        number = 0 if char == "<" else int(char) if char.isdigit() else ord(char) - ord("A") + 10
        total += number * WEIGHTS[index % 3]
    return str(total % 10)


def _suggest(field: str, value: str, source: str, page: int) -> dict:
    return {"field": field, "value": value, "page": page,
            "sourceText": source, "requiresConfirmation": True}


def mrz_fields(pages) -> list[dict]:
    result = []
    for page in pages:
        lines = [line.strip().upper().replace(" ", "") for line in page.text.splitlines()]
        for index in range(len(lines) - 1):
            first, second = lines[index:index + 2]
            if not (MRZ_LINE.fullmatch(first) and MRZ_LINE.fullmatch(second)
                    and first.startswith("P<")):
                continue
            # Passport number, birth, expiry and optional composite check digits.
            if not (second[9] == check_digit(second[:9])
                    and second[19] == check_digit(second[13:19])
                    and second[27] == check_digit(second[21:27])
                    and second[43] == check_digit(second[:10] + second[13:20] + second[21:43])):
                continue
            number = second[:9].rstrip("<")
            if not number or not re.fullmatch(r"[A-Z0-9]{6,9}", number):
                continue
            result.append(_suggest("passport_number", number, second, page.page))
            names = first[5:].split("<<", 1)
            if len(names) == 2:
                surname = names[0].replace("<", " ").strip()
                given = names[1].replace("<", " ").strip()
                if surname:
                    # Display names are reconstructed from MRZ separators, so
                    # their sourceText is the exact MRZ line, not an exact substring.
                    # Do not add these to strict literal-substring suggestions.
                    pass
            # Dates are YYMMDD; without century evidence, avoid guessing.
            # Expose only the fully validated passport number for now.
            return result
    return result
