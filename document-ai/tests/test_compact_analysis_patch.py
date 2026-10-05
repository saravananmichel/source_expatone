import asyncio
import copy
import json
import logging
import sys
from pathlib import Path
from types import SimpleNamespace

import httpx
import pytest
from fastapi.testclient import TestClient

root = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(root if (root / 'app').is_dir() else root / 'document-ai'))
from app import compact, limits, main, ollama, pipeline
from app.schemas import Page, SemanticDocument


def page(text='Journey: Cameron Highlands to Kuala Lumpur.\nFare: RM35.00.\nNo refund after departure.', number=1):
    return Page(page=number, text=text, width=100, height=100,
                extraction='text', blocks=[], extractionConfidence=1)


def result():
    return {'category': 'Other', 'confidence': .9, 'refs': ['e0'], 'items': [
        {'k': 'money', 'l': 'Fare', 't': 'The fare is RM35.00.',
         'v': 'RM35.00', 'c': .9, 'e': ['e0']},
        {'k': 'condition', 'l': 'Refund condition',
         't': 'No refund after departure.', 'v': 'No refund after departure.',
         'c': .9, 'e': ['e0']}]}


def completion(content, reason='stop', done=True):
    return {'done': done, 'done_reason': reason,
            'message': {'content': json.dumps(content)}, 'eval_count': 100,
            'eval_duration': 25_000_000_000}


@pytest.fixture(autouse=True)
def environment(monkeypatch):
    monkeypatch.setenv('OLLAMA_MODEL', 'qwen3:4b')
    monkeypatch.setenv('DOCUMENT_AI_SERVICE_KEY', 'test-key')
    monkeypatch.setenv('DOCUMENT_AI_ENVIRONMENT', 'Development')
    monkeypatch.setenv('OLLAMA_BASE_URL', 'http://model.test')


def client_factory(monkeypatch, handler):
    original = httpx.AsyncClient
    monkeypatch.setattr(httpx, 'AsyncClient', lambda **kw:
        original(transport=httpx.MockTransport(handler), **kw))


def test_compact_schema_has_no_generated_evidence_or_server_fields():
    schema = compact.CompactExtraction.model_json_schema()
    assert set(schema['properties']) == {'category', 'confidence', 'refs', 'items'}
    assert set(schema['$defs']['CompactStatement']['properties']) == {'k', 'l', 't', 'v', 'c', 'e'}


@pytest.mark.parametrize('text', [
    'A ' * 3100, '中文文件。不得退款。' * 600,
    'لا يجوز استرداد المبلغ بعد المغادرة.\n' * 200,
    'Bayaran RM35.00. Tiada bayaran balik.\n' * 200,
])
def test_every_source_character_and_page_is_preserved(text):
    passages = compact.source_passages([page(text, 1), page('Second page', 2)])
    assert ''.join(p['text'] for p in passages if p['page'] == 1) == text
    assert ''.join(p['text'] for p in passages if p['page'] == 2) == 'Second page'
    assert len({p['id'] for p in passages}) == len(passages)
    assert all(0 < len(p['text']) <= 1000 for p in passages)


def test_expansion_keeps_contract_evidence_and_negation():
    pages = [page()]
    semantic = compact.expand_extraction(compact.CompactExtraction.model_validate(result()),
                                         compact.source_passages(pages))
    pipeline.validate_grounding(semantic, pages)
    assert semantic.evidence[0].sourceText == pages[0].text
    assert semantic.statements[1].text == 'No refund after departure.'
    assert semantic.statements[0].supportStatus == 'UNCERTAIN'
    assert SemanticDocument.model_validate(semantic.model_dump()) == semantic


def test_unknown_source_id_rejected():
    data = result(); data['items'][0]['e'] = ['invented']
    with pytest.raises(ValueError, match='missing_evidence'):
        compact.expand_extraction(compact.CompactExtraction.model_validate(data), compact.source_passages([page()]))


