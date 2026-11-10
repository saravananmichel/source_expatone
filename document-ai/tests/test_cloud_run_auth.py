"""Regression tests for keyless, opt-in Cloud Run Ollama IAM transport."""
import asyncio
import pytest
from app.cloud_run_auth import cloud_run_audience, ollama_auth_headers
from app.config import ollama_base_url


def test_auth_disabled_by_default(monkeypatch):
    monkeypatch.delenv("OLLAMA_CLOUD_RUN_AUDIENCE", raising=False)
    assert cloud_run_audience() is None
    assert asyncio.run(ollama_auth_headers()) == {}


@pytest.mark.parametrize("audience", [
    "http://ollama-dev.run.app",
    "https://example.com",
    "https://ollama-dev.run.app.evil.com",
    "https://ollama-dev.run.app/other",
    "https://ollama-dev.run.app?x=1",
    "https://user:password@ollama-dev.run.app",
])
def test_reject_invalid_audience(monkeypatch, audience):
    monkeypatch.setenv("OLLAMA_CLOUD_RUN_AUDIENCE", audience)
    with pytest.raises(ValueError):
        cloud_run_audience()


def test_production_accepts_matching_authenticated_cloud_run(monkeypatch):
    url = "https://expatone-ollama-dev-123.asia-southeast1.run.app"
    monkeypatch.setenv("DOCUMENT_AI_ENVIRONMENT", "Production")
    monkeypatch.setenv("OLLAMA_BASE_URL", url)
    monkeypatch.setenv("OLLAMA_CLOUD_RUN_AUDIENCE", url)
    assert ollama_base_url() == url


def test_production_rejects_unauthenticated_cloud_run(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_ENVIRONMENT", "Production")
    monkeypatch.setenv("OLLAMA_BASE_URL", "https://expatone-ollama-dev-123.asia-southeast1.run.app")
    monkeypatch.delenv("OLLAMA_CLOUD_RUN_AUDIENCE", raising=False)
    with pytest.raises(ValueError):
        ollama_base_url()


def test_identity_token_attached(monkeypatch):
    from google.oauth2 import id_token
    monkeypatch.setenv("OLLAMA_CLOUD_RUN_AUDIENCE", "https://expatone-ollama-dev-123.asia-southeast1.run.app")
    observed = []
    def fake_fetch(request, audience):
        observed.append(audience)
        return "test-token"
    monkeypatch.setattr(id_token, "fetch_id_token", fake_fetch)
    assert asyncio.run(ollama_auth_headers()) == {"Authorization": "Bearer test-token"}
    assert observed == ["https://expatone-ollama-dev-123.asia-southeast1.run.app"]
