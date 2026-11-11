"""API contract regression: candidate extraction must not invoke Ollama."""
import json
from fastapi.testclient import TestClient
from app.main import app


def test_extract_returns_evidence_candidates_without_model(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_SERVICE_KEY", "test-key")
    async def model_must_not_run():
        raise AssertionError("Ollama must not run for /extract")
    monkeypatch.setattr("app.main.check_model", model_must_not_run)

    class FakeProcess:
        returncode = 0

        async def communicate(self, data):
            assert data == b"%PDF-synthetic"
            pages = [{
                "page": 1, "text": "Due Date: 15/12/2026\\nAmount Due: RM 1,250.60",
                "width": 100, "height": 100, "extraction": "native",
                "blocks": [],
            }]
            return json.dumps(pages).encode(), b""

    async def fake_spawn(*args, **kwargs):
        return FakeProcess()

    monkeypatch.setattr("app.main.asyncio.create_subprocess_exec", fake_spawn)
    with TestClient(app) as client:
        response = client.post(
            "/extract",
            headers={"X-Service-Key": "test-key"},
            files={"file": ("bill.pdf", b"%PDF-synthetic", "application/pdf")},
        )
    assert response.status_code == 200
    body = response.json()
    assert body["pageCount"] == 1
    assert body["extractedText"].startswith("Due Date:")
    assert {candidate["field"] for candidate in body["fieldCandidates"]} == {
        "due_date", "amount_due"
    }
    assert all(candidate["requiresConfirmation"] for candidate in body["fieldCandidates"])


def test_extract_requires_auth_for_candidates(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_SERVICE_KEY", "test-key")
    with TestClient(app) as client:
        response = client.post(
            "/extract",
            files={"file": ("bill.pdf", b"%PDF-synthetic", "application/pdf")},
        )
    assert response.status_code == 401
