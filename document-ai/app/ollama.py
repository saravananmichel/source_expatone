"""Shared private Ollama transport. Never logs request or response contents."""
import json
import time
from .config import ollama_base_url

async def post_chat(client, payload):
    return await client.post(ollama_base_url() + '/api/chat', json=payload)

async def stream_chat(client, payload, metrics=None):
    started = time.monotonic()
    first_token = None
    content = []
    final = None
    async with client.stream('POST', ollama_base_url() + '/api/chat', json={**payload, 'stream': True}) as response:
        response.raise_for_status()
        async for line in response.aiter_lines():
            if not line: continue
            chunk = json.loads(line)
            if chunk.get('error'): raise ValueError('inference_failed')
            token = chunk.get('message', {}).get('content', '')
            if token:
                if first_token is None: first_token = time.monotonic() - started
                content.append(token)
            if chunk.get('done'): final = chunk
    if final is None: raise ValueError('incomplete_inference')
    if metrics is not None:
        metrics.append({'firstTokenMs': round(first_token * 1000) if first_token is not None else None,
            'totalMs': round((time.monotonic() - started) * 1000),
            'promptTokens': final.get('prompt_eval_count'), 'outputTokens': final.get('eval_count'),
            'tokensPerSecond': final.get('eval_count', 0) / max(final.get('eval_duration', 0) / 1e9, 1e-9),
            'loadMs': final.get('load_duration', 0) / 1e6})
    return ''.join(content)
