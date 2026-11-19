from app.document_preprocessing import paragraph_candidates
from app.schemas import Page


def page(number, text):
    return Page(page=number, text=text, width=600, height=800, extraction="native", blocks=[])


def test_repeated_margins_and_original_evidence():
    pages = [page(i, "Repeated header\nUnique heading " + str(i) +
                  "\nYour employment begins\non 10 June 2030.\n\n"
                  "Salary is payable monthly.\nTerms apply.\nUnique clause.\n"
                  "Repeated footer") for i in (1, 2)]
    candidates = paragraph_candidates(pages)
    for c in candidates:
        assert c["text"] == pages[c["page"]-1].text[c["start"]:c["end"]]
        assert "Repeated header" not in c["text"]
        assert "Repeated footer" not in c["text"]
    assert any("employment begins\non 10 June" in c["text"] for c in candidates)
    assert any("Salary is payable monthly." in c["text"] for c in candidates)


def test_long_line_is_bounded_and_preserved():
    source = "A" * 251
    candidates = paragraph_candidates([page(1, source)], max_chars=100)
    assert all(len(c["text"]) <= 100 for c in candidates)
    assert "".join(c["text"] for c in candidates) == source
