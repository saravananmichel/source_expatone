import httpx
import pytest
from app import ollama


@pytest.mark.asyncio
async def test_dev_chat_tuning(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_PROFILE", "development")
    monkeypatch.delenv("OLLAMA_NUM_THREAD", raising=False)
    monkeypatch.delenv("OLLAMA_KEEP_ALIVE", raising=False)
    monkeypatch.setattr(ollama, "ollama_base_url", lambda: "http://ollama")
    async def headers():
        return {}
    monkeypatch.setattr(ollama, "ollama_auth_headers", headers)
    async def handler(request):
        import json
        payload = json.loads(request.content)
        assert payload["options"]["num_thread"] == 2
        assert payload["keep_alive"] == "30m"
        return httpx.Response(200, json={"done": True, "done_reason": "stop", "eval_count": 1, "eval_duration": 1000000000})
    async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as client:
        response = await ollama.post_chat(client, {"model": "qwen3:0.6b", "options": {}})
    assert response.status_code == 200
