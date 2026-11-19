"""V5.5 readiness tests; no real document or network access."""
from app.vision_readiness import vision_configuration


def test_vision_disabled_by_default(monkeypatch):
    monkeypatch.delenv("DOCUMENT_AI_VISION_ENABLED", raising=False)
    monkeypatch.delenv("DOCUMENT_AI_VISION_MODEL", raising=False)
    assert vision_configuration() == {
        "enabled": False, "ready": False, "reason": "disabled"
    }


def test_vision_requires_explicit_model(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_VISION_ENABLED", "true")
    monkeypatch.delenv("DOCUMENT_AI_VISION_MODEL", raising=False)
    assert vision_configuration()["reason"] == "vision_model_not_configured"


def test_vision_model_is_not_assumed_capable(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_VISION_ENABLED", "true")
    monkeypatch.setenv("DOCUMENT_AI_VISION_MODEL", "example-vision:latest")
    result = vision_configuration()
    assert result["ready"] is False
    assert result["reason"] == "probe_required"
