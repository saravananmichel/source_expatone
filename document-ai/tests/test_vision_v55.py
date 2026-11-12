"""V5.5 vision probe safety tests; no external Ollama service required."""
import pytest

from app.vision_v55 import VisionUnavailable, vision_model, vision_payload

PNG = b"\x89PNG\r\n\x1a\n" + b"synthetic"
JPEG = b"\xff\xd8\xff" + b"synthetic"


def test_disabled_by_default(monkeypatch):
    monkeypatch.delenv("DOCUMENT_AI_V55_VISION_ENABLED", raising=False)
    monkeypatch.setenv("DOCUMENT_AI_V55_VISION_MODEL", "llava:7b")
    with pytest.raises(VisionUnavailable, match="vision_disabled"):
        vision_model()


def test_requires_separate_explicit_model(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_V55_VISION_ENABLED", "true")
    monkeypatch.setenv("OLLAMA_MODEL", "qwen3:4b")
    monkeypatch.setenv("DOCUMENT_AI_V55_VISION_MODEL", "qwen3:4b")
    with pytest.raises(VisionUnavailable, match="vision_requires_separate_model"):
        vision_model()
    monkeypatch.setenv("DOCUMENT_AI_V55_VISION_MODEL", "llava:7b")
    assert vision_model() == "llava:7b"


def test_payload_is_private_and_bounded():
    payload = vision_payload(PNG, "image/png", "llava:7b")
    assert payload["stream"] is False
    assert payload["options"]["temperature"] == 0
    assert payload["messages"][0]["images"]
    assert "no guesses" in payload["messages"][0]["content"]


@pytest.mark.parametrize("data,mime", [
    (b"", "image/png"), (b"notpng", "image/png"),
    (PNG, "application/pdf"), (JPEG, "image/png"),
    (PNG + b"x" * (2 * 1024 * 1024), "image/png"),
])
def test_rejects_invalid_or_oversized_images(data, mime):
    with pytest.raises(VisionUnavailable, match="invalid_vision_image"):
        vision_payload(data, mime, "llava:7b")