@pytest.mark.parametrize('field,value,error', [
    ('v', 'RM99.00', 'unsupported_value'),
    ('t', 'The fare is RM99.00.', 'unsupported_number'),
])
def test_original_grounding_checks_are_still_enforced(field, value, error):
    data=result(); data['items'][0][field]=value
    semantic=compact.expand_extraction(compact.CompactExtraction.model_validate(data), compact.source_passages([page()]))
    with pytest.raises(ValueError, match=error):
        pipeline.validate_grounding(semantic, [page()])


@pytest.mark.parametrize('reason,done', [('length', True), ('stop', False)])
def test_transport_rejects_truncation_and_incomplete_response(reason, done):
    async def run():
        async with httpx.AsyncClient(transport=httpx.MockTransport(
                lambda request: httpx.Response(200, json=completion(result(), reason, done)))) as client:
            with pytest.raises(ollama.InferenceOutputError):
                await ollama.post_chat(client, {'options': {'num_predict': 2000}})
    asyncio.run(run())


def test_four_threads_do_not_mutate_request_or_other_options():
    payload={'options': {'num_predict': 2000, 'temperature': 0, 'num_thread': 2}}
    before=copy.deepcopy(payload)
    def handler(request):
        sent=json.loads(request.content)
        assert sent['options'] == {'num_predict': 2000, 'temperature': 0, 'num_thread': 4}
        return httpx.Response(200, json=completion(result()))
    async def run():
        async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as client:
            await ollama.post_chat(client, payload)
    asyncio.run(run())
    assert payload == before


def test_streaming_rejects_partial_final_output():
    data = json.dumps({'message': {'content': 'partial'}, 'done': True, 'done_reason': 'length'}) + '\n'
    async def run():
        async with httpx.AsyncClient(transport=httpx.MockTransport(lambda request: httpx.Response(200, text=data))) as client:
            with pytest.raises(ollama.InferenceOutputError, match='output_token_limit'):
                await ollama.stream_chat(client, {})
    asyncio.run(run())


@pytest.mark.parametrize('bad', ['length', 'schema', 'citation'])
def test_failed_extraction_is_not_regenerated_or_silently_dropped(monkeypatch, bad):
    calls=[]
    def handler(request):
        calls.append(request)
        data=result()
        if bad == 'schema': data = {'private_source': 'SECRET DOCUMENT CONTENT'}
        if bad == 'citation': data['refs']=['not-a-source']
        return httpx.Response(200, json=completion(data, 'length' if bad == 'length' else 'stop'))
    client_factory(monkeypatch, handler)
    with pytest.raises(ollama.InferenceOutputError):
        asyncio.run(pipeline.reason([page()]))
    assert len(calls) == 1


def test_two_chunks_of_one_page_validate_against_all_chunks(monkeypatch):
    data=result(); data['items']=data['items'][:1]
    client_factory(monkeypatch, lambda request: httpx.Response(200, json=completion(data)))
    semantic,_ = asyncio.run(pipeline.reason_batch([page('Fare: RM35.00.\n'), page('Different clause.\n')]))
    assert semantic.statements[0].originalValue == 'RM35.00'


def fake_extraction(monkeypatch):
    async def check(): pass
    async def communicate(data):
        return json.dumps([page().model_dump()]).encode(), b''
    async def subprocess(*args, **kw):
        return SimpleNamespace(returncode=0, communicate=communicate)
    monkeypatch.setattr(main, 'check_model', check)
    monkeypatch.setattr(main.asyncio, 'create_subprocess_exec', subprocess)


def analyze_request():
    with TestClient(main.app) as client:
        return client.post('/analyze', headers={'x-service-key': 'test-key'},
                           files={'file': ('test.pdf', b'fake parser input', 'application/pdf')})


@pytest.mark.parametrize('mode,status,code', [
    ('length', 422, 'output_token_limit'),
    ('schema', 422, 'invalid_model_output'),
    ('timeout', 422, 'inference_timeout'),
    ('connection', 503, None),
])
def test_api_distinguishes_terminal_failures_from_unavailability(monkeypatch, caplog, mode, status, code):
    fake_extraction(monkeypatch)
    calls=[]
    def handler(request):
        calls.append(request)
        if mode=='timeout': raise httpx.ReadTimeout('SECRET DOCUMENT CONTENT', request=request)
        if mode=='connection': raise httpx.ConnectError('SECRET DOCUMENT CONTENT', request=request)
        data=result() if mode!='schema' else {'private_source': 'SECRET DOCUMENT CONTENT'}
        return httpx.Response(200, json=completion(data, 'length' if mode=='length' else 'stop'))
    client_factory(monkeypatch, handler)
    with caplog.at_level(logging.INFO): response=analyze_request()
    assert response.status_code==status
    if code: assert response.json()['detail']['code']==code
    assert 'SECRET DOCUMENT CONTENT' not in response.text + caplog.text
    assert len(calls)==1


