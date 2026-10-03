"""Start isolated loopback verification services with an ephemeral, unprinted service key."""
import os,secrets,subprocess,json,time,signal
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
key=secrets.token_urlsafe(32)
env={**os.environ,'DOCUMENT_AI_SERVICE_KEY':key,'OLLAMA_MODEL':'qwen3:4b','OCR_LANGUAGES':'eng+msa+tam+chi_sim',
 'ASPNETCORE_ENVIRONMENT':'Development','AWS_PROFILE':'expatone-dev',
 'DocumentIntelligence__Enabled':'true','DocumentIntelligence__Provider':os.getenv('QUALITY_PROVIDER','Local'),
 'DocumentIntelligence__ConfigurationVersion':'quality-v2','DocumentIntelligence__ServiceKey':key,
 'DocumentIntelligence__ServiceUrl':'http://127.0.0.1:18092/','DocumentIntelligence__EnableFallback':os.getenv('QUALITY_FALLBACK','false')}
logs=[open('/tmp/expat-quality-ai-service.log','w'),open('/tmp/expat-quality-api-service.log','w')]
processes=[subprocess.Popen([str(ROOT/'document-ai/.venv/bin/python'),'-m','uvicorn','app.main:app','--host','127.0.0.1','--port','18092','--no-access-log'],cwd=ROOT/'document-ai',env=env,stdout=logs[0],stderr=subprocess.STDOUT),
 subprocess.Popen(['dotnet','run','--no-launch-profile','--project',str(ROOT/'src/backend/ExpatOne.Api'),'--urls','http://127.0.0.1:5088'],cwd=ROOT,env=env,stdout=logs[1],stderr=subprocess.STDOUT)]
if os.getenv('QUALITY_DISABLE_AI')=='true':
 processes[0].terminate();processes[0].wait(timeout=10);processes=processes[1:]
def shutdown(signum,frame):
 raise SystemExit(0)
signal.signal(signal.SIGTERM,shutdown)
# API remains loopback-only (Android emulator maps 10.0.2.2 to host loopback); private model remains loopback-only.
try:
 while all(p.poll() is None for p in processes):time.sleep(5)
finally:
 for p in processes:
  p.terminate()
 for p in processes:
  try:p.wait(timeout=10)
  except subprocess.TimeoutExpired:p.kill()
 for l in logs:l.close()
