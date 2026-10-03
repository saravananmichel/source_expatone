# EC2 deployment: existing ExpatOne API and local Qwen3:4B

## Inspection and scope

The existing application is Flutter → Firebase-authenticated ASP.NET Core 8 API →
private FastAPI document intelligence → Ollama. Documents live in private S3;
PostgreSQL with pgvector holds users, durable analysis jobs, evidence and results.
Do not point the mobile app directly at FastAPI. Its service key is server-only.

Qwen calls originate in `document-ai/app/pipeline.py` (extraction and model digest),
`reasoning.py` (global/hierarchical conclusions), and `support.py` (claim review).
`/analyze` is the sole FastAPI inference endpoint; `/ready` checks installed tags.
Public routes that enqueue local analysis are document upload completion,
`POST /api/documents/{id}/analyze`, and `POST /api/documents/{id}/reanalyze`.
The API worker downloads the owned document and calls `/analyze`; the mobile app
polls `/api/documents/{id}/analysis/status` and retrieves persisted results.

Existing Gemini-backed routes remain unchanged: document `/ask`, translation
`/translate`, assistant conversation `/messages`, emergency `/assist`, knowledge
source `/ingest` and `/embeddings/generate` (embedding requests, rather than chat).
The deployment sets document provider **Local**, fallback **false**, extraction
and support models **qwen3:4b**, and `OLLAMA_NO_CLOUD=1`. An optional existing
Gemini key preserves the other features; it is never used as document fallback.
Already-queued jobs retain their original provider snapshot: drain/review any old
Gemini/Hybrid/fallback jobs before enabling this deployment.

No authentication, upload flow, mobile API contract, grounding or model prompts
were replaced. Production CORS stays disabled because the client is native
Flutter; permissive development CORS remains development-only. A browser frontend
would require a separate explicit origin allowlist. No fictitious `DATABASE_URL`,
`CORS_ORIGINS`, `HOST` or `PORT` settings were added to the ASP.NET application.

## EC2 and networking

Use Ubuntu 24.04 LTS, an encrypted EBS root/model volume with at least 50 GiB free,
and a starting CPU budget of 4 vCPU/16 GiB RAM. This is a starting capacity budget,
not a tested performance guarantee. This Compose file caps Ollama at 10 GiB,
FastAPI at 2 GiB and the API at 2 GiB. Benchmark concurrent users and long documents
on the actual instance before serving traffic. CPU inference can take minutes;
the existing asynchronous job API avoids holding mobile HTTP requests open.

Optional NVIDIA EC2 capacity requires a compatible host driver and the NVIDIA
Container Toolkit. The default is CPU; do not enable the GPU override on a CPU
host. Follow the [official NVIDIA instructions](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html), then:

```bash
nvidia-smi
sudo nvidia-ctk runtime configure --runtime=docker
sudo systemctl restart docker
USE_GPU=1 ./deploy.sh
```

Create an internet-facing ALB in two public subnets. Put EC2 and an encrypted
PostgreSQL/pgvector database in private subnets. Provide NAT or controlled outbound
HTTPS for Docker/model downloads, Firebase token verification and existing AWS
services. Keep model files in the persistent Compose `models` volume.

Security groups:

* ALB inbound TCP 443 from clients; optional 80 listener redirects to 443.
* ALB outbound TCP 8080 to the EC2 security group.
* EC2 inbound TCP 8080 **only from the ALB security group**. No public 8080 rule.
* EC2 has no 8090 or 11434 inbound rule; those ports are not published by Compose.
* RDS inbound 5432 only from the EC2 security group. Use verified TLS in its connection string.
* Prefer SSM for administrative access; restrict SSH to your administrative source if used.

