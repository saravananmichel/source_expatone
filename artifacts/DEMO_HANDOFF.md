# ExpatOne Android demo handoff — 1 October 2026

The AWS API is live over trusted HTTPS. The Android release APK is built, and an Android emulator integration test passed using the real Firebase SDK, the app’s login UI, and the deployed API. This is an Android demo package with the limitations below; it is not an App Store/Play Store production release.

## URLs and installable artifact

- Secure API and Flutter demo base URL: **https://d38i3e3pvvjux7.cloudfront.net/api**
- Health: https://d38i3e3pvvjux7.cloudfront.net/api/health — HTTP 200, `{"status":"ok"}`.
- Requests without a Firebase bearer token to `/api/users/me` return 401.
- HTTP requests to the CloudFront hostname return 403. No tokens were sent over public HTTP during testing.
- Original ALB hostname: `expatone-api-alb-68048405.ap-southeast-5.elb.amazonaws.com`. Public ingress has been removed. It remains the private CloudFront origin; use the HTTPS URL above for the demo.
- APK: `artifacts/expatone-demo-20261001.apk` (approximately 54 MiB).
- APK SHA-256: `16674e4aa7042d344aa92db5042389148eda87037e6f8f3dd7d488e430290c75`.
- Release runtime, signed with the project’s existing **Android debug certificate**. Suitable for sideloaded demonstrations, not a finalized Play Store signing setup. The certificate fingerprint matches the Android OAuth client in the local Firebase configuration. Interactive Google login was not tested.

## Architecture and current infrastructure

Android → CloudFront HTTPS → private CloudFront VPC origin → existing ALB on port 80 → existing Fargate backend on port 8080 → private PostgreSQL / private S3 / Firebase / Gemini.

The HTTP origin hops stay inside the VPC. CloudFront accepts HTTPS only, disables API caching, forwards authentication headers/cookies/query strings, replaces the viewer Host with the ALB origin hostname, and adds security headers including HSTS. S3 presigned transfers use S3 HTTPS directly and carry no Firebase bearer token.

- AWS account: `020429479243`; application region: `ap-southeast-5` (Malaysia).
- CloudFront distribution: `E2MI6ETTUWEQWY`, deployed.
- VPC origin: `vo_CgGQZgKuCmVFLnLErvlsYV`, deployed.
- ALB ingress: port 80 only from CloudFront’s service security group `sg-07d7befc75d6dd6a1`; no public IPv4/IPv6 ingress.
- ECS: `expatone-cluster` / `expatone-api-service`, task definition `expatone-api:4`; one ARM64 Linux task, 1 vCPU / 2 GiB. Service rollout completed and ALB target healthy.
- RDS: `expatone-postgres`, private PostgreSQL 17.11, db.t4g.micro, Single-AZ, 400 GiB io2 / 3,000 IOPS. Database `expatone` and migrations including pgvector are applied. Backups retained seven days.
- S3: `expatone-my-prd`, all public-access blocks enabled, AES256 encryption, versioning enabled.
- Gemini generation: `gemini-3.5-flash-lite`; embeddings: `gemini-embedding-2`. Both worked in live tests.
- Firebase: `expatone-873ea`. Real signup/login and server-side identity validation passed.
- Knowledge library: initially empty; the project’s existing seed task added 73 sources and 138 embeddings, then exited successfully. A live assistant answer returned `official_grounded` with five source references. This validates retrieval; it does not re-audit every curated source’s current legal accuracy.
- Removed unused Bedrock/Textract task-role policies and the obsolete ECS service policy from the execution role. The task role now has only its bucket-scoped S3 policy; the execution role retains its standard ECR/logs policy and scoped Secrets Manager policy. Prior inline grants are backed up in `unused-iam-policy-backup.json`.
- Secrets remain in Secrets Manager and are injected through ECS execution permissions. AWS S3 access uses the task role. No AWS access keys are injected into ECS or Flutter; no Gemini key or Firebase service-account credential is included in Flutter.

## Audit and changes

The ASP.NET application already centralizes Firebase authentication, scoped user identity, S3 presigning, Gemini requests, EF migrations, and rate limiting. Swagger and CORS development configuration are restricted to Development. Native Android calls do not require browser CORS. Production logging uses CloudWatch. Health checks now return 503 on database failure.

Flutter backend calls use the centralized `ApiClient` and `ApiConstants`. The development default remains `http://10.0.2.2:5000/api`. `AppConfig.production` retains the future `https://api.expatone.com/api` URL; the new demo configuration supplies the live CloudFront URL. Individual feature services contain no separate development API endpoint.

