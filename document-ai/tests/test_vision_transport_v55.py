"""No Vision request may silently use the text Ollama endpoint."""
import pytest
from app.vision_transport_v55 import vision_endpoint
from app.vision_v55 import VisionUnavailable


def test_missing_vision_endpoint_fails_closed(monkeypatch):
    monkeypatch.delenv("VISION_OLLAMA_BASE_URL", raising=False)
    monkeypatch.delenv("VISION_OLLAMA_CLOUD_RUN_AUDIENCE", raising=False)
    with pytest.raises(VisionUnavailable, match="vision_endpoint_not_configured"):
        vision_endpoint()


def test_separate_authenticated_endpoint(monkeypatch):
    url = "https://expatone-vision-dev.asia-southeast1.run.app"
    monkeypatch.setenv("VISION_OLLAMA_BASE_URL", url)
    monkeypatch.setenv("VISION_OLLAMA_CLOUD_RUN_AUDIENCE", url)
    monkeypatch.setenv("OLLAMA_BASE_URL", "https://expatone-text-dev.asia-southeast1.run.app")
    assert vision_endpoint() == (url, url)


@pytest.mark.parametrize("url", [
    "http://expatone-vision-dev.asia-southeast1.run.app",
    "https://example.com",
    "https://user:pass@expatone-vision-dev.asia-southeast1.run.app",
    "https://expatone-vision-dev.asia-southeast1.run.app/other",
])
def test_rejects_unsafe_endpoint(monkeypatch, url):
    monkeypatch.setenv("VISION_OLLAMA_BASE_URL", url)
    monkeypatch.setenv("VISION_OLLAMA_CLOUD_RUN_AUDIENCE", url)
    with pytest.raises(VisionUnavailable, match="invalid_vision_endpoint"):
        vision_endpoint()


def test_audience_must_match(monkeypatch):
    monkeypatch.setenv("VISION_OLLAMA_BASE_URL", "https://vision-one.run.app")
    monkeypatch.setenv("VISION_OLLAMA_CLOUD_RUN_AUDIENCE", "https://vision-two.run.app")
    with pytest.raises(VisionUnavailable, match="vision_audience_mismatch"):
        vision_endpoint()


def test_never_falls_back_to_text_model_endpoint(monkeypatch):
    url = "https://expatone-text-dev.asia-southeast1.run.app"
    monkeypatch.setenv("VISION_OLLAMA_BASE_URL", url)
    monkeypatch.setenv("VISION_OLLAMA_CLOUD_RUN_AUDIENCE", url)
    monkeypatch.setenv("OLLAMA_BASE_URL", url)
    with pytest.raises(VisionUnavailable, match="vision_endpoint_must_be_separate"):
        vision_endpoint()
