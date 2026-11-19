from app.private_fallback import _plausible_field, source_fallback
from app.schemas import Page


def test_invalid_identity_and_dates_rejected():
    assert not _plausible_field("Name", 'Q"t Signature:')
    assert not _plausible_field("Date", "Zl,ld[ S")
    assert not _plausible_field("Date", "31/02/2025")
    assert _plausible_field("Date", "6 June 2024")
    assert _plausible_field("Name", "Sample Person")


def test_repeated_fields_do_not_consume_fallback_budget():
    pages = [
        Page(page=i, text="Digital signature ID: SAMPLE123\nDesignation: Engineer",
             width=600, height=800, extraction="native", blocks=[])
        for i in range(1, 7)
    ]
    result = source_fallback(pages)
    assert len(result.statements) == 2
    assert len(result.evidence) == 2
