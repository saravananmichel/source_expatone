# Private Ollama DEV Cloud Run image

This directory is an isolated build context. The Dockerfile bakes in `qwen3:0.6b` at image build time, so model downloads do not happen on each Cloud Run cold start.

## Cloud Build manual trigger (prepare after review)
- Source: `source_expatone`, branch `feature/document-ai-v4-adaptive-private-dev` (pin reviewed commit when running)
- Build type: Dockerfile
- Dockerfile: `document-ai/ollama-dev/Dockerfile`
- Build context: `document-ai/ollama-dev`
- Image destination: `asia-southeast1-docker.pkg.dev/expatone/expatone/ollama-dev:<reviewed-sha>`
- No automatic deployment to production

## Cloud Run DEV deployment (separate approval)
- Service: `expatone-ollama-dev`; region `asia-southeast1`
- Service account: `expatone-ollama-dev@expatone.iam.gserviceaccount.com`
- Port: 8080; proposed initial CPU 4 / memory 8 GiB, concurrency 1
- Require authentication; **do not grant allUsers Cloud Run Invoker**
- Internal ingress only; verify a supported private ingress route from Document AI
- Grant `roles/run.invoker` on this service **only** to `expatone-docai-v4-dev@expatone.iam.gserviceaccount.com`
- Document AI config: `OLLAMA_BASE_URL=https://<ollama-service-host>.run.app` and `OLLAMA_CLOUD_RUN_AUDIENCE=https://<same-service-host>.run.app`
- Set `DOCUMENT_AI_PROFILE=development`, `DOCUMENT_AI_DEV_MODEL=qwen3:0.6b`
- For internal ingress from Cloud Run, configure Document AI Direct VPC egress using the Singapore subnet and route **all traffic** through VPC; verify routing and ingress behavior before sending private data. The VPC is not itself an authentication mechanism.
- Keep existing production services, triggers and `github-sync` unchanged.

**Caution:** Cloud Run IAM protects requests at the service boundary; Ollama itself has no native IAM verification. Do not expose its port through any unauthenticated path. CPU performance and model quality must be measured during DEV UAT.
