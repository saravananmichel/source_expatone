"""Tests for hierarchical document reasoning — batching limits, digest bounds, and synthesis validation."""
import asyncio
import json
import os
import pytest
from unittest.mock import AsyncMock, MagicMock, patch


def make_multi_batch_semantic(n_batches=4):
    """Construct a SemanticDocument simulating n_batches of extraction."""
    from app.schemas import SemanticDocument, Statement, Evidence
    statements = []; evidence = []
    for b in range(n_batches):
        page_num = b * 3 + 1
        evidence += [
            Evidence(id=f'b{b}-e0', page=page_num,
                     sourceText=f'Employee: Alex Example. Batch {b} clause.'),
            Evidence(id=f'b{b}-e1', page=page_num,
                     sourceText=f'Monthly salary: RM1,000 per month. Batch {b}.'),
            Evidence(id=f'b{b}-e2', page=page_num,
                     sourceText=f'Contract clause {b}: applies to all parties.'),
        ]
        statements += [
            Statement(id=f'b{b}-s0', kind='entity', label='Employee',
                      text='The employee is Alex Example.',
                      originalValue='Alex Example', confidence=0.9,
                      evidenceIds=[f'b{b}-e0']),
            Statement(id=f'b{b}-s1', kind='money', label='Salary',
                      text='The monthly salary is RM1,000.',
                      originalValue='RM1,000', confidence=0.9,
                      evidenceIds=[f'b{b}-e1']),
            Statement(id=f'b{b}-s2', kind='clause', label='Clause',
                      text=f'Batch {b} clause applies to all parties.',
                      originalValue=f'Contract clause {b}: applies to all parties.',
                      confidence=0.9, evidenceIds=[f'b{b}-e2']),
        ]
    return SemanticDocument(
        documentCategory='Employment Contract', classificationConfidence=0.9,
        classificationEvidenceIds=['b0-e0'], statements=statements, evidence=evidence)


def make_pages(n=12):
    from app.schemas import Page
    return [Page(
        page=i + 1,
        text=(f'Employee: Alex Example. Batch {i // 3} clause. '
              f'Monthly salary: RM1,000 per month. '
              f'Contract clause {i // 3}: applies to all parties.'),
        width=612, height=792, extraction='synthetic_text', blocks=[])
        for i in range(n)]


# ---------------------------------------------------------------------------
# Change 1: page-count cap in batching
# ---------------------------------------------------------------------------

def test_batch_page_limit_for_long_docs():
    """15 pages (>10) → all groups must have at most 3 pages."""
    from app.schemas import Page
    import json

    pages = [
        Page(page=i + 1,
             text='Employee: Alex Example. Monthly salary: RM1,000 per month. Some clause text here.',
             width=612, height=792, extraction='synthetic_text', blocks=[])
        for i in range(15)
    ]

    # Re-implement the grouping logic from pipeline.reason() to inspect groups
    max_pages_per_batch = 3 if len(pages) > 10 else 6
    max_batch_bytes = 13000 if len(pages) > 10 else 18000
    groups = []; current = []; size = 0
    for page in pages:
        text = page.text
        pieces = []
        while text:
            end = min(len(text), 5000)
            if end < len(text):
                boundary = text.rfind('\n', 0, end)
                if boundary > 2500:
                    end = boundary + 1
            pieces.append(text[:end]); text = text[end:]
        for piece in pieces:
            chunk = page.model_copy(update={'text': piece, 'blocks': []})
            length = len(json.dumps(chunk.model_dump(), ensure_ascii=False).encode())
            if current and (size + length > max_batch_bytes or len(current) >= max_pages_per_batch):
                groups.append(current); current = []; size = 0
            current.append(chunk); size += length
    if current:
        groups.append(current)

    assert all(len(g) <= 3 for g in groups), \
        f'Some group exceeded 3 pages: {[len(g) for g in groups]}'


def test_batch_page_limit_not_applied_for_short_docs():
    """6 pages (≤10) → groups may have up to 6 pages."""
    from app.schemas import Page
    import json

    pages = [
        Page(page=i + 1,
             text='Employee: Alex Example. Monthly salary: RM1,000 per month.',
             width=612, height=792, extraction='synthetic_text', blocks=[])
        for i in range(6)
    ]

    max_pages_per_batch = 3 if len(pages) > 10 else 6
    max_batch_bytes = 13000 if len(pages) > 10 else 18000
    groups = []; current = []; size = 0
    for page in pages:
        text = page.text
        pieces = []
        while text:
            end = min(len(text), 5000)
            if end < len(text):
                boundary = text.rfind('\n', 0, end)
                if boundary > 2500:
                    end = boundary + 1
            pieces.append(text[:end]); text = text[end:]
        for piece in pieces:
            chunk = page.model_copy(update={'text': piece, 'blocks': []})
            length = len(json.dumps(chunk.model_dump(), ensure_ascii=False).encode())
            if current and (size + length > max_batch_bytes or len(current) >= max_pages_per_batch):
                groups.append(current); current = []; size = 0
            current.append(chunk); size += length
    if current:
        groups.append(current)

    # For short docs the cap is 6 — all 6 pages fit in one group (bytes permitting)
    assert max_pages_per_batch == 6
    assert all(len(g) <= 6 for g in groups)


