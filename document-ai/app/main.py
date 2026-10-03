import asyncio
import hmac
import os
import sys
import time
import logging
import shutil
import httpx
from fastapi import FastAPI, File, UploadFile, Header, HTTPException
from .schemas import Page
from .limits import IngestionLimits
from .pipeline import reason, render

app = FastAPI(title='ExpatOne Document Intelligence', docs_url=None, redoc_url=None)
app.add_middleware(IngestionLimits)
slots = asyncio.Semaphore(1)
logger = logging.getLogger("document-intelligence")


@app.get('/health')
async def health():
    return {'status':'ok','configured':bool(os.getenv('OLLAMA_MODEL') and os.getenv('DOCUMENT_AI_SERVICE_KEY'))}

@app.get('/ready')
async def ready():
    model=os.getenv('OLLAMA_MODEL')
    if not model or not os.getenv('DOCUMENT_AI_SERVICE_KEY') or not shutil.which('tesseract'):
        raise HTTPException(503,'Service not ready')
    try:
        async with httpx.AsyncClient(timeout=3) as client:
            response=await client.get(os.getenv('OLLAMA_URL','http://127.0.0.1:11434')+'/api/tags')
            response.raise_for_status()
            if model not in [item['name'] for item in response.json()['models']]:
                raise ValueError('model_missing')
        return {'status':'ready'}
    except Exception:
        raise HTTPException(503,'Service not ready') from None

@app.post('/analyze')
async def analyze(file: UploadFile = File(...), x_service_key: str = Header(default='')):
    expected = os.getenv('DOCUMENT_AI_SERVICE_KEY','')
    if not expected or not hmac.compare_digest(x_service_key,expected):
        raise HTTPException(401,'Unauthorized')
    if slots.locked(): raise HTTPException(429,'Service busy; retry later')
    async with slots:
        try:
            data = await file.read(10*1024*1024+1)
            if len(data)>10*1024*1024: raise HTTPException(413,'File too large')
            if not os.getenv('OLLAMA_MODEL'): raise HTTPException(503,'Model not configured')
            stage_started = time.monotonic()
            process = await asyncio.create_subprocess_exec(sys.executable, '-m', 'app.extract_worker', file.content_type,
                stdin=asyncio.subprocess.PIPE, stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.DEVNULL)
            try:
                output, _ = await asyncio.wait_for(process.communicate(data), timeout=120)
                if process.returncode != 0: raise ValueError('extraction_failed')
                import json
                pages = [Page.model_validate(value) for value in json.loads(output)]
            finally:
                if process.returncode is None:
                    process.kill()
                    await process.wait()
            logger.info("Extraction completed pages=%d bytes=%d duration_ms=%d",len(pages),len(data),int((time.monotonic()-stage_started)*1000))
            extraction_ms=round((time.monotonic()-stage_started)*1000)
            stage_started=time.monotonic()
            semantic, model = await reason(pages)
            logger.info("Understanding completed duration_ms=%d",int((time.monotonic()-stage_started)*1000))
            result=render(semantic,pages,model)
            result['qualityDiagnostics'].update({'extractionMs':extraction_ms,'understandingMs':round((time.monotonic()-stage_started)*1000)})
            return result
        except HTTPException: raise
        except Exception:
            # Never return parser/model messages or source text to clients/logs.
            raise HTTPException(422,'Document processing failed') from None
        finally:
            await file.close()
