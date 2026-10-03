import asyncio
import hmac
import os
import sys
import time
import logging
import shutil
from contextlib import asynccontextmanager
import httpx
from fastapi import FastAPI, File, UploadFile, Header, HTTPException
from .schemas import Page
from .limits import IngestionLimits
from .pipeline import reason, render
from .config import check_model, validate_config
from .qa import AskRequest, ask_document
from fastapi.exceptions import RequestValidationError
from starlette.responses import JSONResponse

@asynccontextmanager
async def lifespan(app):
    if os.getenv('DOCUMENT_AI_VALIDATE_STARTUP', 'false').lower() == 'true':
        validate_config()
        for attempt in range(6):
            try:
                if not shutil.which('tesseract'):
                    raise ValueError('OCR unavailable')
                await check_model()
                break
            except (httpx.HTTPError, ValueError, KeyError):
                if attempt == 5:
                    raise RuntimeError('Document service dependencies unavailable') from None
                await asyncio.sleep(2)
    yield

app = FastAPI(title='ExpatOne Document Intelligence', docs_url=None, redoc_url=None, openapi_url=None, lifespan=lifespan)
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
        await check_model()
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
            if os.getenv("DOCUMENT_AI_ENVIRONMENT") == "Production": validate_config()
            data = await file.read(10*1024*1024+1)
            if len(data)>10*1024*1024: raise HTTPException(413,'File too large')
            if not os.getenv('OLLAMA_MODEL'): raise HTTPException(503,'Model not configured')
            # Fail before parsing sensitive uploads when inference is unavailable.
            try:
                await check_model()
            except (httpx.HTTPError, ValueError, KeyError):
                raise HTTPException(503, 'Document model unavailable; retry later') from None
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
        except httpx.HTTPError:
            raise HTTPException(503, 'Document model unavailable; retry later') from None
        except Exception:
            # Never return parser/model messages or source text to clients/logs.
            raise HTTPException(422,'Document processing failed') from None
        finally:
            await file.close()


@app.exception_handler(RequestValidationError)
async def invalid_request(request, exc):
    # FastAPI's default validation errors can echo submitted source/question text.
    return JSONResponse({'detail': 'Invalid document request'}, status_code=422)

@app.post('/ask')
async def ask(request: AskRequest, x_service_key: str = Header(default='')):
    expected = os.getenv('DOCUMENT_AI_SERVICE_KEY', '')
    if not expected or not hmac.compare_digest(x_service_key, expected):
        raise HTTPException(401, 'Unauthorized')
    if slots.locked(): raise HTTPException(429, 'Service busy; retry later')
    async with slots:
        try:
            if os.getenv('DOCUMENT_AI_ENVIRONMENT') == 'Production': validate_config()
            async with asyncio.timeout(50):
                await check_model()
                return await ask_document(request)
        except httpx.HTTPError:
            raise HTTPException(503, 'Local document Q&A unavailable; retry later') from None
        except Exception:
            # Never include source, questions, prompts, or model output in errors/logs.
            raise HTTPException(503, 'Local document Q&A could not produce a verified answer') from None
