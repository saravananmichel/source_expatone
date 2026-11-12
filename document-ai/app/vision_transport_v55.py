"""Fail-closed private Cloud Run transport for the separate V5.5 Vision service.

Never fall back to the text-model Ollama endpoint or its identity token.
"""
import asyncio
import os
from urllib.parse import urlsplit

from .vision_v55 import VisionUnavailable


def vision_endpoint():
    value = os.getenv("VISION_OLLAMA_BASE_URL", "").strip().rstrip("/")
    audience = os.getenv("VISION_OLLAMA_CLOUD_RUN_AUDIENCE", "").strip().rstrip("/")
    if not value or not audience:
        raise VisionUnavailable("vision_endpoint_not_configured")
    for candidate in (value, audience):
        parsed = urlsplit(candidate)
        if (parsed.scheme != "https" or not parsed.hostname or
                not parsed.hostname.endswith(".run.app") or
                parsed.path not in ("", "/") or parsed.port is not None or
                parsed.username or parsed.password or parsed.query or parsed.fragment):
            raise VisionUnavailable("invalid_vision_endpoint")
    if value != audience:
        raise VisionUnavailable("vision_audience_mismatch")
    text_url = (os.getenv("OLLAMA_BASE_URL") or os.getenv("OLLAMA_URL") or "").strip().rstrip("/")
    if value == text_url:
        raise VisionUnavailable("vision_endpoint_must_be_separate")
    return value, audience


async def vision_auth_headers():
    _, audience = vision_endpoint()
    from google.oauth2 import id_token
    from google.auth.transport.requests import Request
    token = await asyncio.to_thread(id_token.fetch_id_token, Request(), audience)
    return {"Authorization": f"Bearer {token}"}
