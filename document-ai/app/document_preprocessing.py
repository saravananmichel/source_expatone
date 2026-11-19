"""Source-preserving preprocessing for private document reasoning.

Candidates are for ranking, never replacements for original page text.
"""
from collections import defaultdict
import re


def paragraph_candidates(pages, max_chars=1000):
    if max_chars < 100:
        raise ValueError("max_chars must be at least 100")
    margins = defaultdict(set)
    layouts = []
    for page in pages:
        lines = list(re.finditer(r"[^\n]+", page.text))
        lines = [m for m in lines if m.group().strip()]
        layouts.append((page, lines))
        if len(lines) < 8:
            continue
        for m in lines[:2] + lines[-2:]:
            key = " ".join(m.group().split()).casefold()
            margins[key].add(page.page)
    repeated = {key for key, numbers in margins.items() if len(numbers) >= 2}
    result = []
    for page, lines in layouts:
        start = end = None

        def emit():
            if start is not None:
                result.append({"page": page.page, "start": start, "end": end,
                               "text": page.text[start:end]})

        for i, m in enumerate(lines):
            key = " ".join(m.group().split()).casefold()
            margin = len(lines) >= 8 and (i < 2 or i >= len(lines)-2)
            if margin and key in repeated:
                emit()
                start = end = None
                continue
            if start is not None and (
                "\n\n" in page.text[end:m.start()].replace("\r", "")
                or m.end() - start > max_chars
            ):
                emit()
                start = end = None
            if start is None:
                start = m.start()
            end = m.end()
            while end - start > max_chars:
                boundary = start + max_chars
                result.append({"page": page.page, "start": start, "end": boundary,
                               "text": page.text[start:boundary]})
                start = boundary
        emit()
    return result
