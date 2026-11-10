"""Corrupted text must not become a supported source fact."""
import pytest
from app.schemas import Page
from app.private_fallback import source_fallback, _plausible_field


def test_malformed_labels_are_not_fields():
    assert not _plausible_field("cl<1i<liPrrf", "BEARER TO PASS FREELY")
    assert not _plausible_field("li;;T11lr.", "PRESIDENT OF THE REPUBLIC")


def test_garbled_page_fails_closed_without_verified_facts():
    text = ("REQUIRE IN THE NAME OF THE PRESIDENT OF THE REPUBLIC. "
            "BEARER TO PASS FREELY WITHOUT LET OR HINDRANCE. "
            "<li;;T11lr.: srr;vlt # fiRqr; p;# "
            "cl<1i<liPrrf: ;r2;j7#an:1-jfT/~: "
            "cl<1i<liPrrf: ;r2;j7#an:1-jfT/~: ")
    page = Page(page=1, text=text, width=600, height=800,
                extraction="native", blocks=[])
    with pytest.raises(ValueError, match="no_reliable_source_text"):
        source_fallback([page])