Changes for the demo:

- Added `src/mobile/config/demo.json` and `AppConfig.demo`.
- Replaced an assertion-only release URL guard with a runtime HTTPS check. Assertions are disabled in release builds.
- Added Android INTERNET permission to the main manifest. Release cleartext traffic is disabled; debug/profile builds retain local development access.
- Updated deprecated Flutter Radio/Switch/dropdown APIs and removed an unused test helper. Analysis now reports no issues.
- Added a live Android integration test in `src/mobile/integration_test/demo_https_test.dart`.
- Added a synthetic HTTPS API smoke script in `scripts/demo_api_smoke.py`.
- Preserved existing user edits to backend local settings and launch configuration. No changes have been committed.

Build again:

```sh
cd "/Users/austincelestia/Documents/ExpatOne /src/mobile"
flutter build apk --release --dart-define-from-file=config/demo.json
```

## Verification

| Check | Result |
|---|---|
| Chrome HTTPS health page | Verified `{"status":"ok"}` |
| Flutter analyze | No issues |
| Flutter unit/widget tests | 203 passed |
| Backend tests | 457 passed in the preceding deployment verification; backend source has not changed since |
| Backend Release build | Passed; one existing EF Core Relational version conflict warning in the test project |
| ARM64 Docker production build | Passed |
| Deployed source/image check | All 52 published application files match the fresh build byte for byte; ECS digest matches ECR |
| Android release build | Passed; APK signature and manifest inspected |
| Release artifact URL/security scan | CloudFront hostname present, emulator URL absent, no AWS private-key/access-key patterns found |
| Firebase SDK signup/token/UI login/logout | Passed on Pixel emulator |
| Authenticated API / matching identity / unauthenticated rejection | Passed |
| PDF wallet | Presigned upload, completion, metadata, list/detail, HTTPS download byte comparison, deletion passed |
| AI document reader | Live analysis, summary, structured fields and persisted analysis passed |
| Reminders | Create/generate, list, dismiss/update, delete passed |
| Government assistant | Conversation, message, retrieval with five official references, history, deletion passed |
| Translation | Language list and real text translation passed |
| Profile | Load, update, options/checklist passed |
| Emergency | Live optional AI assistance passed; deterministic 999 UI/failure behavior covered by existing tests and code inspection |

The Android integration test uses a debug test harness with the same demo HTTPS configuration, not the release APK’s runtime. It exercises the actual application service classes and native Firebase SDK. The release APK was built and inspected separately. Full manual file-picker, microphone, location, sharing, telephony, and speaker interaction on a physical handset remain unverified. **No call to 999 was placed.**

The first API smoke run correctly rejected my unsupported reminder status `Completed` with 400. The corrected `Dismissed` test passed. Initially the assistant returned a marked `general_unverified` answer because the knowledge table was empty; after seeding, the grounded test passed. The emulator initially had an offline/debugger connection problem; a cold boot restored it and the full integration test passed.

CloudWatch review scanned 45 recent events: the only error/exception entries were the rejected invalid reminder test. No credential-pattern matches were found. This is a bounded log/pattern review, not proof that every historical log is free of sensitive data. Startup retains the existing DataProtection ephemeral/unencrypted-key and HTTPS redirect port warnings.

Published image: `020429479243.dkr.ecr.ap-southeast-5.amazonaws.com/expatone-api:prod-20260930-initdb`.
Digest: `sha256:1ed301f03fdfe4b01c3ec046b6fd2229dbf8b1b2a6eabb77b3b9e742ce997450`.
The image was built from the working tree, not a newly committed release. File hashes are recorded in `backend-source-manifest.json`; binary comparison is recorded in `image-verification.json`. A new image manifest digest can differ because build attestations change even when published application bytes match.

## HTTPS option comparison

| Option | Trusted service hostname | Extra infrastructure / suitability |
|---|---|---|
| Existing ALB alone | No trusted TLS certificate for its AWS hostname | Cannot meet the no-custom-domain HTTPS requirement alone |
| API Gateway HTTP API | Yes, execute-api hostname | Private integration adds a VPC link; 30-second maximum integration timeout conflicts with longer AI requests |
| API Gateway REST API | Yes | More configuration and request costs; relevant VPC link V2 regional support does not include Malaysia |
| CloudFront VPC origin (chosen) | Yes, cloudfront.net hostname | One distribution and origin using the existing ALB; supports current HTTP operations, authorization forwarding, disabled caching, 120-second origin read timeout, and direct S3 presigning |

