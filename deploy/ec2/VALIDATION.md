# Validation — 3 October 2026

## Results

* Python: **66 passed**, including bounded startup retry, model tag/config checks,
  safe 503 errors before/after extraction, privacy-safe failure responses and
  existing document grounding/OCR tests. One upstream Starlette/AnyIO deprecation warning.
* .NET: **475 passed**, including existing authentication, uploads, durable jobs,
  forwarded headers and the new ALB HTTP-health/HTTPS-routing and transient-only
  retry cases. Two existing nullable test warnings remain.
* Flutter: `flutter analyze` found no issues; **206 tests passed**, including the
  mobile API layer, document analysis/evidence and production URL guard.
* Both production Dockerfiles built successfully. Both Compose configurations
  and the GPU override parsed successfully; deployment script passed `bash -n`.
  Scoped .NET whitespace validation and `git diff --check` passed.
* Full EC2 Compose topology started on Docker Desktop with a temporary isolated
  pgvector/PostgreSQL database and synthetic Firebase credentials. Migrations,
  Ollama/model initialization, private FastAPI readiness and API database health
  passed. Only API port 8080 was published; 8090 and 11434 were not published.
* HTTP application routes returned 307 to HTTPS; authenticated document routes
  without a token returned 401 after trusted HTTPS forwarding. An untrusted
  production browser preflight had no `Access-Control-Allow-Origin` header.
* A real native Ollama qwen3:4b HTTP `/analyze` smoke passed in **143.47 seconds**,
  returning Passport classification, 5 evidence items and 6 statements. These
  were generated from synthetic input; prompts/documents/output were not printed.
* The full document smoke on the Docker Desktop **CPU** runtime hit the existing
  300-second batch timeout and returned a safe **503**. Its initial model load
  took about 109 seconds and generation was about 2 tokens/second while other
  validation work was running. This is a real performance limitation, not a
  successful end-to-end Docker document result. Readiness alone does not prove
  adequate inference throughput. Benchmark the target EC2 hardware or enable
  supported NVIDIA capacity before accepting traffic. The runbook gives exact
  internal document and device end-to-end tests.
* Stopping Ollama produced safe 503 readiness/analysis responses; restarting
  restored readiness. Re-creating the API and FastAPI together restored a healthy
  stack and database health.
* A short synthetic generation through the internal Docker Ollama API passed.
* Container log inspection found neither synthetic document values nor upstream
  error bodies. No real user documents were used in deployment validation.

AWS resources were **not provisioned or modified**. ALB/ACM/DNS/security groups,
instance boot recovery, EC2 IAM/IMDS, private S3/KMS policies, RDS TLS and NVIDIA
runtime remain infrastructure checks. A live production mobile→Firebase→S3→API
round trip was not run against an AWS deployment; use the existing device test
command in README.md after configuring those resources. Local mobile tests use
its existing test fixtures and do not establish AWS connectivity.

## Files changed

| File | Change |
| --- | --- |
| `.env.example` | Replace unused aliases with native application settings; document dotenv/Dart loading. |
| `.gitignore` | Ignore private EC2 credential material. |
| `document-ai/.env.example` | Canonical Ollama URL, startup and timeout options; preserve legacy alias. |
| `document-ai/app/config.py` | Shared URL validation, separate transport timeouts, exact-model readiness and production config validation. |
| `document-ai/app/main.py` | Bounded startup checks, model preflight, safe model/transport 503s and private OpenAPI removal. |
| `document-ai/app/pipeline.py` | Use shared URL and separate connection/read timeouts. |
| `document-ai/app/reasoning.py` | Use shared URL and separate connection/read timeouts in both reasoning passes. |
| `document-ai/app/support.py` | Use shared URL and separate connection/read timeouts for claim review. |
| `document-ai/app/smoke.py` | Container-compatible real HTTP smoke using synthetic PDF and content-free output. |
| `document-ai/compose.yaml` | Restart policies, Ollama health, automatic qwen3:4b initialization and gated FastAPI start. |
| `document-ai/tests/test_deployment.py` | Config/tag, startup, timeout, outage and privacy regression checks. |
| `document-ai/tests/test_pipeline.py` | Isolate readiness in the existing invalid-document test. |
| `document-ai/tests/test_service_integration.py` | Isolate readiness alongside its existing mocked model. |
| `src/backend/.dockerignore` | Additional nested secret and local-config exclusions, preserving existing exclusions. |
| `src/backend/ExpatOne.Api/Program.cs` | Exempt only the content-free ALB health probe from HTTPS redirection. |
| `src/backend/ExpatOne.Api/Middleware/ExceptionHandlingMiddleware.cs` | Log exception category without arbitrary exception bodies. |
| `src/backend/ExpatOne.Api/Services/DocumentIntelligenceWorker.cs` | Retry only connection/timeouts, 408, 429 and 5xx; fail permanent errors. |
| `src/backend/ExpatOne.Infrastructure/Services/DocumentAnalysisService.cs` | Log exception categories instead of source/parser exception bodies. |
| `src/backend/ExpatOne.Infrastructure/Services/GeminiAIService.cs` | Remove upstream error body logging; preserve other features. |
| `src/backend/ExpatOne.Tests/ForwardedHeadersTests.cs` | Check private HTTP health and HTTPS enforcement on application routes. |
| `src/backend/ExpatOne.Tests/DocumentWorkerRelationalTests.cs` | Test permanent versus transient HTTP failure behavior in real job persistence. |
| `deploy/ec2/compose.yaml` | Complete single-EC2 CPU stack, fixed local model, loopback document transport, limits and log rotation. |
| `deploy/ec2/compose.gpu.yaml` | Optional NVIDIA device reservation. |
| `deploy/ec2/.env.example` | Actual required production settings without embedded secrets. |
| `deploy/ec2/deploy.sh` | Ubuntu Docker installation, clean Git update, model/init/service ordering and health checks. |
| `deploy/ec2/README.md` | EC2 networking, HTTPS, secrets, migration/deploy/update/restart, model/API/mobile verification and recovery commands. |
| `deploy/ec2/VALIDATION.md` | Actual test outcomes, file manifest and remaining infrastructure checks. |
| `deploy/document-intelligence/README.md` | Link original deployment constraints to the concrete EC2 runbook. |

The existing Flutter production base-URL mechanism was verified and documented;
no mobile source change was needed.
