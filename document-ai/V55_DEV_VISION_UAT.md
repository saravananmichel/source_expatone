# Document AI V5.5 — controlled DEV Vision readiness

## Safety boundary
- Production and DEV must retain `DOCUMENT_AI_V55_VISION_ENABLED=false` until the checks below pass.
- Vision is an optional **image-only** review diagnostic. PDFs are not sent to the Vision model.
- Do not upload actual identity documents during initial testing. Use synthetic test images with invented data.
- A matching result is **not** verification of a passport number or date. Manual confirmation remains mandatory.
- Do not record source documents, image bytes, tokens, raw OCR text or model responses in test logs.

## Operator inventory check (no image sent)
Run from the `document-ai` directory in an authenticated, approved DEV environment:

```powershell
$env:DOCUMENT_AI_V55_VISION_ENABLED = "true"
$env:DOCUMENT_AI_V55_VISION_MODEL = "<separately-deployed-private-vision-model>"
python -m app.vision_preflight_cli_v55
```

Configure `VISION_OLLAMA_BASE_URL` and `VISION_OLLAMA_CLOUD_RUN_AUDIENCE` to the **same dedicated private Vision Cloud Run origin**, using the established DEV deployment mechanism. Never reuse the existing text `OLLAMA_BASE_URL` or its audience. Grant the Document AI DEV service account `roles/run.invoker` on the new Vision service only. Do not paste credentials into tickets or commit environment secrets. The CLI only requests `GET /api/tags` and prints bounded status flags; it does **not** change deployment settings.

A `model_listed` response proves only that the exact model name appears in the inventory; it does not establish vision capability, successful inference, model memory sufficiency, or latency.

## Required DEV acceptance gates
1. Confirm private/authenticated network access to the dedicated Vision Ollama service, selected model tag, and explicit image capability. Keep the production text model unchanged.
2. Measure memory and CPU/GPU capacity, concurrency, timeout, image payload sizes and per-request costs; set a cost ceiling.
3. With synthetic PNG/JPEG files under 2 MB, test correct matching passport number/DOB/expiry, mismatched values, missing labels, unreadable image and model timeout.
4. Confirm no raw identity values appear in `qualityDiagnostics.visionReview` or logs.
5. Confirm a Vision error never interrupts normal OCR and never modifies `structuredFields`, `importantDates`, or passport category.
6. Test PDF path remains OCR-only with `unsupported_media` review status when Vision is enabled.
7. Confirm authorization, rate limits, latency, and rollback. Only then approve a short DEV-only activation window.
8. Disable the flag after the test window; production remains disabled pending explicit release approval.

## Rollback
Set `DOCUMENT_AI_V55_VISION_ENABLED=false` in the DEV service configuration and redeploy the previous approved configuration. Re-run existing OCR regression and document analysis tests.

## Release status
This runbook and preflight CLI do not provision infrastructure, deploy models, activate Vision, or validate real inference.
