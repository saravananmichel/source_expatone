# ExpatOne Document Intelligence implementation

## Repository audit and integration

The existing modular ASP.NET Core backend already implements Firebase authentication, owner-scoped
S3 uploads/access URLs, document entities, versioning, sharing and audit records. PostgreSQL and
pgvector are configured in `ExpatOneDbContext`; pgvector powers knowledge retrieval, not document
analysis. `IAIService`/`GeminiAIService` remain in use for chat, translation and legacy document Q&A.
`DocumentAnalysisService` previously performed a synchronous Gemini request and overwrote
`Document.ExtractedMetadata`. The existing Flutter analysis screen displayed that JSON.
Hosted reminder processing provides the background-service convention reused here. Existing ECS
API Docker support remains in `src/backend/Dockerfile`; no GPU inference infrastructure existed.
Existing backend unit/security/API tests and Flutter widget tests were preserved.

## Implemented flow

With `DocumentIntelligence__Enabled=true`, upload completion queues an analysis. Explicit analyze
and reanalyze requests return HTTP 202 with an analysis ID. A PostgreSQL-backed worker leases one
job, downloads the private object into a temporary file, checks its size and SHA-256, invokes the
configured provider, persists the result and updates the current document's display cache.
The queue captures the object key/content type/document version before work starts, retries transient
failures with backoff, recovers expired leases, and caps attempts. A partial unique index prevents
concurrent duplicate active jobs. Authorization checks document ownership before any storage read.
Enqueue hashes actual bytes; cache reuse requires the same content, configuration fingerprint and
successful status. Forced reanalysis creates a new record after an active job finishes.
Changed bytes between enqueue and processing fail safely rather than producing a stale analysis.

The local Python service validates file signatures, uses PDFium native text/geometry when available,
renders scanned pages for Tesseract, preserves pages and text block boxes, labels basic layout
patterns, corrects confident Tesseract orientation detections, and calls a configurable Ollama model with a constrained Pydantic schema. Dense pages
are split into bounded groups without silently dropping source text. The semantic result contains
facts, entities, dates, money, sections, clauses, obligations, conditions, relationships and findings
when extracted. Evidence quotations and original values must match the source; references and
explanation numbers are checked. Deterministic postprocessing normalizes explicit ISO dates and
currency values, flags approaching/passed expiry and potentially contradictory labelled values.
The renderer produces document-specific descriptive text from validated statements. Fact and
interpretation kinds stay distinct. All local outputs currently require review: quotation matching
cannot prove that an explanation is semantically entailed by its evidence.

`Local`, `Gemini`, and `Hybrid` are selectable through configuration. `EnableFallback` explicitly
allows Gemini on eligible local failures. Gemini's existing analysis lacks page evidence and is
marked low confidence/requires review. Fallback is disabled in the example configuration.

Flutter polls the requested analysis ID, displays processing stages, review notices, readable
statement cards, original evidence quotes, analysis history and source-page links using the
existing private presigned access mechanism. Legacy results keep their existing presentation.

## Data migration

`20261002064605_AddDocumentAnalysisRuns` adds `document_analysis_runs` with durable status/lease,
attempt count, source snapshot, content/configuration hashes, immutable result JSON and timestamps.
The result contains semantic data and provider/model/analyzer/prompt versions. Existing document
and document-version tables are reused; no redundant fact/entity tables were added. Historical
`DocumentVersionId` is a retained identifier rather than a cascading foreign key so deleting a
file version does not erase its analysis history. It may make its source file unavailable.
The migration was applied to the configured local development database during implementation.

## API

- `POST /api/documents/{id}/analyze`: queue/reuse; `{ "forceReanalyze": false }`.
- `POST /api/documents/{id}/reanalyze`: new analysis after an active attempt finishes.
- `GET /api/documents/{id}/analysis/status?analysisId={analysisId}`: exact job state/result.
- `GET /api/documents/{id}/analysis/{analysisId}`: historical job/result.
- `GET /api/documents/{id}/analysis/history`: newest 100 runs.
- `GET /api/documents/{id}/analysis`: latest job result, with legacy cache compatibility.

