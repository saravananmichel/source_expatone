"""Operator-only V5.5 private model inventory check.

Run from document-ai/: python -m app.vision_preflight_cli_v55
No uploaded document, image, prompt, or credential is printed.
"""
import asyncio
import json

from .vision_preflight_v55 import vision_preflight


def readiness_decision(result: dict) -> dict:
    """Inventory listing is necessary, but never sufficient, for DEV activation."""
    status = result.get("status", "preflight_unavailable")
    return {
        "status": status,
        "modelAvailable": result.get("modelAvailable") is True,
        "inferenceVerified": False,
        "devActivationApproved": False,
        "nextGate": (
            "synthetic_image_inference_and_capacity_validation"
            if status == "model_listed"
            else "configure_private_vision_model_and_repeat_preflight"
        ),
    }


async def main():
    result = await vision_preflight()
    print(json.dumps(readiness_decision(result), sort_keys=True))


if __name__ == "__main__":
    asyncio.run(main())
