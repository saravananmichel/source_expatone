"""Synthetic-only /ask benchmark. Records timings/counters, never source/questions/answers.
Run in a container on an internal Docker network with private Ollama and no published ports.
"""
import argparse
import json
import time
from pathlib import Path
import httpx
from fastapi.testclient import TestClient
from app.main import app
from app import qa
from app.config import ollama_base_url

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
args = parser.parse_args()
import os
key = os.environ['DOCUMENT_AI_SERVICE_KEY']
original_stream = qa.stream_chat
original_send = httpx.AsyncClient.send
metrics = []
requests = []
allowed = httpx.URL(ollama_base_url())
async def recorded_stream(client, payload, ignored=None):
    content = await original_stream(client, payload, metrics)
    if 'sufficientEvidence' in payload['format'].get('properties', {}):
        parsed = qa.Answer.model_validate_json(content)
        source = {c['id']: c for c in json.loads(payload['messages'][1]['content'])['context']}
        metrics[-1]['validation'] = {'sufficientEvidence': parsed.sufficientEvidence,
            'citationCount': len(parsed.citations), 'idsValid': all(c.id in source for c in parsed.citations)}
    return content
async def guarded_send(self, request, **kwargs):
    if request.url.host != allowed.host or request.url.port != allowed.port or request.url.path not in ('/api/chat','/api/tags'):
        raise AssertionError('unexpected_document_network_destination')
    requests.append({'host': request.url.host, 'path': request.url.path})
    return await original_send(self, request, **kwargs)
qa.stream_chat = recorded_stream
httpx.AsyncClient.send = guarded_send
short = [{'id': 'salary', 'page': 2, 'text': 'Employment contract. Salary: RM12,000 per month. Notice during probation is seven days.'}]
bounded = [{'id': f'p{i}', 'page': i, 'text': ('This synthetic agreement describes office policies and confidentiality. '*14)[:950]} for i in range(1,7)]
bounded[4]['text'] = 'Salary: RM12,000 per month. '+bounded[4]['text'][:920]
results = []
with TestClient(app) as client:
    for label, context in [('cold-short',short),('warm-short',short),('warm-bounded',bounded)]:
        metrics.clear(); started = time.monotonic()
        response = client.post('/ask', headers={'X-Service-Key': key}, json={'question': 'What is the salary?',
            'documentCategory': 'Employment Contract', 'context': context})
        results.append({'case': label, 'httpStatus': response.status_code, 'grounded': response.json().get('grounded',False),
            'totalMs': round((time.monotonic()-started)*1000), 'contextCharacters': sum(len(c['text']) for c in context),
            'inferencePasses': list(metrics)})
        Path(args.output).write_text(json.dumps({'synthetic':True,'model':'qwen3:4b','results':results,
            'destinations':list({(r['host'],r['path']) for r in requests}),
            'limitations':'First-token measures upstream Ollama content, not Flutter delivery. Answers are buffered until citation/support validation. No real user data.'},indent=2))
        print(json.dumps(results[-1]),flush=True)
