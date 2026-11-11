"""Privacy-safe diagnostics: exception messages may contain private OCR content."""
import asyncio
import httpx
from app.main import _fallback_reason
from app.ollama import InferenceOutputError


def test_fallback_reason_codes_are_bounded():
    examples = [
        (asyncio.TimeoutError("private data"), "deadline_exceeded"),
        (httpx.ReadTimeout("private data"), "model_http_timeout"),
        (httpx.ConnectError("private data"), "model_transport_error"),
        (InferenceOutputError("private data"), "incomplete_or_invalid_inference"),
        (ValueError("model_unavailable"), "model_unavailable"),
        (ValueError("private data"), "validation_error"),
        (KeyError("private data"), "unexpected_model_error"),
    ]
    for error, expected in examples:
        code = _fallback_reason(error)
        assert code == expected
        assert "private" not in code


def test_http_status_reason_does_not_leak_request_details():
    request = httpx.Request("POST", "https://example.test/api/chat")
    response = httpx.Response(503, request=request)
    error = httpx.HTTPStatusError("private data", request=request, response=response)
    assert _fallback_reason(error) == "model_http_status"
