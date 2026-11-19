import pytest
from app.general_classification import general_classification
from app.schemas import Page

def page(text, confidence=.95):
    return Page(page=1, text=text, width=600, height=800, extraction="native",
                extractionConfidence=confidence, blocks=[])

@pytest.mark.parametrize("title,category,subtype", [
    ("Offer of Employment", "Offer Letter", "employment_offer"),
    ("Subject: Change in Designation", "Other", "designation_change"),
    ("Relieving Letter", "Other", "relieving_letter"),
    ("Account Opening Form", "Financial Document", "account_opening"),
    ("Background Verification Consent Form", "Other", "consent_authorization"),
])
def test_titles(title, category, subtype):
    route = general_classification([page(title)])
    assert (route.category, route.subtype, route.evidence) == (category, subtype, (title,))

def test_abstention():
    assert general_classification([page("Offer Letter", .5)]) is None
    assert general_classification([page("Offer Letter\nRelieving Letter")]) is None
    assert general_classification([page("Please refer to your offer letter.")]) is None
