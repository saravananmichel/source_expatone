"""Hardened container liveness/readiness/authentication; no source content logging."""
import secrets,subprocess,time,json,sys,urllib.request,urllib.error,http.client
from pathlib import Path
key=secrets.token_urlsafe(32);name='expat-quality-runtime';base='http://127.0.0.1:18093'
results=[]
subprocess.run(['docker','run','--rm','-d','--name',name,'--read-only','--tmpfs','/tmp:size=134217728,mode=1777','--memory','2g','--cpus','2','--cap-drop','ALL','--security-opt','no-new-privileges','-p','127.0.0.1:18093:8090','-e','DOCUMENT_AI_SERVICE_KEY='+key,'-e','OLLAMA_URL=http://host.docker.internal:11434','-e','OLLAMA_MODEL=qwen3:4b','expatone-document-ai:local'],capture_output=True,check=True)
def req(path,method='GET'):
 try:
  with urllib.request.urlopen(urllib.request.Request(base+path,method=method),timeout=5) as r:return r.status
 except urllib.error.HTTPError as e:return e.code
try:
 for _ in range(30):
  try:
   if req('/health')==200:break
  except (urllib.error.URLError,http.client.RemoteDisconnected,TimeoutError):pass
  time.sleep(1)
 for name2,path,method,expect in [('health','/health','GET',200),('ready','/ready','GET',200),('auth-before-multipart','/analyze','POST',401)]:
  status=req(path,method);results.append({'name':name2,'status':status,'passed':status==expect})
finally:subprocess.run(['docker','stop',name],capture_output=True)
Path('document-ai/evaluation/reports/container-runtime.json').write_text(json.dumps(results,indent=2));print(json.dumps(results))
