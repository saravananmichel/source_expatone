import json
from app.section_selection import select_sections
from app.schemas import Page

def page(i, text):
    return Page(page=i, text=text, width=600, height=800, extraction="native", blocks=[])

def test_prioritized_sections_and_budget():
    pages = [page(1, "General information. " * 100),
             page(2, "Employment salary and compensation. Notice period of 90 days.")]
    result = select_sections(pages, byte_budget=600)
    assert result[0]["page"] == 2
    assert len(json.dumps(result, ensure_ascii=False).encode("utf-8")) <= 600
    assert all(r["text"] in pages[r["page"]-1].text for r in result)
    assert len({r["id"] for r in result}) == len(result)

def test_empty_or_tiny_budget():
    assert select_sections([]) == []
    assert select_sections([page(1, "Employment contract")], byte_budget=2) == []