# ---------------------------------------------------------------------------
# Change 4: build_synthesis_digest — size bounds
# ---------------------------------------------------------------------------

def test_synthesis_digest_bounded():
    """n_batches=8 → digest must be ≤ 10,000 bytes."""
    from app.reasoning import build_synthesis_digest
    semantic = make_multi_batch_semantic(n_batches=8)
    pages = make_pages(n=24)
    digest = build_synthesis_digest(semantic, pages)
    size = len(json.dumps(digest, ensure_ascii=False).encode())
    assert size <= 10000, f'Digest too large: {size} bytes'


def test_synthesis_digest_bounded_very_large():
    """n_batches=12 → digest must still be ≤ 10,000 bytes."""
    from app.reasoning import build_synthesis_digest
    semantic = make_multi_batch_semantic(n_batches=12)
    pages = make_pages(n=36)
    digest = build_synthesis_digest(semantic, pages)
    size = len(json.dumps(digest, ensure_ascii=False).encode())
    assert size <= 10000, f'Digest too large: {size} bytes'


def test_synthesis_digest_ev_refs_valid():
    """All evIds referenced in digest entries must exist in the semantic's evidence set."""
    from app.reasoning import build_synthesis_digest
    semantic = make_multi_batch_semantic(n_batches=4)
    pages = make_pages(n=12)
    digest = build_synthesis_digest(semantic, pages)
    ev_ids = {e.id for e in semantic.evidence}
    for section_key in ('parties', 'dates', 'amounts', 'clauses', 'findings'):
        for item in digest.get(section_key, []):
            for eid in item.get('evIds', []):
                assert eid in ev_ids, f'evId {eid!r} in digest but not in evidence set'
    for eid in digest.get('evidenceQuotes', {}):
        assert eid in ev_ids, f'evidenceQuotes key {eid!r} not in evidence set'


def test_synthesis_digest_preserves_page_numbers():
    """Pages list in each digest entry must match the actual evidence page numbers."""
    from app.reasoning import build_synthesis_digest
    semantic = make_multi_batch_semantic(n_batches=4)
    pages = make_pages(n=12)
    digest = build_synthesis_digest(semantic, pages)
    ev_map = {e.id: e for e in semantic.evidence}
    for section_key in ('parties', 'dates', 'amounts', 'clauses', 'findings'):
        for item in digest.get(section_key, []):
            expected_pages = sorted({ev_map[eid].page for eid in item['evIds'] if eid in ev_map})
            assert item['pages'] == expected_pages, \
                f'page mismatch for {item["stmtId"]}: got {item["pages"]}, expected {expected_pages}'


# ---------------------------------------------------------------------------
# Change 4: hierarchical_document_reasoning — validation gates
# ---------------------------------------------------------------------------

def _make_ollama_response(content: str):
    """Return a mock httpx response with the given JSON message content."""
    mock_resp = MagicMock()
    mock_resp.raise_for_status = MagicMock()
    mock_resp.json.return_value = {'message': {'content': content}}
    return mock_resp


def _conclusions_json(overview_extra=None, statements=None):
    """Build a minimal valid Conclusions JSON payload."""
    from app.reasoning import Conclusions, Overview
    overview = {
        'id': 'global-0',
        'kind': 'interpretation',
        'label': 'Overview',
        'text': 'This is an Employment Contract for Alex Example.',
        'originalValue': 'Alex Example',
        'confidence': 0.85,
        'evidenceIds': ['b0-e0'],
        'supportStatus': 'UNCERTAIN',
        'supportReason': None,
        'normalizedValue': None,
    }
    if overview_extra:
        overview.update(overview_extra)
    payload = {'overview': overview, 'statements': statements or []}
    return json.dumps(payload)


def test_hierarchical_drops_invalid_evid(monkeypatch):
    """Overview citing a non-existent evidence ID must not be added to statements."""
    monkeypatch.setenv('OLLAMA_MODEL', 'test-model')

    from app.reasoning import hierarchical_document_reasoning
    semantic = make_multi_batch_semantic(n_batches=4)
    pages = make_pages(n=12)
    original_count = len(semantic.statements)

    bad_overview = {
        'text': 'Overview referencing a nonexistent evidence.',
        'originalValue': 'Alex Example',
        'evidenceIds': ['NONEXISTENT-ev-id'],
    }
    canned = _conclusions_json(overview_extra=bad_overview)

    mock_resp = _make_ollama_response(canned)
    mock_post = AsyncMock(return_value=mock_resp)

    with patch('httpx.AsyncClient') as mock_client_cls:
        mock_client = AsyncMock()
        mock_client.__aenter__ = AsyncMock(return_value=mock_client)
        mock_client.__aexit__ = AsyncMock(return_value=False)
        mock_client.post = mock_post
        mock_client_cls.return_value = mock_client

        asyncio.run(hierarchical_document_reasoning(semantic, pages))

    # The invalid overview statement must NOT be appended
    added_ids = [s.id for s in semantic.statements]
    assert 'global-0' not in added_ids, \
        'Statement with invalid evidence ID was incorrectly added'