Obtain an ACM certificate in the ALB region for `api.your-domain.example`, attach
to the HTTPS listener, and create a DNS alias to the ALB. Target group protocol
HTTP, instance port 8080, health path `/api/health`, success code 200. This endpoint
returns only status, tests database connectivity, and accepts private HTTP probes.
Application routes still enforce HTTPS and use the ALB's forwarded headers.
Configure the trusted VPC CIDRs in `.env`; never trust arbitrary internet proxies.
See [AWS ALB health checks](https://docs.aws.amazon.com/elasticloadbalancing/latest/application/target-group-health-checks.html)
and [security groups](https://docs.aws.amazon.com/elasticloadbalancing/latest/application/load-balancer-update-security-groups.html).

FastAPI shares the API container's network namespace and binds **127.0.0.1:8090**.
The API therefore retains its existing HTTPS-or-loopback rule; HTTP document data
never crosses the host network. Ollama uses the Compose bridge at
`http://ollama:11434`, with no host port. Re-create the API and FastAPI together
when changing the API container. These services are intentionally one EC2 unit;
separating them across hosts requires private TLS as in the original deployment notes.

Attach an EC2 instance role with only the existing S3 bucket/object permissions
and applicable KMS access. Use IMDSv2; bridged Docker credential access may require
metadata response hop limit 2. Restrict metadata access to containers needing AWS
credentials. Do not put AWS access keys in `.env`. Block S3 public access, require
TLS, encrypt documents/results, and configure retention/backups. Verify bucket
CORS separately for any browser client uploading to S3.

## Install, configure and migrate

Replace all placeholder values. Run from your checked-out project on EC2:

```bash
sudo apt-get update
sudo apt-get install -y git python3
# Clone your existing repository through its normal authorized Git credentials.
git clone YOUR_REPOSITORY_URL expatone
cd expatone/deploy/ec2
cp .env.example .env
chmod 600 .env
# Edit .env securely: database, bucket/region, Firebase project and keys.
```

The exact required environment keys are in `.env.example`. They use ASP.NET's
native `Section__Property` names, rather than the old root example's unused aliases.
The script does not source `.env` as shell code. Use Compose single quotes for
values containing `$`, `#`, spaces or JSON. Protect the file and do not run
`docker compose config` without `--quiet` because it renders secrets.

To generate the shared service key and import your Firebase service-account file
without printing either value (replace `/secure/firebase.json`):

```bash
python3 - <<'PY'
import json, secrets
from pathlib import Path
p = Path('.env')
s = p.read_text()
s = s.replace('DOCUMENT_AI_SERVICE_KEY=REPLACE_WITH_RANDOM_32_BYTE_KEY',
              'DOCUMENT_AI_SERVICE_KEY=' + secrets.token_hex(32))
credential = json.dumps(json.loads(Path('/secure/firebase.json').read_text()), separators=(',', ':'))
# Compose single-quoted values permit escaped single quotes.
credential = credential.replace("'", "\\'")
s = s.replace('Firebase__CredentialJson=', "Firebase__CredentialJson='" + credential + "'")
p.write_text(s)
p.chmod(0o600)
PY
```

The remaining keys are `ConnectionStrings__DefaultConnection`, `Aws__Region`,
`Aws__S3BucketName`, `Firebase__ProjectId`, optional `Gemini__ApiKey` for existing
non-document features, `OCR_LANGUAGES`, and `ForwardedHeaders__TrustedCidrs__0/1`.
The Compose configuration fixes production document provider/model/URL/timeouts.
Only the private FastAPI gets the service key and model settings, not the mobile app.

Install Docker using the [official Ubuntu package repository](https://docs.docker.com/engine/install/ubuntu/):

```bash
./deploy.sh --install
```

The `--install` command only installs/enables dependencies and exits; it enables
Docker on boot. If the Docker socket denies your current
user, use `sudo docker ...` for the following commands, or configure authorized
Docker group membership and log in again. Docker group membership grants root
control of the host. The script intentionally does not change that membership.
Finish with the commands below using sudo when required. Database migrations are explicit, not automatic:

```bash
sudo docker compose build backend document-ai
sudo docker compose run --rm --no-deps backend --migrate-database
# pgvector must be available on your PostgreSQL instance; use a migration role.
sudo ./deploy.sh
```

`deploy.sh` checks `.env`, pulls the pinned Ollama image, builds both application
images, waits for Ollama, verifies/downloads qwen3:4b with a 30-minute bound, then
starts the services and checks readiness and database health. It does not create
AWS resources, change database credentials, seed knowledge, or print secrets.
Use `./deploy.sh --update` to require a clean Git tree and pull with `--ff-only`.
When invoking as root, update Git as your checkout owner first, then `sudo ./deploy.sh`.

## Docker operations and model verification

All commands below run in `deploy/ec2`; add `sudo` if your socket requires it.
For GPU include `-f compose.yaml -f compose.gpu.yaml` in **each** Compose command.

```bash
docker compose up -d --wait --wait-timeout 2100
docker compose ps
docker compose exec -T ollama ollama list
docker compose exec -T ollama ollama show qwen3:4b
# Explicitly re-download/update weights only when intended:
docker compose exec -T ollama ollama pull qwen3:4b
docker compose exec -T ollama ollama ps
docker compose exec -T document-ai python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8090/ready',timeout=5); print('ready')"
curl --fail --max-time 10 http://127.0.0.1:8080/api/health
curl --fail --max-time 10 https://api.your-domain.example/api/health
# Authentication remains required (expect 401):
curl -i https://api.your-domain.example/api/documents
# No arbitrary production browser CORS permission (no Allow-Origin header):
curl -i -X OPTIONS https://api.your-domain.example/api/documents \
  -H 'Origin: https://untrusted.example' -H 'Access-Control-Request-Method: POST'
docker compose restart ollama document-ai backend
docker compose logs --tail=100 ollama document-ai backend
# Preserve models during shutdown; do not use down -v.
docker compose down
```

An internal real FastAPI inference smoke test is provided:

```bash
docker compose exec -T document-ai python -m app.smoke
```

It creates only a synthetic PDF, calls `/analyze` through HTTP with the existing
server service key, checks local qwen3:4b and grounded output, and prints only
counts/duration. It does not print document text, credentials or model responses.
For a simple Ollama generation independently of the application (synthetic only):

```bash
docker compose exec -T document-ai python -c "import httpx; r=httpx.post('http://ollama:11434/api/chat',json={'model':'qwen3:4b','stream':False,'think':False,'messages':[{'role':'user','content':'Reply with OK'}]},timeout=180); r.raise_for_status(); assert r.json()['message']['content']; print('inference passed')"
```

Do not expect an interactive `ollama run` to exercise document processing; the
HTTP `/analyze` smoke verifies extraction, reasoning, support review and rendering.

## Mobile production verification

The actual HTTP client already reads `API_BASE_URL` through Dart defines and
rejects localhost/plain HTTP in release. Keep that mechanism:

```bash
cd ../../src/mobile
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5000/api
flutter build appbundle --release \
  --dart-define=API_BASE_URL=https://api.your-domain.example/api
# On an authorized configured test device; uses synthetic Firebase test account/S3 input:
flutter test integration_test/document_quality_local_test.dart -d YOUR_DEVICE_ID \
  --dart-define=API_BASE_URL=https://api.your-domain.example/api
```

The existing device integration test checks authenticated upload, S3 completion,
durable job polling, local provider/evidence, result reload/history, and source UI.
It requires your Firebase platform configuration, test account sign-up settings,
HTTPS DNS/certificate, S3 permissions and the deployed migrated database. It leaves
synthetic test artifacts; manage them under your normal retention policy.
Manual equivalent: sign in on the production app, upload a synthetic PDF, request
analysis, wait for job `COMPLETED`/`REQUIRES_REVIEW`, reopen the document and verify
source evidence. Inspect `modelVersion` for `qwen3:4b@...` and provider `Local`.

## Failure recovery and sensitive data

If initialization fails, run `docker compose logs model-init ollama`, check disk,
outbound HTTPS/DNS and memory, then `docker compose run --rm model-init` and rerun
`deploy.sh`. `/health` is liveness; `/ready` is private model/OCR readiness.
Compose startup readiness does not restart a container merely because it becomes
unhealthy. `unless-stopped` restarts exited processes, and FastAPI returns a safe
503 for model/transport failures. Monitor private readiness, process exits, queue
age, memory/disk and failed jobs, and alert/recover a hung process explicitly.
The worker retries transient HTTP/timeouts with its existing bounded backoff;
invalid document/schema failures remain non-retryable. Never retry inference in an
infinite loop. Do not expose `/ready` through the ALB or publish Ollama diagnostics.

For a controlled outage test in a non-production environment:

```bash
docker compose stop ollama
# Expect 503 privately:
docker compose exec -T document-ai python -c "import httpx; assert httpx.get('http://127.0.0.1:8090/ready').status_code == 503"
docker compose start ollama
# Check /ready again, then submit another synthetic document.
```

File size remains 10 MiB with bounded multipart ingestion, 40 pages/100,000
characters, OCR subprocess limits, one concurrent analysis, 900-second total
request ceiling, and separate 5-second connection timeouts. Existing worker
16/17/18-minute HTTP/work/lease bounds remain intact. FastAPI closes spooled
uploads; API temp downloads use DeleteOnClose plus finally cleanup. Containers
use bounded in-memory `/tmp`, read-only application filesystems and dropped
capabilities. No prompt/source/output is logged; upstream error bodies and generic
unhandled exception bodies were removed from logs. Keep logs access-controlled
and avoid debug/sensitive EF logging or dumping environment/configuration.

## Validation record

See `VALIDATION.md` for actual checks performed in this workspace. AWS ALB/TLS,
security groups, IAM/KMS, RDS connectivity, host boot and NVIDIA capacity need
verification on your deployed infrastructure. This guide prepares deployment;
it does not assert that those AWS resources were provisioned.

After replacing the IDs with your provisioned AWS resources, verify AWS state:

```bash
aws ssm start-session --target YOUR_INSTANCE_ID
aws elbv2 describe-target-health --target-group-arn YOUR_TARGET_GROUP_ARN
aws ec2 describe-security-groups --group-ids YOUR_EC2_GROUP_ID YOUR_ALB_GROUP_ID
aws ec2 describe-instances --instance-ids YOUR_INSTANCE_ID \
  --query 'Reservations[].Instances[].{State:State.Name,Role:IamInstanceProfile.Arn,Metadata:MetadataOptions}'
aws s3api get-public-access-block --bucket YOUR_PRIVATE_BUCKET
aws s3api get-bucket-encryption --bucket YOUR_PRIVATE_BUCKET
```

Run these with an authorized administrative identity; the application role need
not have infrastructure inspection permissions. A healthy target and successful
HTTPS/mobile checks still require the networking and IAM steps described above.