Statuses: QUEUED, PROCESSING, COMPLETED, FAILED, REQUIRES_REVIEW.
Every route is authenticated and owner-scoped. Existing rate limits apply to analyze/reanalyze.
`POST .../ask` remains the existing Gemini document Q&A feature; local Q&A is not implemented.

## Exact local commands

From the repository root (the directory name has a trailing space; quote it when using absolute paths):

```sh
brew install python@3.12 tesseract ollama
# Optional multilingual traineddata on macOS:
brew install tesseract-lang
python3.12 -m venv document-ai/.venv
document-ai/.venv/bin/pip install -r document-ai/requirements.txt
OLLAMA_NO_CLOUD=1 ollama serve
```

In another terminal:

```sh
ollama pull qwen3:4b
cd document-ai
# Set DOCUMENT_AI_SERVICE_KEY to a new private random secret through your environment.
export OLLAMA_MODEL=qwen3:4b
export OLLAMA_URL=http://127.0.0.1:11434
export OCR_LANGUAGES=eng
.venv/bin/python -m app
```

In the backend terminal, set the SAME service key without displaying it:

```sh
export DocumentIntelligence__Enabled=true
export DocumentIntelligence__Provider=Local
export DocumentIntelligence__EnableFallback=false
export DocumentIntelligence__ServiceUrl=http://127.0.0.1:8090/
export DocumentIntelligence__ServiceKey="$DOCUMENT_AI_SERVICE_KEY"
export DocumentIntelligence__ConfigurationVersion=engine-v1-grounding-v2-qwen3-4b-359d7dd4bcda-ocr-eng
export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ROOT=/opt/homebrew/opt/dotnet@8/libexec
# If EF CLI is not installed:
dotnet tool install dotnet-ef --version 8.0.31 --tool-path .tools
.tools/dotnet-ef database update --project src/backend/ExpatOne.Infrastructure --startup-project src/backend/ExpatOne.Api
dotnet run --project src/backend/ExpatOne.Api
```

Use the existing mobile launch configuration and authenticated upload flow:

```sh
cd src/mobile
flutter run --dart-define-from-file=config/demo.json
```

That launch file is part of existing local work; verify its API URL/Firebase setup for your device.
Do not commit secrets. Increment `ConfigurationVersion` whenever the model, OCR languages, schema,
reasoning prompt, preprocessing or other analysis behavior changes, so completed results are not
reused under changed configuration. Ollama tags are mutable; results record the full model digest.

Required existing backend configuration remains: `ConnectionStrings__DefaultConnection`,
`Aws__Region`, `Aws__S3BucketName`, AWS execution-role credentials, and Firebase `ProjectId` with
`CredentialPath`/`CredentialJson` or application-default credentials. `Gemini__ApiKey` and the existing
Gemini model configuration are needed only for Gemini/fallback and other existing AI features.
No secret values are included here. `.env.example` lists the model-service variables.

## Tests

```sh
dotnet build src/backend/ExpatOne.sln
dotnet test src/backend/ExpatOne.Tests
cd src/mobile
flutter analyze
flutter test
cd ../../document-ai
.venv/bin/python -m pytest -q
.venv/bin/pip-audit -r requirements.txt
OLLAMA_MODEL=qwen3:4b PYTHONPATH=. .venv/bin/python -m evaluation.run
```

The new tests cover job ownership/cache/history, HTTP 202/status/history/owner isolation, a
relational worker's expired-lease recovery and result persistence, invalid signatures, bounded
requests, native multipage PDFs, scanned PDF/PNG OCR, evidence/value/reference rejection,
normalization and attention rules, and multipart-to-analysis integration with a stubbed model.
The separate synthetic evaluation uses real local inference; it is not a mocked benchmark.
See `document-ai/evaluation/results.local.json` and its README for measured results and limitations.

## Production deployment requirements and limits

Both API and document-AI Docker builds were verified. The AI container passed liveness, model
readiness, authentication rejection, invalid-file handling, native PDF extraction and PNG OCR under
read-only filesystem/resource limits. `compose.yaml` provides a bounded local CPU topology:

