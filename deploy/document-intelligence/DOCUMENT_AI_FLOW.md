# Production document AI: private Qwen only

Document routes no longer depend on `IAIService`/Gemini. `GeminiAIService.AnalyzeDocumentAsync` is a compatibility guard that throws before reading the file or making a request. Its document prompts and Base64 upload implementation have been removed. Other Gemini features remain registered when a key is configured.

## Flow A — upload and analysis

| Step | Exact relative file and class/function | Destination/data |
|---|---|---|
| Select/upload | `src/mobile/lib/features/documents/add_document_screen.dart` — `_AddDocumentScreenState._upload()` | Calls mobile document service |
| Request URL | `src/mobile/lib/core/services/document_service.dart` — `DocumentService.requestUploadUrl()`; `src/mobile/lib/core/networking/api_client.dart` — `ApiClient.post()`, `_getHeaders()` | `POST /api/documents/upload-url`, metadata and Firebase bearer token |
| Prepare upload | `src/backend/ExpatOne.Api/Controllers/DocumentsController.cs` — `DocumentsController.RequestUpload()` → `src/backend/ExpatOne.Infrastructure/Services/DocumentService.cs` — `RequestUploadAsync()` | PostgreSQL document metadata |
| Sign upload | `src/backend/ExpatOne.Infrastructure/Services/S3StorageService.cs` — `GeneratePresignedUploadUrl()` | Presigned S3 PUT URL |
| Upload original | `src/mobile/lib/core/services/document_service.dart` — `DocumentService.uploadFileToS3()` | Original bytes and MIME type directly to private AWS S3 |
| Complete | Same mobile service — `completeUpload()` → `DocumentsController.CompleteUpload()` → `DocumentService.CompleteUploadAsync()` | `POST /api/documents/{id}/complete`; marks active and queues local analysis |
| Explicit analysis/reanalysis | `src/mobile/lib/features/documents/document_analysis_screen.dart` — `_DocumentAnalysisScreenState._analyzeDocument()` → `DocumentService.analyzeDocument()` → `DocumentsController.AnalyzeDocument()` / `Reanalyze()` | `POST /api/documents/{id}/analyze` or `/reanalyze`; returns queued job; 503 if jobs unavailable |
| Queue | `src/backend/ExpatOne.Infrastructure/Services/DocumentAnalysisJobs.cs` — `EnqueueAsync()`; `DocumentAnalysisConfiguration.cs` — `Fingerprint()` | S3 download/hash, version and run in PostgreSQL; accepts Local/no fallback only |
| Worker | `src/backend/ExpatOne.Api/Services/DocumentIntelligenceWorker.cs` — `ExecuteAsync()` → `ProcessOne()` | S3 via `S3StorageService.DownloadFileAsync()`; bounded temporary original file |
| Private analysis request | `src/backend/ExpatOne.Infrastructure/Services/DocumentIntelligenceService.cs` — `AnalyzeWithConfigurationAsync()` | Multipart `POST http://127.0.0.1:8090/analyze`, `X-Service-Key` |
| Receive | `document-ai/app/main.py` — `analyze()`; `document-ai/app/limits.py` — `IngestionLimits.__call__()` | Authorized, bounded private file ingestion |
| Process | `document-ai/app/extract_worker.py` — module `__main__` → `document-ai/app/extraction.py` — `extract()` / `ocr()` | Local PDFium/pypdf/Pillow/Tesseract; no OCR API |
| Extraction/reasoning | `document-ai/app/pipeline.py` — `reason()` / `reason_batch()`, `PROMPT`, `extraction_schema()` | Extracted source pages to private Ollama |
| Whole-document reasoning | `document-ai/app/reasoning.py` — `document_reasoning()` or `hierarchical_document_reasoning()` / `build_synthesis_digest()` | Source evidence/digest to private Ollama |
| Support review | `document-ai/app/support.py` — `review_support()`, `JUDGE` | Claims/cited evidence to private Ollama |
| Transport/model | `document-ai/app/ollama.py` — `post_chat()`; `document-ai/app/config.py` — `ollama_base_url()` | `POST http://ollama:11434/api/chat`, `qwen3:4b`; production startup/request validation also pins support model |
| Render/return | `document-ai/app/pipeline.py` — `render()` → `main.py` — `analyze()` → `DocumentIntelligenceService.AnalyzeWithConfigurationAsync()` | JSON including source pages/evidence |
| Persist | `DocumentIntelligenceWorker.ProcessOne()` | PostgreSQL `DocumentAnalysisRun.ResultJson`, `Document.ExtractedMetadata`; removes temporary file |
| Poll/display | `DocumentsController.AnalysisStatus()` → `DocumentAnalysisJobs.GetAsync()` / `Map()` → `DocumentService.analyzeDocument()` → `_DocumentAnalysisScreenState._analyzeDocument()` | Saved JSON to Flutter; polling uses separate `queryParams` |
| Reopen/display | `DocumentsController.GetAnalysis()` → `DocumentAnalysisService.GetDocumentAnalysisAsync()` → `DocumentService.getDocumentAnalysis()` → `_DocumentAnalysisScreenState._loadAnalysis()` | Validated current-version Local/Qwen saved result |

