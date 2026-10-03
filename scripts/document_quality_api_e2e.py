"""Real Firebase auth -> local API -> private S3 -> PostgreSQL worker -> local AI.
Only generated synthetic document content is uploaded. No credentials or URLs logged.
"""
import json,secrets,time,urllib.request,urllib.error,os
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
BASE='http://127.0.0.1:5088/api'
results=[];token=None
expected_provider=os.getenv('EXPECTED_PROVIDER','Local')

def req(method,url,data=None,auth=True,mime='application/json'):
    body=json.dumps(data).encode() if data is not None and mime=='application/json' else data
    headers={'Content-Type':mime}
    if auth and token:headers['Authorization']='Bearer '+token
    try:
        with urllib.request.urlopen(urllib.request.Request(url,body,headers,method=method),timeout=120) as response:
            status=response.status;raw=response.read()
    except urllib.error.HTTPError as exc:status=exc.code;raw=exc.read()
    try:return status,json.loads(raw)
    except:return status,raw

def check(name,method,path,data=None,expected=(200,),auth=True):
    status,value=req(method,BASE+path,data,auth)
    results.append({'name':name,'status':status,'passed':status in expected});print(name,status,flush=True)
    if status not in expected:raise RuntimeError(name+' failed')
    return value

try:
    check('local health','GET','/health',auth=False)
    check('auth required','GET','/documents',expected=(401,),auth=False)
    config=json.loads((ROOT/'src/mobile/android/app/google-services.json').read_text())
    key=config['client'][0]['api_key'][0]['current_key']
    firebase='https://identitytoolkit.googleapis.com/v1/accounts:'
    status,user=req('POST',firebase+'signUp?key='+key,{'email':'quality-'+secrets.token_hex(6)+'@example.com','password':secrets.token_urlsafe(32),'returnSecureToken':True},auth=False)
    if status!=200:raise RuntimeError('synthetic Firebase auth unavailable')
    token=user['idToken'];check('authenticated identity','GET','/users/me')
    types=check('document types','GET','/document-types')
    data=(ROOT/'document-ai/evaluation/dataset/scan.pdf').read_bytes()
    upload=check('request upload','POST','/documents/upload-url',{'documentTypeId':types[0]['id'],'documentName':'Synthetic quality verification','fileName':'quality.pdf','contentType':'application/pdf','fileSizeBytes':len(data)},expected=(200,201))
    started=time.monotonic();status,_=req('PUT',upload['uploadUrl'],data,auth=False,mime='application/pdf')
    assert status==200;results.append({'name':'real private S3 upload','passed':True,'uploadMs':round((time.monotonic()-started)*1000)})
    doc=upload['documentId'];started=time.monotonic()
    check('complete and enqueue','POST',f'/documents/{doc}/complete')
    job=check('deduplicated async analyze','POST',f'/documents/{doc}/analyze',{},expected=(202,))
    analysis_id=job['analysisId']
    duplicate=check('duplicate uses active run','POST',f'/documents/{doc}/analyze',{},expected=(202,))
    assert duplicate['analysisId']==analysis_id
    for _ in range(600):
        status,job=req('GET',BASE+f'/documents/{doc}/analysis/status?analysisId={analysis_id}')
        assert status==200
        if job['status'] in ('COMPLETED','REQUIRES_REVIEW','FAILED'):break
        time.sleep(2)
    assert job['status'] in ('COMPLETED','REQUIRES_REVIEW')
    analysis=job['analysis'];assert analysis['provider']==expected_provider
    if expected_provider=='Local':
        assert analysis['evidence'] and analysis['semanticDocument']['documentGraph']
        assert all(s['supportStatus'] in ('SUPPORTED','PARTIALLY_SUPPORTED','UNSUPPORTED','UNCERTAIN') for s in analysis['statements'])
    else:
        assert analysis['requiresReview'] and analysis['confidence']=='low'
    results.append({'name':'real local model worker completed','passed':True,'seconds':round(time.monotonic()-started,2),'provider':analysis['provider'],'model':analysis['modelVersion'],'diagnostics':analysis['qualityDiagnostics']})
    history=check('persisted version history','GET',f'/documents/{doc}/analysis/history');assert len(history)==1
    persisted=check('reload exact analysis','GET',f'/documents/{doc}/analysis/{analysis_id}');assert persisted['analysis']['analysisId']==analysis_id
    # Keep disposable synthetic document for Flutter UI evidence verification; no destructive cleanup.
    (ROOT/'artifacts/document-quality-e2e-document.json').write_text(json.dumps({'documentId':doc,'analysisId':analysis_id},indent=2))
except Exception as exc:
    results.append({'name':'execution','passed':False,'errorType':type(exc).__name__});print('Stopped:',type(exc).__name__,flush=True)
finally:
    (ROOT/('document-ai/evaluation/reports/'+os.getenv('E2E_REPORT','local-api-e2e')+'.json')).write_text(json.dumps({'baseUrl':BASE,'syntheticOnly':True,'results':results,'limitations':'This script exercises actual local API/S3/PostgreSQL/model. Flutter is a separate device test; development API uses HTTP on local host.'},indent=2))