```sh
# Set DOCUMENT_AI_SERVICE_KEY securely first.
docker compose -f document-ai/compose.yaml up --build -d
```

Install its model with `docker compose -f document-ai/compose.yaml exec ollama ollama pull qwen3:4b` after startup.
On Apple Silicon, native Ollama uses the host GPU; the Linux container topology needs separate GPU
runtime/device setup to use GPU hardware. Fargate can host the API and CPU OCR; use a separately
provisioned GPU host/service for predictable model latency. Pin model digests/container images in
production. Supply keys via Secrets Manager, restrict service ingress, use HTTPS/mTLS between
hosts, and terminate TLS for the model service through a private proxy. The backend rejects
non-HTTPS non-loopback model URLs outside development. Keep Ollama internal and disable cloud
features. Enforce private S3/block-public-access, default SSE/KMS encryption, TLS-only access,
least-privilege object access and encrypted PostgreSQL/temp volumes. Managed S3 uploads explicitly
request AES256; presigned uploads rely on the bucket's enforced default encryption.

Health: `/health` for liveness; `/ready` checks configured key, OCR executable and installed model.
OCR parsing runs in a disposable subprocess with a CPU limit and Linux 1 GiB address-space cap,
120-second extraction timeout, 40-page/10-MiB/100k-character limits, and image dimension limits.
Requests are authenticated and byte-bounded before multipart spooling. One inference request runs
per service process. Whole requests have a 900-second budget; backend requests have a 240-second
budget and jobs a 300-second budget. Large documents may exceed that budget and fail safely.
Scale by adding workers/model replicas after measuring representative workloads. Logs omit source
text/prompts/model output and use analysis IDs/status categories; original file keys were removed
from S3 success/deletion logs. Existing unrelated service logging requires its own security audit.

Not verified: AWS deployment,
real S3/Firebase/device end-to-end uploads, production GPU latency, Malay/Tamil/Chinese model quality,
handwriting, rotation on real-world scans, sophisticated table/form/column reconstruction, signature/stamp
recognition, semantic entailment, calibrated confidence, and broad model superiority. OCR language
packs can be selected with `eng+msa+tam+chi_sim`; availability does not establish accuracy.
History is exposed in Flutter; source-page opening depends on the external viewer's PDF fragment
support and does not highlight boxes. Initial uploads receive a source-version snapshot when queued so subsequent version changes preserve source navigation.
Job cancellation on navigation is not implemented; background work persists for later retrieval.

Next improvements: independently annotate multilingual/rotated/low-quality/table fixtures,
measure OCR CER/WER and entity precision/recall, review summaries/actions for usefulness and
entailment, benchmark alternative OCR/layout and larger models, add source highlighting/in-app PDF
viewing, local grounded Q&A and production load/security testing.

## Model and license choice

