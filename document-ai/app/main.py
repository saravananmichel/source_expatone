import asyncio
import hmac
import os
import sys
import time
import logging
import shutil
from contextlib import asynccontextmanager
import httpx
from fastapi import FastAPI, File, UploadFile, Header, HTTPException, Request
from .schemas import Page
from .field_candidates import field_candidates
from .structured_analysis import structured_fields, billing_category, passport_category
from .adaptive_profile import selected_profile
from .private_fallback import source_fallback
from .limits import IngestionLimits
from .pipeline import reason, render
from .ollama import InferenceOutputError
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
from .visa_forms import router as visa_forms_router
app.include_router(visa_forms_router)
app.add_middleware(IngestionLimits)
slots = asyncio.Semaphore(1)
logger = logging.getLogger("uvicorn.error")


def _fallback_reason(error):
    """Bounded diagnostic code; never expose exception text or source values."""
    if isinstance(error, asyncio.TimeoutError):
        return 'deadline_exceeded'
    if isinstance(error, httpx.TimeoutException):
        return 'model_http_timeout'
    if isinstance(error, httpx.HTTPStatusError):
        return 'model_http_status'
    if isinstance(error, httpx.RequestError):
        return 'model_transport_error'
    if isinstance(error, InferenceOutputError):
        return 'incomplete_or_invalid_inference'
    if isinstance(error, ValueError):
        return 'model_unavailable' if str(error) == 'model_unavailable' else 'validation_error'
    return 'unexpected_model_error'



# Independent text extraction: deliberately separate from Ollama analysis.
extraction_busy = False


@app.post('/extract')
async def extract_document(file: UploadFile = File(...),
                           x_service_key: str = Header(default='')):
    expected = os.getenv('DOCUMENT_AI_SERVICE_KEY', '')
    if not expected or not hmac.compare_digest(x_service_key, expected):
        raise HTTPException(401, 'Unauthorized')

    global extraction_busy
    if extraction_busy:
        raise HTTPException(429, 'Extraction service busy; retry later')

    extraction_busy = True
    try:
        try:
            content_type = file.content_type or ''
            if content_type not in ('application/pdf', 'image/jpeg', 'image/png'):
                raise HTTPException(415, 'Unsupported document type')

            data = await file.read(10 * 1024 * 1024 + 1)
            if len(data) > 10 * 1024 * 1024:
                raise HTTPException(413, 'File too large')

            process = await asyncio.create_subprocess_exec(
                sys.executable, '-m', 'app.extract_worker', content_type,
                stdin=asyncio.subprocess.PIPE,
                stdout=asyncio.subprocess.PIPE,
                stderr=None
            )
            try:
                output, _ = await asyncio.wait_for(
                    process.communicate(data), timeout=120
                )
            except asyncio.TimeoutError:
                process.kill()
                await process.wait()
                raise HTTPException(422, 'Document extraction timed out') from None

            if process.returncode != 0:
                raise HTTPException(422, 'Document text extraction failed')

            import json
            pages = json.loads(output)
            extracted_text = '\n\n'.join(
                page.get('text', '') for page in pages
            )
            validated_pages = [Page.model_validate(page) for page in pages]
            return {
                'extractedText': extracted_text,
                'pageCount': len(validated_pages),
                'fieldCandidates': field_candidates(validated_pages),
            }
        except HTTPException:
            raise
        except Exception:
            logger.warning('Independent text extraction failed')
            raise HTTPException(422, 'Document text extraction failed') from None
        finally:
            await file.close()
    finally:
        extraction_busy = False


@app.get('/health')
async def health():
    return {'status':'ok','configured':bool(selected_profile().model and os.getenv('DOCUMENT_AI_SERVICE_KEY'))}

