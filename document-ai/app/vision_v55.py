"""Optional private Ollama vision probe for V5.5.

This module does not alter /analyze, persist images, or promote identity fields.
Callers must explicitly opt in and review source-grounded outputs separately.
"""
import base64
import os
import re

import httpx

from .config import ollama_timeout
from .ollama import check_completion

_ALLOWED_MODELS = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.:/-]{0,100}$")
_ALLOWED_MIME = {"image/jpeg", "image/png"}
_MAX_BYTES = 2 * 1024 * 1024


class VisionUnavailable(ValueError):
    """Safe, bounded failure without source image or model output in the message."""


def vision_timeout_seconds():
    """Bounded Vision-only HTTP read timeout; does not affect text inference."""
    raw = os.getenv("DOCUMENT_AI_V55_VISION_TIMEOUT_SECONDS", "120").strip()
    try:
        seconds = int(raw)
    except ValueError:
        raise VisionUnavailable("invalid_vision_timeout") from None
    if not 35 <= seconds <= 180:
        raise VisionUnavailable("invalid_vision_timeout")
    return seconds


def vision_model():
    if os.getenv("DOCUMENT_AI_V55_VISION_ENABLED", "false").lower() != "true":
        raise VisionUnavailable("vision_disabled")
    name = os.getenv("DOCUMENT_AI_V55_VISION_MODEL", "").strip()
    if not name or not _ALLOWED_MODELS.fullmatch(name):
        raise VisionUnavailable("vision_model_not_configured")
    # The production text model remains fixed; do not silently use it for images.
    if name == os.getenv("OLLAMA_MODEL", "qwen3:4b"):
        raise VisionUnavailable("vision_requires_separate_model")
    return name


def vision_payload(image: bytes, mime_type: str, model: str):
    if mime_type not in _ALLOWED_MIME or not image or len(image) > _MAX_BYTES:
        raise VisionUnavailable("invalid_vision_image")
    if mime_type == "image/png" and not image.startswith(b"\x89PNG\r\n\x1a\n"):
        raise VisionUnavailable("invalid_vision_image")
    if mime_type == "image/jpeg" and not image.startswith(b"\xff\xd8\xff"):
        raise VisionUnavailable("invalid_vision_image")
    return {
        "model": model,
        "stream": False,
        "think": False,
        "messages": [{
            "role": "user",
            "content": (
                "Read only text visibly present in this document image. "
                "Return plain text with line breaks, no guesses, no normalized dates, "
                "no inferred identity details. If unreadable, return UNREADABLE."
            ),
            "images": [base64.b64encode(image).decode("ascii")],
        }],
        "options": {"num_predict": 600, "temperature": 0},
    }


async def probe_image(image: bytes, mime_type: str) -> str:
    """Return untrusted vision OCR text for review; never use as verified identity."""
    from .vision_transport_v55 import vision_endpoint, vision_auth_headers
    model = vision_model()
    endpoint, _ = vision_endpoint()
    payload = vision_payload(image, mime_type, model)
    try:
        async with httpx.AsyncClient(timeout=ollama_timeout(vision_timeout_seconds())) as client:
            response = await client.post(
                endpoint + "/api/chat",
                json=payload,
                headers=await vision_auth_headers(),
            )
            response.raise_for_status()
            raw = response.json()
            check_completion(raw)
            result = raw.get("message", {}).get("content", "")
            if not isinstance(result, str) or not result.strip() or len(result) > 12000:
                raise VisionUnavailable("invalid_vision_output")
            return result
    except (httpx.HTTPError, ValueError, KeyError, TypeError) as exc:
        if isinstance(exc, VisionUnavailable):
            raise
        raise VisionUnavailable("vision_unavailable") from None