Tesseract provides CPU OCR; PDFium/pypdf preserve native PDF text and geometry without introducing
an AGPL PDF-library dependency. Qwen3:4b through Ollama is a development candidate sized for this
16-GiB Apple Silicon machine, not a proven universal production model. Its weights use Apache 2.0
([Qwen license](https://huggingface.co/Qwen/Qwen3-4B/blob/main/LICENSE)); PDFium wrapper licensing is
[Apache/BSD](https://github.com/pypdfium2-team/pypdfium2/blob/main/README.md). Ollama's
[structured output interface](https://github.com/ollama/ollama/blob/main/docs/capabilities/structured-outputs.mdx)
is used with independent Pydantic/evidence validation. Retain third-party notices in distributions.

## Files changed for this feature

Existing files extended: `.gitignore`; `src/backend/ExpatOne.Api/Program.cs`;
`src/backend/ExpatOne.Api/Controllers/DocumentsController.cs`;
`src/backend/ExpatOne.Application/DTOs/DocumentAnalysisDto.cs`;
`src/backend/ExpatOne.Infrastructure/ExpatOne.Infrastructure.csproj`;
`src/backend/ExpatOne.Infrastructure/Persistence/ExpatOneDbContext.cs`;
`src/backend/ExpatOne.Infrastructure/Persistence/Migrations/ExpatOneDbContextModelSnapshot.cs`;
`src/backend/ExpatOne.Infrastructure/Services/DocumentVersionService.cs`;
`src/backend/ExpatOne.Infrastructure/Services/S3StorageService.cs`;
`src/backend/ExpatOne.Tests/ExpatOne.Tests.csproj`;
`src/mobile/lib/core/services/document_service.dart`;
`src/mobile/lib/features/documents/document_analysis_screen.dart`.

New backend files: `Services/DocumentIntelligenceWorker.cs` and
`appsettings.DocumentIntelligence.example.json` in the API; application
`Interfaces/IDocumentIntelligenceService.cs` and `DTOs/AnalysisJobDto.cs`;
domain `Entities/DocumentAnalysisRun.cs`; infrastructure `Services/DocumentAnalysisJobs.cs`,
`Services/DocumentIntelligenceService.cs`, `Persistence/Configurations/DocumentAnalysisRunConfiguration.cs`,
and migration `20261002064605_AddDocumentAnalysisRuns.cs` with its designer.
New backend tests: `DocumentAnalysisJobsTests.cs`, `DocumentIntelligenceEndpointTests.cs`,
`DocumentWorkerRelationalTests.cs`, `DocumentIntelligenceProviderTests.cs`.
New Flutter test: `test/document_evidence_test.dart`.

New Python service: `document-ai/app/{__main__,main,schemas,extraction,extract_worker,limits,pipeline,postprocessing}.py`;
`requirements.txt`, `pytest.ini`, `Dockerfile`, `compose.yaml`, `.env.example`, `.dockerignore`;
`tests/{pdf_helpers,test_pipeline,test_service_integration}.py`; and
`evaluation/{dataset.json,run.py,smoke.py,ocr.py,README.md,results*.json}`.
This document is the implementation/run/deployment report. Unrelated pre-existing working-tree
changes, scripts, artifacts and mobile demo configuration are not part of this feature's change list.

## Verification results (2 October 2026)

Backend build succeeds; backend suite: 464 passed. Flutter analyze reports no issues. Flutter suite: 205 passed.
Python service/pipeline suite: 21 passed including multipart/native/scanned/rotated inputs and grounding.
The real HTTP-to-local-model smoke passed with five passport statements/evidence entries in 69.35 seconds.
The parser dependency audit reports no known vulnerabilities for the pinned requirements.
PostgreSQL migration application succeeded locally. Relational worker tests verify lease recovery,
transactional result/cache persistence, and one-time processing. Provider tests verify malformed/null
output rejection and explicit review-marked fallback. Existing tests were retained.

| Synthetic evaluation | Local Qwen3:4b | Gemini 3.5 Flash Lite |
| --- | ---: | ---: |
| Valid grounded outputs | 8/8 | 8/8 |
| Classification accuracy | 100% | 100% |
| Expected original-field recall | 100% | 100% |
| Evidence quote match in valid outputs | 100% | 100% |
| Forbidden regression phrases detected | 0 | 0 |

The initial local run rejected one tenancy output (7/8); the preserved initial report records that
failure. A clearer evidence-repair instruction resolved it, and the final full run passed 8/8.
The local run averaged 63.61 seconds per short document on this 16-GiB development machine.
Gemini averaged 4.55 seconds per short document; this corpus does not establish model superiority or a
semantic hallucination rate. Local results record the exact digest
`359d7dd4bcdab3d86b87d73ac27966f4dbb9f5efdfcc75d34a8764a09474fae7`.
The deterministic OCR benchmark measured zero character/word errors on three generated English
scans (clean, reduced resolution/blur, and 90-degree rotation); see the report for extraction timings.
Those fixtures do not demonstrate real-world multilingual, handwriting or complicated layout accuracy.
Production acceptance remains incomplete for the unverified deployment, account/device and
model-quality requirements listed above.


Continuation quality work and measured limitations are recorded in `docs/document-intelligence-quality.md`.
