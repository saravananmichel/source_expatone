"""V5.5 private Vision model preflight (no documents or images transmitted)."""
import httpx

from .config import ollama_timeout
from .vision_transport_v55 import vision_endpoint, vision_auth_headers
from .vision_v55 import VisionUnavailable, vision_model


async def vision_preflight() -> dict:
    """Check configured model inventory; no source content leaves the service.

    A positive inventory check does not prove image inference, capacity, or cost.
    """
    try:
        model = vision_model()
        endpoint, _ = vision_endpoint()
    except VisionUnavailable as exc:
        return {"status": str(exc), "modelAvailable": False, "inferenceVerified": False}
    try:
        async with httpx.AsyncClient(timeout=ollama_timeout(5)) as client:
            response = await client.get(
                endpoint + "/api/tags",
                headers=await vision_auth_headers(),
            )
            response.raise_for_status()
            models = response.json().get("models", [])
            if not isinstance(models, list):
                raise ValueError("invalid_model_inventory")
            present = any(isinstance(item, dict) and item.get("name") == model for item in models)
            return {
                "status": "model_listed" if present else "model_not_listed",
                "modelAvailable": present,
                "inferenceVerified": False,
            }
    except (httpx.HTTPError, ValueError, TypeError, KeyError):
        return {"status": "preflight_unavailable", "modelAvailable": False, "inferenceVerified": False}