## Flow B — document Q&A

| Step | Exact relative file and class/function | Destination/data |
|---|---|---|
| Ask | `src/mobile/lib/features/documents/document_qa_screen.dart` — `_DocumentQAScreenState._askQuestion()` | Mobile document service |
| HTTP | `src/mobile/lib/core/services/document_service.dart` — `DocumentService.askDocument()` → `ApiClient.post()` | Authenticated `POST /api/documents/{id}/ask`, question |
| Controller | `src/backend/ExpatOne.Api/Controllers/DocumentsController.cs` — `DocumentsController.AskDocument()` | Local document analysis service; 503 when unavailable |
| Retrieve | `src/backend/ExpatOne.Infrastructure/Services/DocumentAnalysisService.cs` — `AskDocumentAsync()` / `GetDocumentAnalysisAsync()` | Authorizes owner; reads PostgreSQL completed/review results matching current object/version/hash, Provider Local and qwen3:4b model |
| Missing/invalid context | Same — `AskDocumentAsync()` → `DocumentAnalysisJobs.EnqueueAsync(force: true)` | Queues local extraction only when valid current analysis is missing; returns 409 processing message. Active jobs deduplicate. Never downloads the original for a ready Q&A request |
| Retrieve snippets | `src/backend/ExpatOne.Infrastructure/Services/DocumentContextSelector.cs` — `Select()` | Deterministic lexical ranking with inverse document frequency; source page chunks, blocks and evidence; at most six snippets/6,000 characters. No external embedding call |
| Private request | `src/backend/ExpatOne.Infrastructure/Services/DocumentIntelligenceService.cs` — `AskAsync()` | `POST http://127.0.0.1:8090/ask`, question/category/selected source IDs/pages/text, `X-Service-Key` |
| Authenticate/bound | `document-ai/app/limits.py` — `IngestionLimits.__call__()`; `document-ai/app/main.py` — `ask()` | Auth before JSON parsing; 64 KiB body limit, schema/question/context bounds; shared inference semaphore; 50-second total Q&A ceiling |
| Answer | `document-ai/app/qa.py` — `ask_document()`, `PROMPT` | Qwen JSON answer and citation IDs; only supplied source evidence |
| Transport | `document-ai/app/ollama.py` — `stream_chat()`; `config.py` — `ollama_base_url()` | `POST http://ollama:11434/api/chat`, exact model `qwen3:4b` |
| Verify | `qa.ask_document()` with `support.JUDGE` / `support.Review` | Same private Qwen reviews answer against server-owned source quotations; unknown/duplicate IDs, unsupported numbers, incomplete/unsupported verdicts fail closed |
| Return | `main.ask()` → `DocumentIntelligenceService.AskAsync()` → `DocumentAnalysisService.AskDocumentAsync()` → `DocumentsController.AskDocument()` | Answer, grounded flag and exact source citations; backend checks citation ID/page/text again; no inference diagnostics |
| Display | `DocumentAnswer.fromJson()` / `DocumentAnswerEvidence.fromJson()` in mobile service → `_DocumentQAScreenState._askQuestion()` / `_buildQAEntry()` | Flutter answer and page/source quotations |

`grounded=true` means the automatic source and model support checks passed; a model judge is fallible, not a proof of semantic correctness. Unrelated questions and insufficient evidence return an explicit answer with `grounded=false` and no citations. Lexical retrieval has limited recall for synonyms and cross-language questions; it fails closed rather than using external search.

## Privacy and deployment

- EC2 retains Local, fallback=false, qwen3:4b for extraction/reasoning/support/Q&A, OLLAMA_NO_CLOUD=1. Ports 8090 and 11434 remain unpublished; Python shares API loopback.
- Outside development, ASP.NET permits only loopback document-ai service URLs. Python production permits Ollama at loopback, Docker hostname `ollama`, or RFC1918 IP addresses; external cloud URLs are rejected. Production request validation applies even if startup validation is disabled.
- Unsafe Gemini/Hybrid/fallback configurations cannot be newly queued. Old unsafe queued provider snapshots fail locally; they never call Gemini. Requeue such documents under Local if needed.
- Documents persist in S3; extracted pages/evidence/results persist in PostgreSQL. Questions and Q&A answers are not persisted by this feature. No original file download/OCR happens for ready Q&A. Python Q&A has a 50-second total ceiling and ASP.NET a 55-second local request ceiling, within Flutter's existing 60-second AI timeout; timeouts were not increased.
- Application logs omit raw files, source text, prompts, questions, model responses and filenames. User identifiers were removed from document upload/share/delete log messages. Audit database rows retain identifiers for ownership/history.
- The API returns only answer/citations, not private model diagnostics. JSON validation errors do not echo private request inputs.

## DATA LEAVES OUR INFRASTRUCTURE

For production document functionality:

