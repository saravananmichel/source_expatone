# Deployment constraints

The ASP.NET API can run on ECS Fargate CPU. Deploy the Python/model worker separately on
CPU ECS with measured latency, or ECS EC2 GPU capacity; Fargate does not provide GPU instances.
The local Mac benchmark uses Metal on Apple unified memory and is not an AWS GPU benchmark.

Use immutable ECR digests for both built images, private subnets, no public IP for Python/model
services, and security groups allowing only the API worker to reach Python over TLS or a private
service mesh. Never expose Ollama port 11434 outside its task/host. Use a private service address
for DocumentIntelligence__ServiceUrl. Readiness must fail until the configured model is installed.
Pre-stage reviewed model weights in a private encrypted volume/artifact; do not download models
on the first document request. Set OLLAMA_NO_CLOUD=1. License review remains required for each
selected model and OCR/layout dependency.

Use Secrets Manager references for the service key, PostgreSQL credentials, Firebase service
credential, and Gemini API key. Grant task execution role access to only those secret ARNs and
ECR/log stream resources. The application task role needs only its private S3 bucket/user-object
operations and required KMS key. S3 bucket policies must deny public access and insecure
transport. CloudWatch logs must not contain documents, prompts, model output, signed URLs or
credentials. Disable database sensitive parameter logging.

Enforce one analysis per Python process, upload 10 MiB, 40 pages, 100,000 extracted characters,
20 million image pixels, OCR subprocess CPU/memory limits, bounded model output, and request
900-second default ceiling (DOCUMENT_AI_REQUEST_SECONDS, 30-1800). API HTTP timeout 16 minutes;
worker timeout 17 minutes with 18-minute lease. These are resource bounds, not latency guarantees.
Use encrypted database/S3/EFS; restrict PostgreSQL, set backup/retention policies and delete
source/result history according to the product's explicit retention policy. Roll out with
DocumentIntelligence__Enabled=true and a new ConfigurationVersion, default local-v2.

Do not deploy until private bucket policy/encryption, least-privilege IAM, secret injection,
user isolation, HTTPS, real CPU/GPU latency and failure recovery are independently verified.
The current development AWS identity could upload/read through the application but received
AccessDenied on bucket-security inspection APIs. Production cloud security is not certified.
