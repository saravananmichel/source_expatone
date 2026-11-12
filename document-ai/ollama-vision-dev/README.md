# Private Vision Ollama DEV image

Isolated from `document-ai/ollama-dev` (the existing Qwen3 text service).

- Base: `ollama/ollama:0.12.11`
- Pilot Vision model: `moondream:1.8b` (verify model availability at build time; test synthetic documents before real passport data)
- Build context: `document-ai/ollama-vision-dev`
- Destination: `asia-southeast1-docker.pkg.dev/expatone/expatone/ollama-vision-dev:<reviewed-sha>`
- Cloud Run service: `expatone-ollama-vision-dev`, `asia-southeast1`
- Runtime service account: `expatone-ollama-vision-dev@expatone.iam.gserviceaccount.com`
- Ingress: internal only; require IAM authentication; never grant `allUsers` invoker
- Invoker: `expatone-docai-v4-dev@expatone.iam.gserviceaccount.com`
- Pilot capacity: 2 vCPU, 4 GiB memory, concurrency 1, max instances 1, min instances 0; adjust after measurements
- Document AI config after deployment: `VISION_OLLAMA_BASE_URL` and `VISION_OLLAMA_CLOUD_RUN_AUDIENCE` must both equal the Vision Cloud Run HTTPS origin; `DOCUMENT_AI_V55_VISION_MODEL=moondream:1.8b`
- Keep `DOCUMENT_AI_V55_VISION_ENABLED=false` until synthetic UAT passes.

**Important:** Model availability, 4 GiB fit, cold-start latency, and passport OCR accuracy have not been validated. The image build itself will download model weights from the model registry; this is a build-time dependency, not runtime inference. Review model provenance/licensing and network/egress controls before use with sensitive documents. No deployment is performed by this change.