@app.get('/ready')
async def ready():
    model = selected_profile().model
    if not model:
        logger.error("Readiness failed: model_not_configured")
        raise HTTPException(503, 'Service not ready')
    if not os.getenv('DOCUMENT_AI_SERVICE_KEY'):
        logger.error("Readiness failed: service_key_missing")
        raise HTTPException(503, 'Service not ready')
    if not shutil.which('tesseract'):
        logger.error("Readiness failed: tesseract_missing")
        raise HTTPException(503, 'Service not ready')
    try:
        await check_model()
        logger.info("Readiness passed: all_dependencies_available")
        return {'status': 'ready'}
    except httpx.TimeoutException:
        logger.error("Readiness failed: model_timeout")
    except httpx.HTTPStatusError as exc:
        logger.error("Readiness failed: model_http_status_%s", exc.response.status_code)
    except httpx.RequestError:
        logger.error("Readiness failed: model_connection_error")
    except ValueError:
        logger.error("Readiness failed: model_configuration_error")
    except Exception as exc:
        logger.error("Readiness failed: model_unexpected_error exception_type=%s", type(exc).__name__)
    raise HTTPException(503, 'Service not ready') from None

@app.post('/analyze')
async def analyze(request: Request, file: UploadFile = File(...),
                  x_service_key: str = Header(default='')):
    # FastAPI has already consumed the multipart body before this handler runs.
    async def wait_for_disconnect():
        while True:
            message = await request.receive()
            if message['type'] == 'http.disconnect':
                return

    work = asyncio.create_task(
        _analyze_impl(file, x_service_key),
        name='document-analysis'
    )
    disconnected = asyncio.create_task(
        wait_for_disconnect(),
        name='document-disconnect'
    )
    try:
        done, _ = await asyncio.wait(
            {work, disconnected},
            return_when=asyncio.FIRST_COMPLETED
        )
        if work in done:
            return await work
        # Propagate receive errors rather than silently treating them as disconnects.
        await disconnected
        logger.warning("Analysis client disconnected; cancelling processing")
        raise HTTPException(499, 'Client disconnected')
    finally:
        for task in (work, disconnected):
            if not task.done():
                task.cancel()
        await asyncio.gather(work, disconnected, return_exceptions=True)
        await file.close()