- **Gemini/Google AI:** no request path. Document routes have no `IAIService` dependency; Gemini document requests throw before HTTP. Backend and Python tests intercept outgoing document HTTP and restrict it to private document-ai/Ollama. The real CPU benchmark runs on an internal Docker network with no public ports and guards every outgoing Python HTTP request.
- **Google authentication:** existing Firebase/Google login still handles authentication data, separate from documents; no source text/question/context/answer is passed by the document feature to those SDK calls.
- **Ollama Cloud:** no request path; requests use private Ollama, cloud disabled in deployment, external production Ollama URLs rejected.
- **Other external AI providers:** none in the production document path. Existing pgvector/Gemini embeddings support government knowledge, not uploaded-document Q&A.
- **AWS:** original files go to private S3; document metadata and analysis go to configured PostgreSQL (AWS when deployed there). Flutter exchanges questions/results with the configured API. This implementation/test report is not evidence of a new AWS deployment or a packet capture from live AWS.

## Remaining Gemini use outside uploaded-document workflows

`src/backend/ExpatOne.Api/Program.cs` still registers `GeminiAIService` as `IAIService` for a configured Gemini key:

- `src/backend/ExpatOne.Infrastructure/Services/AssistantService.cs` — `SendMessageAsync()` calls `GenerateResponseAsync()` for the Government Assistant.
- `src/backend/ExpatOne.Infrastructure/Services/TranslationService.cs` — `TranslateAsync()` calls `IAIService.TranslateAsync()` for text translation.
- `src/backend/ExpatOne.Infrastructure/Services/EmergencyAssistService.cs` — `AssistAsync()` calls `GenerateResponseAsync()` and optional `TranslateAsync()` for emergency guidance/messages.
- `src/backend/ExpatOne.Infrastructure/Services/KnowledgeIngestionService.cs` — `GenerateEmbeddingsAsync()` calls `GenerateEmbeddingAsync()` for curated government knowledge.
- `src/backend/ExpatOne.Infrastructure/Services/KnowledgeSearchService.cs` — `SearchAsync()` calls `GenerateEmbeddingAsync()` for knowledge retrieval queries.
- `document-ai/evaluation/run.py` — `gemini()` remains an explicitly selected synthetic-corpus comparator, using GEMINI_EVALUATION_MODEL. `evaluation/quality.py::run()` can invoke it. Neither is imported by the production API request path.

Gemini generative requests use `https://generativelanguage.googleapis.com/v1beta/models/{Gemini:Model}:generateContent` (default gemini-2.5-flash); knowledge embeddings use the configured `Gemini:EmbeddingModel` (default gemini-embedding-2) with `:embedContent`. These unrelated features remain intact.


## Validation and CPU performance (2026-10-03)

- Backend: 419 tests in the full suite; document-only run: 116 tests, zero Google AI request entries. Tests cover private HTTP routing, no Gemini fallback, missing/invalid/stale analysis, ownership, registration without a Gemini key, bounded retrieval and citation integrity.
- Python: 86 tests, including authorized/unauthorized private `/ask`, document ingestion through local request guards, exact Qwen model, insufficient evidence, malformed output, production URL/model rejection and safe failures.
- Flutter: 208 tests; static analysis clean. Polling regression verifies the URI path plus separate analysisId query parameter. Source citations deserialize and display.
- Both application Docker images build. Real Qwen inference was benchmarked on an internal Docker network with no published ports. All outgoing benchmark HTTP was guarded to private Ollama `/api/tags` and `/api/chat`. Runtime network metadata is in `document-ai/evaluation/reports/document-qa/verification.json`.

Final CPU repeat (Linux aarch64 Docker on the local Mac; 10 Docker vCPUs, ~7.75 GiB VM memory, no GPU):

| Case | Context characters | First Ollama token | Total endpoint latency | Prompt tokens (answer / support) | Output tokens/sec (answer / support) | Result |
|---|---:|---:|---:|---:|---:|---|
| Cold, short | 87 | 21.01 s | 50.01 s | 198 / support did not finish | 3.57 / unavailable | Local 503 at total ceiling |
| Warm, short | 87 | 0.565 s | 21.30 s | 198 / 352 | 4.14 / 4.72 | Grounded answer + citation |
| Warm, bounded | 5,698 | 15.85 s | 44.30 s | 959 / 457 | 3.84 / 3.47 | Grounded answer + citation |

An earlier run overlapping other work also timed out on the bounded case. It is retained as `cpu-under-load.json` rather than discarded. CPU performance is variable and the cold path is not reliably within the existing mobile deadline; production hardware/resident-model behavior must be measured before rollout. No timeout was increased to mask this. Cold failure returns a safe local error and never invokes another provider.

First-token timing measures Ollama content arriving at the private Python service. Flutter receives a buffered answer only after citation and support checks, so visible delivery latency is the total endpoint latency. Prompt tokens are Ollama-reported counts, not estimates. Observed maximum container memory was ~4.17 GiB for Ollama and ~41 MiB for the Python Q&A process. Docker memory samples and observed maxima are in `memory.json`; they are container accounting measurements, not a guaranteed instantaneous RSS peak. No user documents were used in these tests/benchmarks.

Artifacts: `document-ai/evaluation/reports/document-qa/{cpu.json,cpu-under-load.json,memory.json,memory-under-load.json,verification.json}`. This work modifies and verifies local code; it does not deploy changes to AWS or certify an existing live AWS network.
