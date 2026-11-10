from app.adaptive_profile import selected_profile


def test_development_inference_timeout_default(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_PROFILE", "development")
    monkeypatch.delenv("DOCUMENT_AI_DEV_INFERENCE_SECONDS", raising=False)
    assert selected_profile().inference_seconds == 120


def test_development_inference_timeout_override(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_PROFILE", "development")
    monkeypatch.setenv("DOCUMENT_AI_DEV_INFERENCE_SECONDS", "90")
    assert selected_profile().inference_seconds == 90


def test_production_inference_timeout_unaffected(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_PROFILE", "production")
    monkeypatch.setenv("DOCUMENT_AI_DEV_INFERENCE_SECONDS", "120")
    assert selected_profile().inference_seconds == 300
