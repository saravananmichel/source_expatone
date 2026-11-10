"""Optional keyless Cloud Run IAM authentication for private Ollama.

Only enabled when OLLAMA_CLOUD_RUN_AUDIENCE is explicitly configured.
Never log identity tokens or document contents.
"""
import asyncio
import os
from urllib.parse import urlsplit


def cloud_run_audience():
    audience = os.getenv("OLLAMA_CLOUD_RUN_AUDIENCE", "").strip().rstrip("/")
    if not audience:
        return None
    parsed = urlsplit(audience)
    if (parsed.scheme != "https" or not parsed.hostname or
            not parsed.hostname.endswith(".run.app") or
            parsed.username or parsed.password or parsed.query or parsed.fragment or
            parsed.path not in ("", "/") or parsed.port is not None):
        raise ValueError("OLLAMA_CLOUD_RUN_AUDIENCE must be an HTTPS Cloud Run service origin")
    return audience


async def ollama_auth_headers():
    audience = cloud_run_audience()
    if not audience:
        return {}
    # google-auth uses the attached Cloud Run service account (no JSON key).
    from google.oauth2 import id_token
    from google.auth.transport.requests import Request
    token = await asyncio.to_thread(id_token.fetch_id_token, Request(), audience)
    return {"Authorization": f"Bearer {token}"}