def test_successful_http_flow_preserves_review_and_public_contract(monkeypatch):
    fake_extraction(monkeypatch)
    stages=[]
    def handler(request):
        if request.method=='GET':
            return httpx.Response(200, json={'models':[{'name':'qwen3:4b','digest':'test-digest'}]})
        payload=json.loads(request.content); stage=payload['format']['title']; stages.append(stage)
        assert payload['options']['num_thread']==4
        if stage=='CompactExtraction': data=result()
        elif stage=='Conclusions':
            data={'overview': {'id':'overview','kind':'interpretation','label':'Overview',
                'text':'The document states the fare and a refund condition.', 'originalValue':'RM35.00',
                'confidence':.9,'evidenceIds':['b0-e0']}, 'statements':[]}
        elif stage=='Review':
            claims=json.loads(payload['messages'][-1]['content'])
            data={'verdicts':[{'id':s['id'],'supportStatus':'SUPPORTED','reason':'Matches the supplied evidence.'} for s in claims]}
        else: raise AssertionError(stage)
        return httpx.Response(200, json=completion(data))
    client_factory(monkeypatch, handler)
    response=analyze_request()
    assert response.status_code==200, response.text
    body=response.json()
    assert body['requiresReview'] is True
    assert body['keyInformation'][0]['value']=='RM35.00'
    assert body['evidence'][0]['sourceText']==page().text
    assert body['qualityDiagnostics']['verdictCoverage']==1
    assert body['analyzerVersion']=='document-engine-v3-compact1'
    assert stages==['CompactExtraction','Conclusions','Review']


def test_analysis_deadline_returns_terminal_422(monkeypatch):
    # Force the existing deadline path without a real 30-second wait.
    async def expired(scope, receive, send): raise TimeoutError()
    sent=[]
    async def send(message): sent.append(message)
    async def receive(): return {'type':'http.request','body':b'','more_body':False}
    scope={'type':'http','path':'/analyze','headers':[(b'x-service-key',b'test-key')]}
    asyncio.run(limits.IngestionLimits(expired)(scope,receive,send))
    assert sent[0]['status']==422
    assert b'analysis_budget_exceeded' in sent[1]['body']


def test_review_failure_never_promotes_uncertain_claims(monkeypatch):
    from app.support import review_support
    semantic=compact.expand_extraction(compact.CompactExtraction.model_validate(result()), compact.source_passages([page()]))
    client_factory(monkeypatch, lambda request: httpx.Response(200, json=completion({}, 'length')))
    quality=asyncio.run(review_support(semantic))
    assert quality['verdictCoverage']==0
    assert all(s.supportStatus=='UNCERTAIN' for s in semantic.statements)
    body=pipeline.render(semantic, [page()], 'qwen3:4b@test')
    assert body['keyInformation']==[]
    assert body['requiresReview'] is True
    assert body['confidence']=='low'


def test_successful_stream_preserves_text_and_metrics():
    chunks=[{'message':{'content':'Verified '}},
            {'message':{'content':'answer'},'done':True,'done_reason':'stop',
             'eval_count':10,'eval_duration':2_000_000_000}]
    def handler(request):
        payload=json.loads(request.content)
        assert payload['stream'] is True
        assert payload['options']['num_thread']==4
        return httpx.Response(200, text='\n'.join(json.dumps(c) for c in chunks))
    async def run():
        metrics=[]
        async with httpx.AsyncClient(transport=httpx.MockTransport(handler)) as client:
            text=await ollama.stream_chat(client, {}, metrics)
        assert text=='Verified answer'
        assert metrics[0]['tokensPerSecond']==5
    asyncio.run(run())
