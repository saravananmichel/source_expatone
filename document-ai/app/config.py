"""Shared Ollama transport configuration; OLLAMA_URL remains a legacy alias."""
import os
from urllib.parse import urlsplit

import httpx


def ollama_base_url():
    value = (os.getenv('OLLAMA_BASE_URL') or os.getenv('OLLAMA_URL') or
             'http://127.0.0.1:11434').rstrip('/')
    url = urlsplit(value)
    if url.scheme not in ('http', 'https') or not url.hostname or url.username or url.password or url.query or url.fragment:
        raise ValueError('Invalid Ollama base URL')
    return value


def ollama_timeout(read_seconds):
    return httpx.Timeout(read_seconds, connect=5, pool=5, write=30)


async def check_model():
    model = os.getenv('OLLAMA_MODEL')
    if not model:
        raise ValueError('Model not configured')
    async with httpx.AsyncClient(timeout=ollama_timeout(3)) as client:
        response = await client.get(ollama_base_url() + '/api/tags')
        response.raise_for_status()
        if model not in [item.get('name') for item in response.json()['models']]:
            raise ValueError('Model unavailable')


def validate_config():
    ollama_base_url()
    if not os.getenv('DOCUMENT_AI_SERVICE_KEY') or not os.getenv('OLLAMA_MODEL'):
        raise ValueError('Service key and model must be configured')
    if os.getenv('DOCUMENT_AI_ENVIRONMENT') == 'Production':
        key = os.getenv('DOCUMENT_AI_SERVICE_KEY', '')
        if len(key) < 32 or key.startswith(('YOUR_', 'REPLACE_')):
            raise ValueError('Production requires a strong service key')
        if os.getenv('OLLAMA_MODEL') != 'qwen3:4b' or os.getenv('SUPPORT_MODEL', 'qwen3:4b') != 'qwen3:4b':
            raise ValueError('Production requires qwen3:4b')
