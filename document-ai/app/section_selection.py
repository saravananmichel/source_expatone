"""Prioritize exact source candidates without modifying original evidence."""
import json
import re
from .document_preprocessing import paragraph_candidates

SIGNALS = re.compile(r"\b(?:employment|appointment|offer|salary|compensation|pay|notice|termination|effective|commence|acceptance|condition|obligation|consent|signature|deadline|validity|benefits|probation|confidentiality|declaration)\b", re.I)

def select_sections(pages, byte_budget=2400, max_sections=12):
    candidates = paragraph_candidates(pages, max_chars=450)
    ranked = sorted(enumerate(candidates), key=lambda pair: (
        -(min(5, len(set(m.group().lower() for m in SIGNALS.finditer(pair[1]["text"]))))
          + (2 if pair[1]["page"] == pages[0].page and pair[0] == 0 else 0)),
        pair[0]))
    selected = []
    seen = set()
    for _, c in ranked:
        key = " ".join(c["text"].split()).casefold()
        if key in seen:
            continue
        entry = {"id": "p"+str(c["page"])+"-"+str(c["start"]),
                 "page": c["page"], "text": c["text"]}
        if len(json.dumps(selected + [entry], ensure_ascii=False).encode("utf-8")) > byte_budget:
            continue
        if len(selected) >= max_sections:
            break
        selected.append(entry)
        seen.add(key)
    return selected
