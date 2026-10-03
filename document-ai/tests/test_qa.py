import json
import httpx
import pytest
from fastapi.testclient import TestClient
from app.main import app
from app.qa import AskRequest, ask_document, INSUFFICIENT

SOURCE = 'Salary: RM12,000 per month. Notice during probation is seven days.'
BODY = {'question': 'What is the salary?', 'documentCategory': 'Employment Contract',
    'context': [{'id': 'e1', 'page': 2, 'text': SOURCE}]}

@pytest.fixture
def transport(monkeypatch):
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY', 'synthetic-test-key')
    monkeypatch.setenv('OLLAMA_MODEL', 'qwen3:4b')
    monkeypatch.setenv('SUPPORT_MODEL', 'qwen3:4b')
    monkeypatch.setenv('OLLAMA_BASE_URL', 'http://ollama:11434')
    monkeypatch.delenv('DOCUMENT_AI_ENVIRONMENT', raising=False)
    seen = []
    reply = {'answer': 'The salary is RM12,000 per month.', 'sufficientEvidence': True,
        'citations': [{'id': 'e1'}]}
    verdict = {'verdicts': [{'id': 'answer', 'supportStatus': 'SUPPORTED', 'reason': 'Exact source fact.'}]}
    def handler(request):
        # Every outgoing HTTP request goes through this guard. Google/cloud fail the test.
        assert request.url.host == 'ollama' and request.url.port == 11434
        assert request.url.path in ('/api/tags', '/api/chat')
        seen.append(request)
        if request.url.path == '/api/tags': return httpx.Response(200, json={'models': [{'name': 'qwen3:4b', 'digest': 'sha256:synthetic'}]})
        payload = json.loads(request.content)
        assert payload['model'] == 'qwen3:4b'
        assert payload['think'] is False
        output = verdict if 'verdicts' in payload['format']['properties'] else reply
        chunks = [{'message': {'content': json.dumps(output)}, 'done': False},
            {'message': {'content': ''}, 'done': True, 'prompt_eval_count': 420, 'eval_count': 42, 'eval_duration': 1_000_000_000}]
        return httpx.Response(200, content='\n'.join(json.dumps(c) for c in chunks))
    original = httpx.AsyncClient
    monkeypatch.setattr(httpx, 'AsyncClient', lambda **kwargs: original(transport=httpx.MockTransport(handler), **kwargs))
    return seen, reply, verdict

def post(body=BODY, key='synthetic-test-key'):
    with TestClient(app) as client: return client.post('/ask', json=body, headers={'X-Service-Key': key})

def test_private_endpoint_uses_only_qwen_and_returns_verified_page_quotes(transport, caplog):
    seen, _, _ = transport
    result = post()
    assert result.status_code == 200
    assert result.json()['grounded'] is True
    assert result.json()['evidence'] == [{'id': 'e1', 'page': 2, 'sourceText': SOURCE}]
    assert len(seen) == 3  # readiness + answer + support review; no extraction
    assert SOURCE not in caplog.text and BODY['question'] not in caplog.text
    assert 'qualityDiagnostics' not in result.json()

@pytest.mark.parametrize('key', ['', 'wrong'])
def test_unauthorized_ask_never_reaches_inference(transport, key):
    assert post(key=key).status_code == 401
    assert transport[0] == []

def test_unauthorized_large_body_is_rejected_before_parsing(transport):
    with TestClient(app) as client:
        assert client.post('/ask', content=b'x'*100000).status_code == 401
    assert transport[0] == []

@pytest.mark.parametrize('mutation', ['unknown_id', 'wrong_number', 'no_evidence', 'unsupported', 'partial', 'duplicate'])
def test_unsupported_answers_fail_closed(transport, mutation):
    _, reply, verdict = transport
    if mutation == 'unknown_id': reply['citations'][0]['id'] = 'missing'
    if mutation == 'wrong_number': reply['answer'] = 'The salary is RM99,999.'
    if mutation == 'no_evidence': reply['sufficientEvidence'] = False
    if mutation == 'unsupported': verdict['verdicts'][0]['supportStatus'] = 'UNSUPPORTED'
    if mutation == 'partial': verdict['verdicts'][0]['supportStatus'] = 'PARTIALLY_SUPPORTED'
    if mutation == 'duplicate': reply['citations'] *= 2
    result = post()
    assert result.json() == {'answer': INSUFFICIENT, 'grounded': False, 'evidence': []}

