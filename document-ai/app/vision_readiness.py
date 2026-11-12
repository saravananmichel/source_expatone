"""V5.5 opt-in vision readiness probe; no document images leave this service.

Ollama /api/show exposes capabilities for installed models. Do not infer
vision support from model names, and do not log model metadata or documents.
"""
import os
import httpx
from .config import ollama_base_url, ollama_timeout
from .cloud_run_auth import ollama_auth_headers


def vision_configuration():
    enabled = os.getenv("DOCUMENT_AI_VISION_ENABLED", "false").lower() == "true"
    model = os.getenv("DOCUMENT_AI_VISION_MODEL", "").strip()
    if enabled and not model:
        return {"enabled": True, "ready": False, "reason": "vision_model_not_configured"}
    return {"enabled": enabled, "ready": False,
            "reason": "probe_required" if enabled else "disabled"}


async def probe_vision_readiness():
    config = vision_configuration()
    if not config["enabled"] or config["reason"] != "probe_required":
        return config
    model = os.environ["DOCUMENT_AI_VISION_MODEL"].strip()
    try:
        async with httpx.AsyncClient(timeout=ollama_timeout(10)) as client:
            response = await client.post(
                ollama_base_url() + "/api/show",
                json={"model": model},
                headers=await ollama_auth_headers(),
            )
            response.raise_for_status()
            capabilities = response.json().get("capabilities", [])
            if not isinstance(capabilities, list):
                return {"enabled": True, "ready": False, "reason": "invalid_capabilities"}
            if "vision" not in capabilities:
                return {"enabled": True, "ready": False, "reason": "model_not_vision_capable"}
            return {"enabled": True, "ready": True, "reason": "vision_capability_confirmed"}
    except (httpx.HTTPError, ValueError, KeyError):
        return {"enabled": True, "ready": False, "reason": "probe_unavailable"}
