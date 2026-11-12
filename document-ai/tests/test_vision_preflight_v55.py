"""V5.5 Vision preflight never uploads documents."""
import asyncio
from unittest.mock import AsyncMock

from app import vision_preflight_v55 as module


def test_disabled_does_not_make_network_call(monkeypatch):
    monkeypatch.delenv("DOCUMENT_AI_V55_VISION_ENABLED", raising=False)
    result = asyncio.run(module.vision_preflight())
    assert result["status"] == "vision_disabled"
    assert result["inferenceVerified"] is False


def test_listed_model_does_not_claim_inference(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_V55_VISION_ENABLED", "true")
    monkeypatch.setenv("DOCUMENT_AI_V55_VISION_MODEL", "llava:latest")
    monkeypatch.setenv("OLLAMA_MODEL", "qwen3:4b")
    class Response:
        def raise_for_status(self): pass
        def json(self): return {"models": [{"name": "llava:latest"}]}
    class Client:
        async def __aenter__(self): return self
        async def __aexit__(self, *args): pass
        async def get(self, url, headers):
            assert url.endswith("/api/tags")
            return Response()
    monkeypatch.setattr(module.httpx, "AsyncClient", lambda **kwargs: Client())
    monkeypatch.setattr(module, "ollama_auth_headers", AsyncMock(return_value={}))
    result = asyncio.run(module.vision_preflight())
    assert result == {"status": "model_listed", "modelAvailable": True, "inferenceVerified": False}


def test_unlisted_model_is_not_ready(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_V55_VISION_ENABLED", "true")
    monkeypatch.setenv("DOCUMENT_AI_V55_VISION_MODEL", "llava:latest")
    monkeypatch.setenv("OLLAMA_MODEL", "qwen3:4b")
    class Response:
        def raise_for_status(self): pass
        def json(self): return {"models": []}
    class Client:
        async def __aenter__(self): return self
        async def __aexit__(self, *args): pass
        async def get(self, url, headers): return Response()
    monkeypatch.setattr(module.httpx, "AsyncClient", lambda **kwargs: Client())
    monkeypatch.setattr(module, "ollama_auth_headers", AsyncMock(return_value={}))
    result = asyncio.run(module.vision_preflight())
    assert result["status"] == "model_not_listed"
