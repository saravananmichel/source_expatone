"""Actual API image runtime; inject existing credentials in environment, never output values."""
import os,json,subprocess,time,re,urllib.request,urllib.error,http.client
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
s=json.loads(Path('/Users/austincelestia/.microsoft/usersecrets/f7b90b48-ab3d-44e1-9810-58695895c9d5/secrets.json').read_text(encoding='utf-8-sig'))
credentials=subprocess.run(['aws','configure','export-credentials','--profile','expatone-dev','--format','process'],capture_output=True,check=True)
c=json.loads(credentials.stdout)
env={**os.environ,'ASPNETCORE_ENVIRONMENT':'Development','DocumentIntelligence__Enabled':'false',
 'ConnectionStrings__DefaultConnection':re.sub(r'(?i)(Host=)(localhost|127\.0\.0\.1)',r'\1host.docker.internal',s['ConnectionStrings:DefaultConnection']),
 'Firebase__ProjectId':s['Firebase:ProjectId'],'Firebase__CredentialJson':Path(s['Firebase:CredentialPath']).read_text(),
 'Aws__Region':s['Aws:Region'],'Aws__S3BucketName':s['Aws:S3BucketName'],
 'AWS_ACCESS_KEY_ID':c['AccessKeyId'],'AWS_SECRET_ACCESS_KEY':c['SecretAccessKey'],'AWS_SESSION_TOKEN':c.get('SessionToken','')}
name='expat-quality-api-runtime';args=['docker','run','--rm','-d','--name',name,'--memory','1g','--cpus','1','--security-opt','no-new-privileges','--cap-drop','ALL','-p','127.0.0.1:5087:8080']
for key in env:
 if key not in os.environ or key in ['ASPNETCORE_ENVIRONMENT']:args+=['-e',key]
args+=['expatone-document-api:local']
subprocess.run(args,env=env,capture_output=True,check=True);results=[]
def req(path):
 try:
  with urllib.request.urlopen('http://127.0.0.1:5087/api'+path,timeout=5) as r:return r.status
 except urllib.error.HTTPError as e:return e.code
try:
 for _ in range(30):
  try:
   if req('/health')==200:break
  except (urllib.error.URLError,http.client.RemoteDisconnected,TimeoutError):pass
  time.sleep(1)
 for path,expect in [('/health',200),('/documents',401)]:
  status=req(path);results.append({'path':path,'status':status,'passed':status==expect})
finally:subprocess.run(['docker','stop',name],capture_output=True)
(ROOT/'document-ai/evaluation/reports/api-container-runtime.json').write_text(json.dumps(results,indent=2));print(json.dumps(results))
