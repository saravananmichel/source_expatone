"""Failure and configuration regressions for the production deployment."""
import asyncio
import httpx
import pytest
from fastapi.testclient import TestClient
from app.config import check_model, ollama_base_url, ollama_timeout, validate_config
from app.main import app


def test_url_precedence_and_legacy_compatibility(monkeypatch):
    monkeypatch.delenv('OLLAMA_BASE_URL', raising=False)
    monkeypatch.setenv('OLLAMA_URL', 'http://legacy:11434/')
    assert ollama_base_url() == 'http://legacy:11434'
    monkeypatch.setenv('OLLAMA_BASE_URL', 'http://ollama:11434/')
    assert ollama_base_url() == 'http://ollama:11434'


@pytest.mark.parametrize('url', ['ftp://ollama', 'http://user:secret@ollama', 'http://ollama?token=secret'])
def test_invalid_transport_config(monkeypatch, url):
    monkeypatch.setenv('OLLAMA_BASE_URL', url)
    with pytest.raises(ValueError): ollama_base_url()


def test_production_rejects_other_models(monkeypatch):
    monkeypatch.setenv('DOCUMENT_AI_ENVIRONMENT', 'Production')
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY', 'synthetic' * 8)
    monkeypatch.setenv('OLLAMA_MODEL', 'qwen3:4b-cloud')
    with pytest.raises(ValueError): validate_config()
    monkeypatch.setenv('OLLAMA_MODEL', 'qwen3:4b')
    monkeypatch.setenv('SUPPORT_MODEL', 'qwen3:4b')
    validate_config()


def test_model_readiness_checks_exact_tag(monkeypatch):
    monkeypatch.setenv('OLLAMA_MODEL', 'qwen3:4b')
    async def get(self, url):
        return httpx.Response(200, json={'models': [{'name': 'qwen3:8b'}]}, request=httpx.Request('GET', url))
    monkeypatch.setattr(httpx.AsyncClient, 'get', get)
    with pytest.raises(ValueError): asyncio.run(check_model())


def test_bounded_connection_timeout():
    timeout = ollama_timeout(900)
    assert timeout.connect == 5 and timeout.read == 900 and timeout.write == 30


def test_model_outage_is_retryable_and_private(monkeypatch):
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY', 'synthetic')
    monkeypatch.setenv('OLLAMA_MODEL', 'qwen3:4b')
    async def unavailable():
        raise httpx.ConnectError('private upstream details')
    monkeypatch.setattr('app.main.check_model', unavailable)
    with TestClient(app) as client:
        assert client.get('/health').status_code == 200
        assert client.get('/ready').status_code == 503
        response = client.post('/analyze', headers={'X-Service-Key': 'synthetic'},
                               files={'file': ('synthetic.pdf', b'private document', 'application/pdf')})
        assert response.status_code == 503
        assert 'private' not in response.text


def test_inference_outage_after_readiness(monkeypatch):
    from pdf_helpers import native_pdf
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY', 'synthetic')
    monkeypatch.setenv('OLLAMA_MODEL', 'qwen3:4b')
    async def ready(): pass
    async def unavailable(pages): raise httpx.ReadTimeout('private response')
    monkeypatch.setattr('app.main.check_model', ready)
    monkeypatch.setattr('app.main.reason', unavailable)
    with TestClient(app) as client:
        response = client.post('/analyze', headers={'X-Service-Key': 'synthetic'},
            files={'file': ('synthetic.pdf', native_pdf(['Salary: RM5000 per month. This is a synthetic employment document.']), 'application/pdf')})
        assert response.status_code == 503
        assert 'private' not in response.text


def test_startup_retries_are_bounded(monkeypatch):
    monkeypatch.setenv('DOCUMENT_AI_VALIDATE_STARTUP', 'true')
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY', 'synthetic')
    monkeypatch.setenv('OLLAMA_MODEL', 'qwen3:4b')
    monkeypatch.setattr('app.main.shutil.which', lambda name: '/synthetic/tesseract')
    attempts = []
    async def unavailable():
        attempts.append(1)
        raise httpx.ConnectError('private configuration')
    async def no_wait(seconds): pass
    monkeypatch.setattr('app.main.check_model', unavailable)
    monkeypatch.setattr('app.main.asyncio.sleep', no_wait)
    with pytest.raises(RuntimeError, match='dependencies unavailable'):
        with TestClient(app): pass
    assert len(attempts) == 6


@pytest.mark.parametrize('key', ['short', 'REPLACE_WITH_RANDOM_32_BYTE_KEY_LONG_PLACEHOLDER'])
def test_production_rejects_weak_service_keys(monkeypatch, key):
    monkeypatch.setenv('DOCUMENT_AI_ENVIRONMENT', 'Production')
    monkeypatch.setenv('OLLAMA_MODEL', 'qwen3:4b')
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY', key)
    with pytest.raises(ValueError, match='strong service key'): validate_config()
