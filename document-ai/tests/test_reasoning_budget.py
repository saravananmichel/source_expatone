from app.adaptive_profile import InferenceProfile
from app.reasoning_budget import reasoning_budget


def profile(name="development", seconds=120):
    return InferenceProfile(name, "test", 2048, 450, seconds, False)


def test_default_unchanged(monkeypatch):
    monkeypatch.delenv("DOCUMENT_AI_ADAPTIVE_BUDGET", raising=False)
    assert reasoning_budget(profile(), 30) == 122


def test_opt_in_elapsed_budget(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_ADAPTIVE_BUDGET", "1")
    assert reasoning_budget(profile(), 20) == 25
    assert reasoning_budget(profile(), 46) == 0
    assert reasoning_budget(profile(seconds=10), 0) == 12
    assert reasoning_budget(profile("production", 300), 40) == 302