async def _analyze_impl(file, x_service_key):
    expected = os.getenv('DOCUMENT_AI_SERVICE_KEY','')
    if not expected or not hmac.compare_digest(x_service_key,expected):
        raise HTTPException(401,'Unauthorized')
    if slots.locked(): raise HTTPException(429,'Service busy; retry later')
    async with slots:
        try:
            if os.getenv("DOCUMENT_AI_ENVIRONMENT") == "Production": validate_config()
            data = await file.read(10*1024*1024+1)
            if len(data)>10*1024*1024: raise HTTPException(413,'File too large')
            profile = selected_profile()
            model_available = True
            try:
                await check_model()
            except (httpx.HTTPError, ValueError, KeyError):
                if profile.name != 'development':
                    raise HTTPException(503, 'Document model unavailable; retry later') from None
                model_available = False
            logger.info('document_ai stage=model_check available=%s profile=%s', model_available, profile.name)
            stage_started = time.monotonic()
            process = await asyncio.create_subprocess_exec(sys.executable, '-m', 'app.extract_worker', file.content_type,
                stdin=asyncio.subprocess.PIPE, stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.DEVNULL)
            try:
                output, _ = await asyncio.wait_for(process.communicate(data), timeout=120)
                if process.returncode != 0:
                    logger.warning('extraction_worker_failed returncode=%d', process.returncode)
                    raise ValueError('extraction_failed')
                import json
                pages = [Page.model_validate(value) for value in json.loads(output)]
            finally:
                if process.returncode is None:
                    process.kill()
                    await process.wait()
            logger.info("Extraction completed pages=%d bytes=%d duration_ms=%d",len(pages),len(data),int((time.monotonic()-stage_started)*1000))
            extraction_ms=round((time.monotonic()-stage_started)*1000)
            stage_started=time.monotonic()
            suggestions = structured_fields(pages)
            from .passport_fast_path import eligible_passport
            allow_layout = os.getenv('PASSPORT_LAYOUT_FAST_PATH', '0') == '1'
            fast_enabled = (
                profile.name == 'development'
                and os.getenv('DOCUMENT_OCR_ENGINE', 'tesseract').lower() == 'rapidocr'
                and os.getenv('PASSPORT_FAST_PATH', '0') == '1'
            )
            fast_passport = fast_enabled and eligible_passport(pages, suggestions, allow_layout=allow_layout)
            fast_status = ('disabled' if not fast_enabled else 'ineligible' if not fast_passport else
                           'mrz_review' if eligible_passport(pages, suggestions) else 'layout_review')
            universal_enabled = (profile.name == 'development'
                                 and os.getenv('DOCUMENT_AI_UNIVERSAL_REVIEW', '0') == '1')
            v2_enabled = universal_enabled and os.getenv('DOCUMENT_AI_UNIVERSAL_V2', '0') == '1'
            v2_route = None
            v2_fields = []
            selected_sections = []
            immigration = None
            general_route = None
            general_fields = []
            if universal_enabled and not fast_passport:
                from .immigration_review import immigration_review
                from .general_document import general_document_hints
                immigration = immigration_review(pages)
                if immigration is None:
                    general_route, general_fields = general_document_hints(pages)
                    if v2_enabled:
                        from .general_classification import general_classification
                        from .semantic_candidates import semantic_candidates
                        from .section_selection import select_sections
                        v2_route = general_classification(pages)
                        if v2_route and v2_route.subtype in {'employment_offer', 'employment_contract', 'designation_change', 'relieving_letter'}:
                            v2_fields = semantic_candidates(pages)
                        if v2_route:
                            selected_sections = select_sections(pages)
            if fast_passport:
                from .passport_fast_path import passport_review_result
                semantic, model = passport_review_result(pages, suggestions, allow_layout=allow_layout), 'passport-deterministic-review-v1'
            elif immigration is not None:
                semantic, suggestions = immigration
                model = 'immigration-deterministic-review-v1'
            elif profile.name == 'development':
                try:
                    if not model_available:
                        raise ValueError('model_unavailable')
                    from .reasoning_budget import reasoning_budget
                    budget = reasoning_budget(profile, extraction_ms / 1000 + time.monotonic() - stage_started)
                    if budget < 1:
                        raise asyncio.TimeoutError()
                    if selected_sections:
                        from .pipeline import reason_batch
                        semantic, model = await asyncio.wait_for(
                            reason_batch(pages, passages=selected_sections), timeout=budget)
                        from .private_fallback import exact_support
                        semantic = exact_support(semantic)
                        semantic._quality = {'reasoningStatus': 'partial: selected sections',
                                             'supportMethod': 'exact-source-only', 'verdictCoverage': 0.0}
                    else:
                        semantic, model = await asyncio.wait_for(reason(pages), timeout=budget)
                except (asyncio.TimeoutError, httpx.HTTPError, InferenceOutputError, ValueError, KeyError) as error:
                    # Log only the exception class: model errors may include source text.
                    logger.warning(
                        'document_ai stage=reasoning event=fallback reason_type=%s reason_code=%s',
                        type(error).__name__, _fallback_reason(error),
                    )
                    semantic, model = source_fallback(pages), 'source-only-private-fallback'
                    semantic._quality = {'reasoningStatus':'skipped: fallback', 'fallbackReason': _fallback_reason(error),
                                         'supportMethod':'exact-source-only', 'verdictCoverage':0.0}
            else:
                semantic, model = await reason(pages)
            logger.info("Understanding completed duration_ms=%d",int((time.monotonic()-stage_started)*1000))
            result=render(semantic,pages,model)
            # Independent, evidence-backed suggestions for the existing analysis
            # UI. Never silently overwrite model classification or persist fields.
            result['structuredFields'] = suggestions
            result['suggestedDocumentCategory'] = (
                'Utility Bill' if billing_category(pages, suggestions) else
                'Passport' if passport_category(pages, suggestions) else None
            )
            # V5.3: reconcile only strongly evidenced passport classifications.
            # A weak suggestion must never override a model's document category.
            from .passport_reconciliation import reconcile_passport_result
            reconcile_passport_result(result, pages, suggestions, model)
            from .passport_date_guard import guard_passport_response
            guard_passport_response(result, pages)
            if immigration is not None:
                result['suggestedDocumentCategory'] = semantic.documentCategory
                result['summary'] = (
                    'Immigration document details extracted for review. '
                    'Confirm all details against the original; this analysis does not establish legal status.'
                )
            elif general_route is not None and not result.get('suggestedDocumentCategory'):
                # Keep independent hints separate from verified model output.
                result['suggestedDocumentCategory'] = general_route.category
                existing = {f['field'] for f in suggestions}
                result['structuredFields'] = suggestions + [
                    f for f in general_fields if f['field'] not in existing
                ]
            if v2_route and not result.get('suggestedDocumentCategory'):
                result['suggestedDocumentCategory'] = v2_route.category
            if v2_route:
                result['qualityDiagnostics']['generalSubtypeHint'] = v2_route.subtype
                from .review_reconciliation import reconcile_review_fields
                result['structuredFields'], conflicts = reconcile_review_fields(
                    pages, result['structuredFields'], v2_fields)
                result['qualityDiagnostics']['reviewFieldConflicts'] = conflicts
                result['qualityDiagnostics']['structuredFieldCount'] = len(result['structuredFields'])
                result['qualityDiagnostics']['generalClassificationConflict'] = (
                    result.get('documentCategory') not in ('Unknown', 'Other', v2_route.category)
                )
                if model == 'source-only-private-fallback':
                    result['summary'] = ('Possible ' + v2_route.subtype.replace('_', ' ') +
                                         '. Review the extracted details against the original. '
                                         'AI interpretation was incomplete.')
            if v2_enabled:
                result['qualityDiagnostics']['selectedSectionCount'] = len(selected_sections)
                result['qualityDiagnostics']['partialSourceSelection'] = bool(selected_sections)
            result['qualityDiagnostics']['universalReviewStatus'] = (
                'disabled' if not universal_enabled else 'passport_priority' if fast_passport
                else 'immigration_review' if immigration is not None
                else 'general_hint' if general_route is not None else 'abstained'
            )
            # Log only counts and statuses; never log OCR text or document values.
            logger.info('document_ai stage=field_mapping category=%s field_count=%d fallback=%s',
                        result.get('documentCategory', 'Unknown'), len(suggestions),
                        model == 'source-only-private-fallback')
            result['qualityDiagnostics'].update({'extractionMs':extraction_ms,'understandingMs':round((time.monotonic()-stage_started)*1000)})
            result['qualityDiagnostics']['passportFastPathStatus'] = fast_status
            return result
        except HTTPException: raise
        except InferenceOutputError as error:
            code = str(error) if str(error) in ('output_token_limit', 'incomplete_inference', 'invalid_model_output') else 'invalid_model_output'
            logger.warning('Analysis stopped category=%s retryable=false', code)
            raise HTTPException(422, {'code': code, 'message': 'Analysis could not produce a complete verified result.'}) from None
        except httpx.TimeoutException:
            logger.warning('Analysis stopped category=model_timeout retryable=true')
            raise HTTPException(503, 'Document model unavailable; retry later') from None
        except httpx.HTTPError:
            raise HTTPException(503, 'Document model unavailable; retry later') from None
        except ValueError as error:
            if str(error).startswith('invalid_model_output:'):
                logger.warning('Analysis stopped category=invalid_model_output retryable=false')
                raise HTTPException(
                    422,
                    {'code': 'invalid_model_output',
                     'message': 'Analysis could not produce a complete verified result.'}
                ) from None
            # Do not expose parser errors or source text.
            raise HTTPException(422, 'Document processing failed') from None
        except Exception:
            # Never return parser/model messages or source text to clients/logs.
            raise HTTPException(422, 'Document processing failed') from None
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