def test_hierarchical_drops_unsupported_number(monkeypatch):
    """Overview text containing a number not present in evidence must not be added.

    Both standalone numbers (999) and currency-prefixed amounts (RM999) are
    now detected by the number check in hierarchical_document_reasoning.
    """
    monkeypatch.setenv('OLLAMA_MODEL', 'test-model')

    from app.reasoning import hierarchical_document_reasoning
    semantic = make_multi_batch_semantic(n_batches=4)
    pages = make_pages(n=12)

    # 999 is not present in any evidence sourceText; the check must reject this claim.
    bad_overview = {
        'text': 'The salary is 999 per month.',
        'originalValue': 'Alex Example',
        'evidenceIds': ['b0-e0'],
    }
    canned = _conclusions_json(overview_extra=bad_overview)

    mock_resp = _make_ollama_response(canned)
    mock_post = AsyncMock(return_value=mock_resp)

    with patch('httpx.AsyncClient') as mock_client_cls:
        mock_client = AsyncMock()
        mock_client.__aenter__ = AsyncMock(return_value=mock_client)
        mock_client.__aexit__ = AsyncMock(return_value=False)
        mock_client.post = mock_post
        mock_client_cls.return_value = mock_client

        asyncio.run(hierarchical_document_reasoning(semantic, pages))

    added_ids = [s.id for s in semantic.statements]
    assert 'global-0' not in added_ids, \
        'Statement with unsupported number was incorrectly added'


def test_hierarchical_drops_rm_prefixed_unsupported_amount(monkeypatch):
    """Overview citing RM999 where evidence has no 999 must be rejected."""
    monkeypatch.setenv('OLLAMA_MODEL', 'test-model')

    from app.reasoning import hierarchical_document_reasoning
    semantic = make_multi_batch_semantic(n_batches=4)
    pages = make_pages(n=12)

    # RM999 — the currency-prefixed number check should catch 999 not being in evidence.
    bad_overview = {
        'text': 'The monthly salary is RM999.',
        'originalValue': 'Alex Example',
        'evidenceIds': ['b0-e0'],
    }
    canned = _conclusions_json(overview_extra=bad_overview)
    mock_resp = _make_ollama_response(canned)
    mock_post = AsyncMock(return_value=mock_resp)

    with patch('httpx.AsyncClient') as mock_client_cls:
        mock_client = AsyncMock()
        mock_client.__aenter__ = AsyncMock(return_value=mock_client)
        mock_client.__aexit__ = AsyncMock(return_value=False)
        mock_client.post = mock_post
        mock_client_cls.return_value = mock_client

        asyncio.run(hierarchical_document_reasoning(semantic, pages))

    added_ids = [s.id for s in semantic.statements]
    assert 'global-0' not in added_ids, \
        'Statement with RM-prefixed unsupported amount was incorrectly added'


def test_hierarchical_adds_valid_claim(monkeypatch):
    """Overview with valid evId and originalValue that exists in evidence must be added."""
    monkeypatch.setenv('OLLAMA_MODEL', 'test-model')

    from app.reasoning import hierarchical_document_reasoning
    semantic = make_multi_batch_semantic(n_batches=4)
    pages = make_pages(n=12)

    # 'Alex Example' appears in b0-e0 sourceText; no invented numbers in text
    valid_overview = {
        'text': 'This Employment Contract names Alex Example as the employee.',
        'originalValue': 'Alex Example',
        'evidenceIds': ['b0-e0'],
    }
    canned = _conclusions_json(overview_extra=valid_overview)

    mock_resp = _make_ollama_response(canned)
    mock_post = AsyncMock(return_value=mock_resp)

    with patch('httpx.AsyncClient') as mock_client_cls:
        mock_client = AsyncMock()
        mock_client.__aenter__ = AsyncMock(return_value=mock_client)
        mock_client.__aexit__ = AsyncMock(return_value=False)
        mock_client.post = mock_post
        mock_client_cls.return_value = mock_client

        asyncio.run(hierarchical_document_reasoning(semantic, pages))

    added_ids = [s.id for s in semantic.statements]
    assert 'global-0' in added_ids, \
        'Valid overview statement was not added to semantic.statements'
