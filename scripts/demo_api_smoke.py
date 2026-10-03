"""Live HTTPS smoke test. Creates synthetic test data; prints no credentials or signed URLs."""
import json, secrets, time, urllib.request, urllib.error, pathlib, datetime
BASE = 'https://d38i3e3pvvjux7.cloudfront.net/api'
ROOT = pathlib.Path(__file__).resolve().parents[1]
results=[]
token=None

def req(method,url,data=None,auth=False,ctype='application/json'):
    assert url.startswith('https://'), 'HTTPS required'
    body=(json.dumps(data).encode() if ctype=='application/json' and data is not None else data)
    headers={'Content-Type':ctype}
    if auth: headers['Authorization']='Bearer '+token
    try:
        with urllib.request.urlopen(urllib.request.Request(url,body,headers,method=method),timeout=150) as r:
            raw=r.read(); status=r.status
    except urllib.error.HTTPError as e:
        raw=e.read(); status=e.code
    try: value=json.loads(raw)
    except: value=raw
    return status,value

def check(name,method,path,data=None,auth=True,expected=(200,201,204)):
    status,value=req(method,BASE+path,data,auth)
    results.append({'test':name,'status':status,'passed':status in expected})
    print(name, status, flush=True)
    return value if status in expected else None

def pdf():
    content=b'BT /F1 12 Tf 50 750 Td (ExpatOne synthetic demo document. Renewal due 31 December 2026.) Tj ET'
    objs=[b'<< /Type /Catalog /Pages 2 0 R >>',b'<< /Type /Pages /Kids [3 0 R] /Count 1 >>',b'<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>',b'<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',b'<< /Length '+str(len(content)).encode()+b' >>\nstream\n'+content+b'\nendstream']
    out=b'%PDF-1.4\n'; offsets=[0]
    for i,o in enumerate(objs,1):
        offsets.append(len(out)); out+=str(i).encode()+b' 0 obj\n'+o+b'\nendobj\n'
    x=len(out); out+=b'xref\n0 6\n0000000000 65535 f \n'+b''.join(f'{o:010d} 00000 n \n'.encode() for o in offsets[1:])
    return out+b'trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n'+str(x).encode()+b'\n%%EOF\n'

try:
    check('health','GET','/health',auth=False)
    check('unauthorized','GET','/users/me',auth=False,expected=(401,))
    config=json.loads((ROOT/'src/mobile/android/app/google-services.json').read_text())
    key=config['client'][0]['api_key'][0]['current_key']
    email='demo-smoke-'+secrets.token_hex(6)+'@example.com'; password=secrets.token_urlsafe(24)
    firebase='https://identitytoolkit.googleapis.com/v1/accounts:'
    status,user=req('POST',firebase+'signUp?key='+key,{'email':email,'password':password,'returnSecureToken':True})
    results.append({'test':'Firebase signup','status':status,'passed':status==200}); print('Firebase signup',status,flush=True)
    if status!=200: raise RuntimeError('Firebase signup unavailable')
    status,login=req('POST',firebase+'signInWithPassword?key='+key,{'email':email,'password':password,'returnSecureToken':True})
    results.append({'test':'Firebase login','status':status,'passed':status==200}); print('Firebase login',status,flush=True)
    if status!=200: raise RuntimeError('Firebase login unavailable')
    token=login['idToken']
    me=check('authenticated identity','GET','/users/me')
    if not me: raise RuntimeError('Authenticated backend unavailable')
    results.append({'test':'identity email matches Firebase','passed':me.get('email')==email})
    check('profile update','PUT','/users/me',{'displayName':'Synthetic Demo Smoke','preferredLanguage':'en'})
    check('profile options','GET','/profile/options'); check('profile checklist','GET','/profile/checklist')
    types=check('document types','GET','/document-types')
    payload=pdf(); upload=check('request PDF upload','POST','/documents/upload-url',{'documentTypeId':types[0]['id'],'documentName':'Synthetic deployment smoke PDF','fileName':'demo-smoke.pdf','contentType':'application/pdf','fileSizeBytes':len(payload),'expiryDate':'2026-12-31T00:00:00Z'})
    if upload:
        doc=upload['documentId']; status,_=req('PUT',upload['uploadUrl'],payload,ctype='application/pdf')
        results.append({'test':'S3 presigned HTTPS upload','status':status,'passed':status==200}); print('S3 upload',status,flush=True)
        check('complete upload','POST','/documents/'+doc+'/complete')
        check('document list','GET','/documents'); check('document detail','GET','/documents/'+doc)
        access=check('download URL','GET','/documents/'+doc+'/access-url')
        if access:
            status,body=req('GET',access['url']); results.append({'test':'S3 HTTPS download matches PDF','status':status,'passed':body==payload}); print('S3 download',status,flush=True)
        analysis=check('AI document analysis','POST','/documents/'+doc+'/analyze',{})
        if analysis: results.append({'test':'analysis fields','fields':list(analysis.keys())})
        check('persisted analysis','GET','/documents/'+doc+'/analysis')
        reminder=check('reminder create','POST','/reminders',{'documentId':doc,'daysBeforeExpiry':7})
        check('reminder list','GET','/reminders')
        if reminder:
            check('reminder update','PUT','/reminders/'+reminder['id'],{'status':'Dismissed'})
            check('reminder delete','DELETE','/reminders/'+reminder['id'])
        check('document delete','DELETE','/documents/'+doc)
    check('supported translation languages','GET','/translation/languages')
    translation=check('Gemini text translation','POST','/translation/translate',{'text':'Hello, thank you.','sourceLanguage':'en','targetLanguage':'ms'})
    if translation: results.append({'test':'translated text nonempty','passed':bool(translation.get('translatedText'))})
    conv=check('assistant conversation','POST','/assistant/conversations',{'countryCode':'MY'})
    if conv:
        response=check('assistant RAG and Gemini','POST','/assistant/conversations/'+conv['id']+'/messages',{'message':'What official steps are required to renew an Employment Pass in Malaysia?'})
        if response: results.append({'test':'official source grounding','passed':response.get('responseMode')=='official_grounded' and bool(response.get('sources')),'responseMode':response.get('responseMode'),'sourceCount':len(response.get('sources') or [])})
        check('assistant history','GET','/assistant/conversations/'+conv['id'])
        check('assistant conversation delete','DELETE','/assistant/conversations/'+conv['id'])
    check('emergency AI assistance','POST','/emergency/assist',{'message':'This is a synthetic demo test. How do I tell a dispatcher I need help?','targetLanguage':'ms'})
    # Discard the local session; backend must reject the next unauthenticated call.
    token=None; check('logout no bearer','GET','/users/me',auth=False,expected=(401,))
except Exception as e:
    print('Smoke stopped:',type(e).__name__,flush=True)
    results.append({'test':'smoke execution','passed':False,'errorType':type(e).__name__})
finally:
    (ROOT/'artifacts/demo-api-smoke-results.json').write_text(json.dumps({'baseUrl':BASE,'checkedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'results':results},indent=2))
