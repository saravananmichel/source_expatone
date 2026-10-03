# Document intelligence quality continuation

This continues the existing engine. It does not replace Firebase authentication, private S3,
PostgreSQL jobs, version history, Gemini Q&A, or the Flutter document flow. Production quality
acceptance is incomplete. Measurements below use deliberately fictitious documents only.

1. **Implemented changes.** Two-stage extraction and document-level reasoning, claim support
   statuses, conservative narrative filtering, source-script identity preservation, date/money
   typing, conditional notice relationships, cross-page evidence graph, clearer contradictions,
   scoped missing-field hints, explanation-language configuration, stage diagnostics, grouped
   Flutter cards, debug quality panel, provider choice retained with queued configuration, and
   bounded longer processing. Both API and Python images run as non-root users.
2. **Files.** `document-ai/app/{pipeline,schemas,reasoning,support,postprocessing,extraction,main,limits}.py`,
   `document-ai/evaluation/`, application analysis DTOs, infrastructure analysis configuration/
   provider/job services, API worker/configuration, Flutter analysis screen and integration test,
   provider/support/configuration/widget tests, backend Dockerfile, safe local verification scripts,
   and `deploy/document-intelligence/README.md`.
3. **Models.** Qwen3:4b is the development default. Qwen3:8b and Phi-4-mini are comparison candidates;
   Gemini 3.5 Flash Lite is a synthetic external extraction reference. Exact local model digests,
   source fingerprints, resident memory, inference token counts and timings are saved in reports.
   Qwen3 uses [Apache 2.0](https://huggingface.co/Qwen/Qwen3-8B/blob/main/LICENSE); Phi-4-mini uses
   [MIT](https://huggingface.co/microsoft/Phi-4-mini-instruct/blob/main/LICENSE).
4. **Winner.** No overall quality winner is certified. Target matching and model-judge verdicts do
   not establish independent semantic accuracy. Model selection requires blinded human review.
5. **Corpus.** 22 synthetic cases across contracts/offers/passports/passes/insurance/rental/bank/
   government/tax/medical documents, contradictions, missing schedules, original English/Malay/
   Tamil/Chinese, actual tables/columns, generated scans and a 24-page agreement. Checked-in
   `dataset/`, `gold/`, `predictions/`, `reports/` directories are used. Gold is explicitly partial;
   empty annotation lists mean unannotated, not verified absent. Real photographed documents,
   handwriting, signatures/stamps and mixed scan layouts remain inadequately covered.
6. **Extraction metrics.** Exact target field/date/money recall and category accuracy are saved per
   case, including failures in denominators. Complete extraction precision/recall: **Not measured.**
7. **Grounding.** Quotations and originals must match analyzed pages; source geometry belongs to
   the server. Every significant statement receives SUPPORTED/PARTIALLY_SUPPORTED/UNSUPPORTED/
   UNCERTAIN. An independent model request checks actor, amount, negation, conditions, modality
   and action justification. Missing/malformed verdicts fail closed. Model judgments are fallible.
8. **Unsupported claims.** Unsupported/partial/uncertain interpretations are omitted from summaries,
   explanations and actions, with source values and a review state retained in the UI. A ten-claim
   author-defined challenge tests tax assumptions, permission negation, probation notice and
   creditworthiness. Independent corpus hallucination/unsupported-claim rates: **Not measured.**
9. **Findings.** Expiry attention and singular-field contradictions cite original evidence. The
   contradiction rule normalizes parenthetical field labels and shows both values. Finding recall
   is a keyword proxy where gold targets exist. Independent finding correctness: **Not measured.**
10. **Actions.** Only supported actions enter the narrative/next-step lists. No unstated legal,
    medical, tax or immigration duties are added. Independent action accuracy: **Not measured.**
11. **Summaries.** A bounded overview stage and supported clause/relationship explanations drive
    narrative. When unavailable, safe extracted details replace unverified prose. Summary target
    coverage is reported; usefulness/human rubric scores: **Not measured.** Actual output review
    found thin baseline summaries and an invented Chinese romanization; identity repairs and
    stricter overview generation were added in response. Archived diagnostic runs preserve failures.
12. **Multilingual.** Original evidence is retained. OCR language models for eng/msa/tam/chi_sim are
    installed in Docker; configure OCR_LANGUAGES explicitly. EXPLANATION_LANGUAGE defaults to
    English. Printed synthetic English/Malay OCR had zero CER in this run. Tamil clean/low-DPI
    CER was 0.111 with field recall 0.333; Chinese CER was 0.224/0.121 with field recall 0.667.
    CER excludes whitespace; Chinese WER is undefined without a segmentation protocol. These
    failures demonstrate that OCR confidence is not correctness. Human multilingual quality and
    non-English explanation quality: **Not measured.**
13. **Layouts.** Native PDF coordinates, OCR lines and source page numbers remain available. Mixed
    native/scanned pages with short typed headers trigger OCR. Actual table and column fixtures
    are evaluated. Full table topology, checkbox semantics, stamps/signatures and photograph
    distortion support: incomplete. No new heavyweight layout dependency is claimed evaluated.
14. **Performance.** Extraction was measured at 1/5/10/20/40 native pages; see extraction-performance.json.
    Full authenticated one-page scan workflow took 285.42 seconds, including model contention;
    upload 1790 ms, extraction 6183 ms, storage 203 ms, model extraction 142023 ms, global reasoning
    90040 ms (timed out), support validation 39942 ms. That run correctly retained a fallback
    summary, not a verified rich overview. These are Mac development measurements, not real-time
    or AWS/GPU guarantees. 5/10/20/40-page full workflow latency and separate database duration:
    **Not measured.** Model reports capture actual inference stage metrics and resident model bytes.
15. **End-to-end.** The live script authenticated Firebase, uploaded a real synthetic PDF to S3,
    queued PostgreSQL, deduplicated requests, ran OCR/local inference/support review, persisted
    source version/history and reloaded the exact analysis. See local-api-e2e.json. Android device
    upload/poll/evidence UI verification has a separate integration test and outcome below; do
    not equate widget mocks or the API script with a successful device run.
16. **Verification.** Backend build/tests, Flutter analyze/widget tests, Python tests/audit, both
    image builds and runtime readiness/auth checks are recorded in the measured-results section.
17. **Security.** Source/prompt/output/keys are not logged by the new engine; network exceptions
    are stripped from Gemini logs, which could otherwise disclose sensitive request details.
    Python parses in a resource-limited subprocess and deletes/spools bounded temporary input.
    Authentication precedes multipart parsing; owner checks govern status/history/source access.
    Private model service and verification API bind loopback. API image now uses APP_UID.
    S3 security inspection returned AccessDenied on policy/encryption/public-access/versioning APIs;
    actual upload/download worked. IAM/Secrets Manager/production HTTPS certification remains
    **Not measured.** Never interpret inaccessible policy as a secure bucket configuration.
18. **Remaining blockers.** Independent human entailment/usefulness review, complete gold,
    confidence calibration, realistic multilingual scans/layouts, long-document global-context
    coverage, production IAM/TLS/retention verification and representative hardware measurements.
    Global reasoning above 24,000 serialized evidence bytes is skipped with an explicit diagnostic;
    full evidence graph and deterministic conditional relationships remain. This is not complete
    arbitrary-length cross-page reasoning. Local evidence navigation uses a signed version URL
    with a PDF page fragment; the external viewer may ignore it. Highlighting is not implemented.
19. **Local commands.** See commands below. Synthetic verification accounts/documents are kept
    only for this development test workflow; scripts do not read personal documents.
20. **Deployment.** CPU API image can run on ECS Fargate; GPU model hosts require separately
    provisioned GPU capacity ([AWS ECS GPU guidance](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/ecs-gpu.html)).
    Private networking/TLS, immutable images, pre-staged weights, least-privilege IAM and Secrets
    Manager configuration are required. Nothing was pushed or deployed to production.
21. **Configuration.** Enable with DocumentIntelligence__Enabled=true; choose Local/Gemini/Hybrid,
    explicit EnableFallback, ServiceUrl, ServiceKey and a bumped ConfigurationVersion (default
    local-v2). New queued fingerprints retain provider/fallback choice. Pre-v2 queued fingerprints
    use current worker settings for backwards compatibility. OCR/explanation/model settings must
    be included in an operator-controlled configuration bump to invalidate cached analysis.
22. **Resource/failure behavior.** 10 MiB, 40 pages, 100k text characters, 20M image pixels, one
    concurrent service job. Request ceiling defaults 900 seconds (configurable 30-1800), API HTTP
    16 minutes, worker 17 minutes, lease 18 minutes. Retries/expired leases remain durable. Corrupt,
    encrypted, oversize PDFs and malformed support references are tested. Failure inventory and
    live fallback results distinguish mocked tests from actual runs; complete chaos testing is
    incomplete. The Flutter polling window still permits reopening a longer-running analysis.
23. **Limits on conclusions.** Same-family model review, author-defined partial gold and keyword
    proxies cannot establish scientific quality. All unmeasured results are explicitly null or
    "Not measured." No production-ready, calibrated-confidence or model-winner claim is made.

## Commands

```sh
# From repository root; no private documents are accepted by these evaluation commands.
OLLAMA_MODEL=qwen3:4b OCR_LANGUAGES=eng+msa+tam+chi_sim document-ai/.venv/bin/python document-ai/evaluation/quality.py
OLLAMA_MODEL=qwen3:4b document-ai/.venv/bin/python document-ai/evaluation/support_challenge.py
document-ai/.venv/bin/python document-ai/evaluation/benchmark_models.py --models qwen3:4b,qwen3:8b,phi4-mini:latest
OLLAMA_MODEL=qwen3:4b OLLAMA_NUM_GPU=0 document-ai/.venv/bin/python document-ai/evaluation/quality.py --cases passport
document-ai/.venv/bin/python document-ai/evaluation/multilingual_ocr.py
document-ai/.venv/bin/python document-ai/evaluation/layout_performance.py
python3 scripts/document_quality_local_services.py
python3 scripts/document_quality_api_e2e.py
# In src/mobile:
flutter test integration_test/document_quality_local_test.dart -d emulator-5554 --dart-define=API_BASE_URL=http://10.0.2.2:5088/api
flutter analyze
flutter test
# In repository root:
dotnet test src/backend/ExpatOne.Tests/ExpatOne.Tests.csproj
cd document-ai && .venv/bin/python -m pytest -q && .venv/bin/python -m pip_audit -r requirements.txt
```

## Measured results

This section is finalized from saved reports after running verification; pending entries must
not be interpreted as passed. Reports preserve failed/uncertain cases and diagnostic revisions.
