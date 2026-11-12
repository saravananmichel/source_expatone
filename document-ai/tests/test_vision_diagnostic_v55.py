"""V5.5 opt-in integration safety tests."""
import asyncio
from types import SimpleNamespace

from app import vision_diagnostic_v55 as module


def test_disabled_never_calls_vision(monkeypatch):
    monkeypatch.delenv("DOCUMENT_AI_V55_VISION_ENABLED", raising=False)
    async def forbidden(*args):
        raise AssertionError("Vision should not run")
    monkeypatch.setattr(module, "probe_image", forbidden)
    result = asyncio.run(module.optional_vision_diagnostic(b"image", "image/png", []))
    assert result is None


def test_pdf_never_sent_to_model(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_V55_VISION_ENABLED", "true")
    async def forbidden(*args):
        raise AssertionError("PDF must not be sent")
    monkeypatch.setattr(module, "probe_image", forbidden)
    result = asyncio.run(module.optional_vision_diagnostic(b"%PDF", "application/pdf", []))
    assert result["status"] == "unsupported_media"


def test_oversize_never_sent_to_model(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_V55_VISION_ENABLED", "true")
    result = asyncio.run(module.optional_vision_diagnostic(b"x" * (2 * 1024 * 1024 + 1), "image/png", []))
    assert result["status"] == "image_limit_exceeded"


def test_success_returns_review_only(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_V55_VISION_ENABLED", "true")
    async def fake_probe(data, mime):
        return "PASSPORT NUMBER ABC123456 COUNTRY MALAYSIA"
    monkeypatch.setattr(module, "probe_image", fake_probe)
    result = asyncio.run(module.optional_vision_diagnostic(
        b"image", "image/png",
        [SimpleNamespace(text="PASSPORT NUMBER ABC123456 COUNTRY MALAYSIA")],
    ))
    assert result["status"] == "high_overlap_review_only"
    assert result["automaticFieldUpdates"] is False
    assert "ABC123456" not in str(result)


def test_failure_does_not_leak_or_interrupt(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_V55_VISION_ENABLED", "true")
    async def fail(data, mime):
        raise RuntimeError("sensitive passport 123456789")
    monkeypatch.setattr(module, "probe_image", fail)
    result = asyncio.run(module.optional_vision_diagnostic(b"image", "image/png", []))
    assert result == {"status": "vision_unavailable", "requiresConfirmation": True}