References: [CloudFront VPC origins](https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/private-content-vpc-origins.html), [managed request policies](https://docs.aws.amazon.com/AmazonCloudFront/latest/DeveloperGuide/using-managed-origin-request-policies.html), [API Gateway HTTP quotas](https://docs.aws.amazon.com/apigateway/latest/developerguide/http-api-quotas.html), [VPC link V2 regions](https://docs.aws.amazon.com/apigateway/latest/developerguide/apigateway-vpc-links-v2.html).

CloudFront VPC origins do not support every future protocol feature; the current application uses regular HTTP requests, not WebSockets or gRPC.

## Monthly cost estimate

Assumes 730 hours, one Fargate task, two public ALB addresses plus the task’s public egress address, low traffic, no savings plan. Rates were queried from the AWS Price List API for Malaysia on 1 October 2026.

| Resource | Estimated USD/month |
|---|---:|
| RDS db.t4g.micro compute | 16.79 |
| Existing RDS 400 GiB io2 storage | 49.60 |
| Existing RDS 3,000 io2 IOPS | 297.00 |
| Fargate ARM64 1 vCPU / 2 GiB | 32.39 |
| ALB hourly charge | 16.57 |
| Three public IPv4 addresses | 10.95 |
| Base total | **423.30** |

Allow roughly **$425–450/month** at low traffic before taxes and credits, plus variable Gemini charges. ALB LCUs, S3/ECR, logs, Secrets Manager, backups, CPU credits and traffic can increase this. One continuously used ALB LCU adds about $5.84/month. The earlier $60–100 estimate covered incremental task/ALB/network costs and excluded this expensive preexisting RDS configuration.

CloudFront pay-as-you-go includes the first 1 TB of monthly internet delivery and 10 million HTTP(S) requests free. Small demo usage should add little cost; charges still depend on usage, including traffic sent to the origin. No paid flat-rate subscription was selected. [CloudFront pay-as-you-go pricing](https://aws.amazon.com/cloudfront/pricing/pay-as-you-go/), [RDS PostgreSQL pricing](https://aws.amazon.com/rds/postgresql/pricing/).

The database storage configuration has been left unchanged. Cost reduction would need a separate reviewed change, with backup and migration planning if reducing allocated capacity.

## Remaining limits and iOS

- iOS lacks `GoogleService-Info.plist`, a DEVELOPMENT_TEAM/signing setup, and full Xcode (only command-line tools are selected). No iOS archive was created. Add the real Firebase iOS configuration for `com.expatone.expatoneApp`, install/select Xcode, configure the authorized Apple team/certificates/provisioning, then build/archive using the HTTPS define.
- Android uses the existing debug signing certificate. Set up a protected release/upload key and its Firebase fingerprints before Play distribution.
- Interactive Google login and physical-device speech/location/telephony remain unverified.
- Reminder push notification delivery is deferred in the existing project; reminders are persisted/processed but FCM delivery is not implemented.
- One task and a Single-AZ database do not provide a highly available production deployment.
- The container currently runs as its default root user. DataProtection persistence/encryption and dependency version pinning remain production hardening work.
- Existing profile-loading failure behavior can skip onboarding on an API error; the verified normal HTTPS profile flow passed, but that failure path remains a hardening item.
- Synthetic smoke accounts/backend user rows remain from testing; uploaded PDFs, reminders and conversations were deleted by the tests. Versioned S3 may retain prior object versions under the existing bucket policy.

## Later migration to api.expatone.com

1. Obtain DNS control for the domain. No domain was purchased or DNS changed in this work.
2. Request an ACM public certificate for `api.expatone.com` in **us-east-1**, required for CloudFront. The existing pending Malaysia certificate is not usable for CloudFront.
3. Add the ACM DNS validation record and wait for issuance.
4. Attach the issued certificate and add `api.expatone.com` as an alternate domain on this existing CloudFront distribution. Keep HTTPS-only access, disabled caching and authorization forwarding.
5. Add DNS CNAME `api` → `d38i3e3pvvjux7.cloudfront.net` (or an appropriate Route 53 alias if using Route 53). Wait for distribution and DNS deployment.
6. Verify trusted TLS, health 200, unauthorized 401, real authenticated identity, S3 transfers and AI calls at `https://api.expatone.com/api`.
7. Build Flutter with `--dart-define=API_BASE_URL=https://api.expatone.com/api`, distribute the updated APK, and apply that define to the future iOS archive.
8. Retain the CloudFront service hostname during client migration. The existing ALB/ECS/RDS/S3 and private origin access can remain unchanged.
