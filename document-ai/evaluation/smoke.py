"""Real HTTP/OCR/inference smoke using synthetic input and an ephemeral unprinted key."""
import os
import secrets
import subprocess
import sys
import time
import httpx
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'tests'))
from pdf_helpers import native_pdf

def main():
    env={**os.environ,'DOCUMENT_AI_SERVICE_KEY':secrets.token_hex(32),'OLLAMA_MODEL':os.getenv('OLLAMA_MODEL','qwen3:4b')}
    server=subprocess.Popen([sys.executable,'-m','uvicorn','app.main:app','--host','127.0.0.1','--port','18090','--no-access-log'],env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    started=time.monotonic()
    try:
        with httpx.Client(timeout=230) as client:
            for _ in range(50):
                try:
                    if client.get('http://127.0.0.1:18090/ready').status_code==200: break
                except httpx.ConnectError: pass
                time.sleep(.2)
            text='PASSPORT\nName: Morgan Example\nPassport number: TEST000001\nNationality: Exampleland\nDate of expiry: 2034-01-01'
            response=client.post('http://127.0.0.1:18090/analyze',headers={'X-Service-Key':env['DOCUMENT_AI_SERVICE_KEY']},
                files={'file':('synthetic.pdf',native_pdf([text]),'application/pdf')})
            response.raise_for_status();result=response.json()
            assert result['documentCategory']=='Passport'
            assert result['evidence'] and result['statements'] and result['semanticDocument']['pages']
            assert '@' in result['modelVersion']
            print({'status':'passed','category':result['documentCategory'],'evidenceCount':len(result['evidence']),
                'statementCount':len(result['statements']),'seconds':round(time.monotonic()-started,2)})
    finally:
        server.terminate()
        try:server.wait(timeout=5)
        except subprocess.TimeoutExpired: server.kill();server.wait()
if __name__=='__main__':main()