def test_malformed_input_does_not_echo_question_or_source(transport):
    body = {**BODY, 'question': 'private-question', 'context': [{**BODY['context'][0], 'page': 0}]}
    result = post(body)
    assert result.status_code == 422 and result.json() == {'detail': 'Invalid document request'}
    assert 'private-question' not in result.text and SOURCE not in result.text
    assert not transport[0]

def test_context_and_body_are_bounded(transport):
    assert post({**BODY, 'context': BODY['context']*7}).status_code == 422
    with TestClient(app) as client:
        assert client.post('/ask', content=b'x'*65537, headers={'X-Service-Key': 'synthetic-test-key'}).status_code == 413

@pytest.mark.parametrize('url', ['https://generativelanguage.googleapis.com', 'https://ollama.com', 'https://8.8.8.8'])
def test_production_rejects_external_ollama_destinations(monkeypatch, url):
    from app.config import ollama_base_url
    monkeypatch.setenv('DOCUMENT_AI_ENVIRONMENT', 'Production')
    monkeypatch.setenv('OLLAMA_BASE_URL', url)
    with pytest.raises(ValueError): ollama_base_url()

def test_model_failure_is_safe_and_has_no_fallback(transport):
    seen, reply, _ = transport
    reply.clear()
    result = post()
    assert result.status_code == 503
    assert len(seen) == 2
    assert SOURCE not in result.text

def test_analysis_ingestion_network_is_private_ollama_only(transport):
    from pdf_helpers import native_pdf
    seen, _, _ = transport
    async def guarded_analyzer(client, payload):
        # Real pipeline sends its actual payload through the same transport guard.
        request = httpx.Request('POST', 'http://ollama:11434/api/chat', json=payload)
        await client.send(request)
        properties = payload['format']['properties']
        if 'documentCategory' in properties:
            output = {'documentCategory': 'Employment Contract', 'classificationConfidence': .8, 'classificationEvidenceIds': ['e1'],
                'evidence': [{'id': 'e1', 'page': 1, 'sourceText': 'Salary: RM12,000 per month.'}],
                'statements': [{'id': 's1', 'kind': 'fact', 'label': 'Salary', 'text': 'The salary is RM12,000 per month.',
                    'originalValue': 'RM12,000', 'confidence': .8, 'evidenceIds': ['e1']}]}
        elif 'overview' in properties:
            output = {'overview': {'id': 'overview', 'kind': 'interpretation', 'label': 'Overview',
                'text': 'The salary is RM12,000 per month.', 'originalValue': 'RM12,000', 'confidence': .8, 'evidenceIds': ['e1']}, 'statements': []}
        else:
            group = json.loads(payload['messages'][1]['content'])
            output = {'verdicts': [{'id': item['id'], 'supportStatus': 'SUPPORTED', 'reason': 'Exact source.'} for item in group]}
        return httpx.Response(200, request=request, json={'message': {'content': json.dumps(output)}})
    # Patch only the response format adapter; actual HTTP calls still pass through the network guard.
    from unittest.mock import patch
    with patch('app.pipeline.post_chat', guarded_analyzer), patch('app.reasoning.post_chat', guarded_analyzer), patch('app.support.post_chat', guarded_analyzer):
        with TestClient(app) as client:
            response = client.post('/analyze', headers={'X-Service-Key': 'synthetic-test-key'},
                files={'file': ('synthetic.pdf', native_pdf(['Employment contract. Salary: RM12,000 per month.']), 'application/pdf')})
    assert response.status_code == 200
    assert response.json()['provider'] == 'Local'
    assert response.json()['modelVersion'].startswith('qwen3:4b@')
    assert any(r.url.path == '/api/chat' for r in seen)

def test_production_model_misconfiguration_fails_before_any_network(transport, monkeypatch):
    key = 'synthetic-production-key-at-least-32-characters'
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY', key)
    monkeypatch.setenv('DOCUMENT_AI_ENVIRONMENT', 'Production')
    monkeypatch.setenv('OLLAMA_MODEL', 'another-model')
    monkeypatch.setenv('DOCUMENT_AI_VALIDATE_STARTUP', 'false')
    assert post(key=key).status_code == 503
    assert transport[0] == []
