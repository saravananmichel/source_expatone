from app.vision_field_review_v55 import field_review

def test_matching_is_review_only():
    text = "Passport Number: X1234567\nDate of Birth: 12/04/1990\nDate of Expiry: 31/12/2030"
    result = field_review(text, text)
    assert all(v == "matching_review_only" for v in result["fields"].values())
    assert result["identityFieldsVerified"] is False
    assert "X1234567" not in str(result)

def test_conflict_requires_review():
    result = field_review("Passport Number: X1234567", "Passport Number: Y7654321")
    assert result["fields"]["passport_number"] == "conflict_requires_review"
    assert "Y7654321" not in str(result)

def test_unlabelled_is_not_evidence():
    result = field_review("X1234567", "X1234567")
    assert result["fields"]["passport_number"] == "insufficient_evidence"

def test_multiple_values_ambiguous():
    result = field_review("Passport Number: X1234567\nPassport Number: Y7654321", "Passport Number: X1234567")
    assert result["fields"]["passport_number"] == "ambiguous"

def test_date_format_not_inferred():
    result = field_review("Date of Birth: 12/04/1990", "Date of Birth: 1990-04-12")
    assert result["fields"]["date_of_birth"] == "conflict_requires_review"
