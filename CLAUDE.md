# ExpatOne — Project Context for Claude Code

## What is this project?
ExpatOne is a mobile app for foreigners/expats living in Malaysia. It's an AI-powered life-admin assistant covering government processes, document management, reminders, translations, and emergency info.

## Current Phase
**Phase 10 — MVP Hardening** (IN PROGRESS — Security Audit complete, Batch A complete, Phase 10.2 Profile + Onboarding complete, Phase 10.3 Document Intelligence complete, Phase 10.4 Advanced Document Wallet IN PROGRESS)

Previous: Phase 9 — Emergency Assistant (COMPLETE)

## Tech Stack
- **Mobile**: Flutter 3.47.3 (Android + iOS)
- **Backend**: ASP.NET Core 8 Web API (.NET 8.0.131, modular monolith)
- **Database**: PostgreSQL 18 (localhost:5432, user: postgres, db: expatone)
- **ORM**: Entity Framework Core 8.0 + Npgsql
- **AI Provider**: Google Gemini (NOT OpenAI) — GeminiAIService implements IAIService (Phase 5)
- **File Storage**: Amazon S3 (implemented Phase 3 — S3StorageService)
- **Auth**: Firebase Authentication (implemented Phase 2)
- **Notifications**: Reminder processing implemented (delivery via FCM deferred)
- **Vector Search**: pgvector 0.8.6 + HNSW cosine index (implemented Phase 6)

## Architecture
Backend uses clean architecture with 4 projects + test project:
- `ExpatOne.Api` — Controllers, middleware, DI config, Program.cs
- `ExpatOne.Application` — Interfaces, DTOs, common types
- `ExpatOne.Domain` — Entities, enums, domain rules
- `ExpatOne.Infrastructure` — DbContext, entity configs, services, migrations
- `ExpatOne.Tests` — xUnit tests (unit + integration)

Flutter uses feature-based architecture:
- `lib/core/` — config, constants, networking, error handling, theme
- `lib/features/` — dashboard, authentication, documents, etc.
- `lib/shared/` — widgets, models, services shared across features

## Key Design Decisions
- **User.ExternalId** (not FirebaseUid) — provider-agnostic identity mapping. Internal DB user is decoupled from Firebase. All relationships (documents, reminders, conversations) reference the internal User, not Firebase directly.
- **Modular monolith** — single ASP.NET Core process, clean layer separation. Can split later if scale requires.
- **EF Core with separate configurations** — each entity has its own IEntityTypeConfiguration class.
- **Snake_case table names** — PostgreSQL convention (users, documents, ai_conversations, etc.)
- **Enum stored as string** — DocumentStatus and ReminderStatus stored as varchar for readability.

## Database
PostgreSQL 18 at `/Library/PostgreSQL/18/`. Migration applied: `InitialCreate`.

Tables: users, documents, document_types, reminders, countries, government_knowledge, government_sources, ai_conversations, ai_conversation_messages, emergency_resources.

Seed data: Malaysia (country code MY). Immigration Batch 1: 17 sources + 25 chunks. Tax Batch 2: 12 sources + 16 chunks. Driving Batch 3: 10 sources + 14 chunks. Employment Batch 4: 17 active sources (9 deactivated during remediation) + chunks. PERKESO Batch 5: 6 sources (KWSP entirely inaccessible — documented as gap). Healthcare Batch 6: 3 sources from IMI (MOH entirely inaccessible — documented as gap). Education Batch 7: 1 source from IMI Student Pass page (MOE pages empty, EMGS not eligible). Customs Batch 8: 3 sources from JKDM (customs.gov.my) + 14 chunks (traveller's guide, import/export prohibitions, strategic trade act). Government Services Batch 9: 4 sources from JPN (jpn.gov.my) + 14 chunks (marriage FAQ, identity card FAQ, birth registration FAQ, citizenship FAQ). PDRM: Malay-only, multiple 500 errors — documented as gap. MyGovernment portal: 403 Forbidden — documented as gap. Emergency services portals: all inaccessible — documented as gap. Total active sources: 74. Total active chunks: 139. Total embeddings: 139/139.

Migrations: `InitialCreate`, `AddCompositeExternalIdentityIndex`, `AddDocumentOriginalFileNameAndSeedDocumentTypes`, `AddReminderDaysBeforeExpiryAndCascadeDelete`, `AddKnowledgeVectorColumns`, `AddProfileAndOnboardingFields`, `AddDocumentVersioningSharingAudit`.

Key index: `IX_users_ExternalProvider_ExternalId` (unique composite) — ensures one user per provider+ID combination.

Document types seeded: Passport, Visa, Employment Pass, Driving Licence, Insurance, Medical Card, Work Permit, Government Letter, Other.

```bash
# Apply migrations
export PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH:/Users/austincelestia/.dotnet/tools"
export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"
cd src/backend
dotnet ef database update --project ExpatOne.Infrastructure --startup-project ExpatOne.Api
```

## Reminder Architecture
```
Document (with ExpiryDate) → POST /api/reminders/generate → creates Reminder records with offsets (30/14/7/1 days)
ReminderProcessorService (BackgroundService, 15min interval) → finds due Active reminders → marks as Sent
```

- Reminders auto-generated from document expiry dates with configurable day offsets
- Generation is idempotent — duplicate offsets are skipped, removed offsets are cancelled
- Document deletion cascades to reminders (FK with Cascade delete)
- Expiry date changes: user re-generates reminders, old ones are cancelled
- Reminder statuses: Active → Sent (processed) / Dismissed (user) / Cancelled (system)
- Notification delivery is deferred — ReminderProcessorService marks as Sent but does not push yet
- Composite unique index on (DocumentId, DaysBeforeExpiry) prevents duplicate reminders

## AI Document Analysis Architecture
```
Flutter → POST /api/documents/{id}/analyze → ASP.NET validates Firebase identity
→ verify document ownership → download from S3 → send to Gemini (base64 + structured output)
→ parse structured JSON → cache in Document.ExtractedMetadata → return DocumentAnalysisDto
Flutter → GET /api/documents/{id}/analysis → return cached analysis
```

- Gemini API called from backend only — API key never exposed to Flutter
- Uses Gemini structured output (response_mime_type=application/json + response_schema)
- Analysis cached in existing Document.ExtractedMetadata field — no new migration
- IsAnalyzed flag derived from ExtractedMetadata presence on DocumentDto
- Supports PDF, JPEG, PNG (same types allowed by document upload)
- Max 10 MB analysis size enforced at AI boundary
- Re-analysis available via forceReanalyze flag
- AI distinguishes extracted facts (isExtracted=true) from inferences (isExtracted=false)
- Prompt instructs Gemini to never fabricate information not in the document
- Conditional DI: if Gemini:ApiKey not configured, analysis endpoints return 503

## Government Knowledge Base Architecture
```
Admin → POST /api/knowledge/sources → registers GovernmentSource
Admin → POST /api/knowledge/sources/{id}/ingest → chunks content → creates GovernmentKnowledge records
Admin → POST /api/knowledge/embeddings/generate → Gemini gemini-embedding-2 → vector(768) embeddings
User → GET /api/knowledge/search?q=... → embed query → pgvector cosine search → ranked results
```

- GovernmentSource → GovernmentKnowledge (one-to-many, FK with SetNull)
- Content chunked at 2000 chars with 200-char overlap, paragraph-aware splitting
- SHA-256 content hashing for idempotent re-ingestion
- Embeddings generated via Gemini `gemini-embedding-2` (768 dimensions, `output_dimensionality=768`)
- Semantic search uses pgvector `<=>` cosine distance with HNSW index
- Query text prefixed with `task: search result | query:` for task-specific embeddings
- Document text prefixed with `title: {title} | text: {content}` for embedding
- Search requires Firebase auth; ingestion requires Firebase auth + `X-Admin-Key` header
- Admin key configured via `Knowledge:AdminKey` in appsettings
- Conditional DI: if Gemini:ApiKey not configured, knowledge endpoints return 503
- Knowledge search used by Phase 7 AI Government Assistant (RAG pipeline)
- Knowledge seed CLI: `dotnet run -- --seed-knowledge` ingests batch data via KnowledgeSeedService
- Batch 1 (Immigration): 17 sources, 25 chunks from ESD/IMI official pages, category = "immigration"
- Batch 2 (Tax): 12 sources, 16 chunks from HASiL/LHDN official pages, category = "tax"
- Batch 3 (Driving): 10 sources, 14 chunks from JPJ official pages, category = "driving"
- Batch 4 (Employment): 17 active sources from JTKSM/PERKESO/JPPM official pages, category = "employment" (9 synthetic sources deactivated during integrity remediation; see employment-sources-catalog.md)
- Batch 5 (PERKESO): 6 sources from perkeso.gov.my official pages, category = "employment". KWSP (EPF) entirely inaccessible (403) — documented as gap. See kwsp-perkeso-sources-catalog.md.
- Batch 6 (Healthcare): 3 sources from imi.gov.my official pages, category = "healthcare". MOH (moh.gov.my) entirely inaccessible (403) including foreign patient charges page — documented as gap. See moh-healthcare-sources-catalog.md.
- Batch 7 (Education): 1 source from imi.gov.my (IMI Student Pass page), category = "education". MOE pages return empty bodies; EMGS is a CLBG (private company, not eligible). See education-sources-catalog.md.
- Batch 8 (Customs): 3 sources from customs.gov.my (JKDM), 14 chunks, category = "customs". Covers: traveller declaration, duty-free allowances (air vs non-air), cash declaration (USD 10,000), prohibited/restricted goods, drugs, green/red lane, temporary import, ATA carnet, DFS, STA declaration, K7 form. MAQIS quarantine pages: Malay-only, commercial-focus — documented gap. NPRA/MOH medicine import for travellers: inaccessible — documented gap. Cigarette/tobacco specific limits: not published on JKDM site — documented gap. See customs-travel-sources-catalog.md.
- Batch 9 (Government Services): 4 sources from jpn.gov.my (JPN), 14 chunks, category = "government-services". Covers: marriage registration under Act 164, identity cards (MyKad/MyPR/MyKid), birth registration, citizenship FAQ. PDRM: Malay-only, multiple HTTP 500s — documented gap. MyGovernment portal: 403 Forbidden — documented gap. Emergency services portals (MERS 999, Bomba): all inaccessible — documented gap. See government-services-sources-catalog.md.
- Source catalogs: `data/knowledge/immigration-sources-catalog.md`, `data/knowledge/tax-sources-catalog.md`, `data/knowledge/driving-sources-catalog.md`, `data/knowledge/kwsp-perkeso-sources-catalog.md`, `data/knowledge/moh-healthcare-sources-catalog.md`, `data/knowledge/education-sources-catalog.md`, `data/knowledge/customs-travel-sources-catalog.md`, `data/knowledge/government-services-sources-catalog.md`
- Batch 9 (Government Services): 4 sources from jpn.gov.my (JPN National Registration Department), category = "government-services"
- Seed is idempotent — SHA-256 deduplication skips unchanged content

## AI Government Assistant Architecture (Phase 7)
```
Flutter → POST /api/assistant/conversations → creates AIConversation
Flutter → POST /api/assistant/conversations/{id}/messages → sends user question
        → IKnowledgeSearchService.SearchAsync (embed query → pgvector cosine search)
        → filter by relevance threshold (default 0.3)
        → format knowledge context with provenance
        → IAIService.GenerateResponseAsync (Gemini with system_instruction + context)
        → persist user + assistant messages in AIConversationMessage
        → return grounded answer + source citations
```

- RAG (Retrieval-Augmented Generation) pipeline: embed user question → vector search → inject context → generate grounded answer
- Gemini instructed to answer ONLY from retrieved government knowledge context
- If no relevant knowledge found (below threshold), returns safe "insufficient evidence" response
- Relevance threshold configurable via `Assistant:RelevanceThreshold` in appsettings (default 0.7)
- Conversation stored in existing AIConversation entity (Module = "government-assistant")
- Messages stored in AIConversationMessage (Role = "user" or "assistant")
- Source references serialized as JSON in AIConversationMessage.SourceReferences column
- Conversation ownership enforced: users can only access their own conversations
- Conversation title auto-set from first user message (truncated to 100 chars)
- Conversation history (last 10 messages) included in Gemini context for follow-up questions
- Gemini role mapping: DB stores "assistant", Gemini API expects "model"
- Temperature 0.2 for factual grounding
- Conditional DI: if Gemini:ApiKey not configured, assistant endpoints return 503
- Flutter chat UI with message bubbles, source cards with official URLs, empty state with example questions
- No streaming — synchronous request-response (ApiClient has no streaming support)
- Transient Gemini errors (429/503/5xx) retried up to 3 times with exponential backoff (500/1000/2000ms)
- AIProviderUnavailableException maps to HTTP 503 in middleware; Flutter shows clean unavailable message
- Permanent errors (400/401/403) are not retried

## Translation Architecture (Phase 8)
```
Flutter → POST /api/translation/translate → TranslationService → IAIService.TranslateAsync → Gemini
Flutter → GET /api/translation/languages → returns supported language list
```

- Translation is completely independent from Government RAG/Assistant — no knowledge search, no database storage
- Stateless: no translations stored in database, no translation table
- Supported languages: en (English), ms (Malay), zh (Chinese Simplified), ta (Tamil), hi (Hindi), ar (Arabic), ja (Japanese), ko (Korean)
- Auto-detect source language ("auto") supported — Gemini infers the source language
- Same-language requests return original text without calling Gemini (case-insensitive comparison)
- Max text length: 5000 characters (configurable via `Translation:MaxTextLength`)
- Temperature 0.1 for translation accuracy
- Translation system prompt: translate only, preserve names/numbers/dates/URLs, never answer questions, never summarize
- Retry pattern: same as other Gemini calls (MaxRetries=3, delays [500,1000,2000]ms, transient = 429/503/5xx)
- Conditional DI: if Gemini:ApiKey not configured, translation endpoints return 503
- Flutter UI: language selectors with swap, text input (5000 char limit with counter), translate button, result card with copy, loading/error states
- Navigation: 6th tab in AppShell (between Assistant and Translate)

## Emergency Assistant Architecture (Phase 9)

Layer 1 — Deterministic emergency actions (Flutter-only, no backend required):
```
Flutter → native phone action (tel:999) → Emergency services
Flutter → Geolocator → device GPS → location display
Flutter → SharePlus → native share sheet → location sharing
```

Layer 2 — Optional AI assistance:
```
Flutter → POST /api/emergency/assist → EmergencyAssistService → IAIService → Gemini
```

- Core principle: Emergency actions MUST NOT depend on Gemini or the backend
- Call 999 uses url_launcher with `tel:` URI scheme (opens native dialer, no CALL_PHONE permission needed)
- Fallback message if dialing unavailable (emulator, tablet, desktop)
- Location is on-demand only — user initiates, never continuously tracked
- Location uses geolocator package with ACCESS_FINE_LOCATION permission
- Location permission requested only when user taps "Get Location"
- Share location generates text with lat/lng and Google Maps URL via share_plus
- AI emergency assistance is optional — helps prepare messages and translate for responders
- Emergency system prompt: prioritize 999, do not diagnose, do not prescribe, do not invent numbers
- AI errors show "You can still call 999 directly" — never blocks emergency actions
- Verified emergency number: 999 (MERS — connects police, fire, ambulance, civil defence, maritime)
- Source: malaysia.gov.my MERS 999 page and PDRM FAQ
- No unverified emergency numbers exposed
- Translation via existing IAIService.TranslateAsync — same 8 languages as Phase 8
- Max message length: 2000 characters (configurable via `Emergency:MaxMessageLength`)
- Conditional DI: if Gemini:ApiKey not configured, assist endpoint returns 503 (core actions still work)
- Navigation: 7th tab in AppShell (between Translate and Reminders)
- Flutter packages added: geolocator ^13.0.2, share_plus ^13.3.0
- Android: ACCESS_FINE_LOCATION + ACCESS_COARSE_LOCATION permissions, tel: DIAL query intent
- iOS: NSLocationWhenInUseUsageDescription in Info.plist

## Profile + Personalized Onboarding Architecture (Phase 10.2)
```
Flutter → OnboardingScreen (8-step wizard) → PUT /api/users/me (saves each step)
Flutter → GET /api/profile/options → ProfileOptionsDto (visa types, employment, family, languages)
Flutter → GET /api/profile/checklist → List<ChecklistItemDto> (rule-based, deterministic)
Flutter → GET /api/users/me → check OnboardingCompleted → route to onboarding or dashboard
```

- User entity extended with 8 new columns: Nationality, ResidenceLocation, VisaPassType, EmploymentStatus, FamilyStatus, HasChildren, NumberOfChildren, OnboardingCompleted
- Enums: VisaPassType (EmploymentPass, DependantPass, MM2H, StudentPass, ProfessionalVisitPass, LongTermSocialVisitPass, Other), EmploymentStatus (Employed, SelfEmployed, Unemployed, Student, Retired, Other), FamilyStatus (Single, Married, MarriedWithFamily, Other)
- Enums stored as strings (same pattern as DocumentStatus, ReminderStatus)
- All new columns nullable except OnboardingCompleted (defaults false)
- Existing users get OnboardingCompleted=false → prompted to onboard on next login
- Onboarding wizard: 8 steps (Welcome, Nationality, Residence, Visa/Pass, Employment, Family, Language, Complete)
- Each step saves progress via PUT /users/me (resumable — if app closes mid-wizard, user resumes where they left off)
- Final step sets OnboardingCompleted=true
- Checklist is deterministic/rule-based (NOT AI): checks user documents, profile fields, conversations, reminders
- Dashboard shows: greeting, visa badge, profile completion card, upcoming reminders, onboarding checklist, quick actions
- Profile editing via full-page form with all fields; email read-only (immutable)
- Validation: VisaPassType/EmploymentStatus/FamilyStatus against enum values, PreferredLanguage against supported codes, Nationality 2-5 chars, NumberOfChildren 0-20
- ProfileController: GET /api/profile/options (public), GET /api/profile/checklist (auth required)

## Document Storage Architecture
```
Flutter → pick file → POST /api/documents/upload-url → presigned PUT URL
Flutter → PUT file directly to S3 → POST /api/documents/{id}/complete
Flutter → GET /api/documents/{id}/access-url → presigned GET URL (5 min expiry)
```

- Files stored in private S3 bucket, metadata in PostgreSQL
- S3 object key: `users/{userId}/documents/{documentId}/{sanitizedFilename}`
- All S3 objects are private — access only via short-lived presigned URLs
- File validation: PDF/JPEG/PNG only, max 10 MB, zero-byte rejected
- Filename sanitized — path traversal rejected
- Delete strategy: S3 first, then DB (if S3 fails, metadata remains for retry)
- Document types seeded in DB (Passport, Visa, Employment Pass, etc.)
- Future: malware scanning as production hardening

## Authentication Architecture
```
Flutter → Firebase Auth → ID Token → ASP.NET Core → Token Validation → ExternalId+ExternalProvider → PostgreSQL User
```

- Firebase handles authentication (email/password, Google Sign-In)
- Backend validates Firebase ID tokens via custom AuthenticationHandler
- User provisioning is idempotent: find-or-create by (ExternalProvider, ExternalId)
- All protected endpoints require `Authorization: Bearer <Firebase ID Token>`
- Identity comes from validated token claims, never from client-provided data
- Composite unique index on (ExternalProvider, ExternalId) ensures no duplicate users

## API Endpoints
- `GET /api/health` — Returns API status + database connectivity (public)
- `GET /api/users/me` — Get authenticated user's profile (requires auth)
- `PUT /api/users/me` — Update authenticated user's profile (requires auth)
- `GET /api/documents` — List authenticated user's documents (requires auth)
- `GET /api/documents/{id}` — Get document metadata (requires auth + ownership)
- `POST /api/documents/upload-url` — Request presigned S3 upload URL (requires auth)
- `POST /api/documents/{id}/complete` — Mark upload as complete (requires auth + ownership)
- `GET /api/documents/{id}/access-url` — Get short-lived signed download URL (requires auth + ownership)
- `DELETE /api/documents/{id}` — Delete document from S3 and DB (requires auth + ownership)
- `POST /api/documents/{id}/analyze` — AI-analyze a document via Gemini (requires auth + ownership)
- `GET /api/documents/{id}/analysis` — Get cached AI analysis (requires auth + ownership)
- `POST /api/documents/{id}/ask` — Ask a question about a document via Gemini (requires auth + ownership)
- `POST /api/documents/{id}/versions/upload-url` — Request presigned S3 upload URL for new version (requires auth + ownership)
- `POST /api/documents/{id}/versions/{versionId}/complete` — Mark version upload complete + set as current (requires auth + ownership)
- `GET /api/documents/{id}/versions` — List all versions of a document (requires auth + owner or shared access)
- `GET /api/documents/{id}/versions/{versionId}/access-url` — Get short-lived signed download URL for a version (requires auth + owner or shared access)
- `DELETE /api/documents/{id}/versions/{versionId}` — Delete a specific version (requires auth + ownership)
- `POST /api/documents/{id}/shares` — Share document read-only with another user by email (requires auth + ownership)
- `GET /api/documents/{id}/shares` — List all shares for a document (requires auth + ownership)
- `DELETE /api/documents/{id}/shares/{shareId}` — Revoke a share (requires auth + ownership)
- `GET /api/documents/shared-with-me` — List documents shared with the authenticated user (requires auth)
- `GET /api/documents/{id}/shared-access-url` — Get access URL for a shared document (requires auth + active share)
- `GET /api/documents/{id}/audit-logs` — Get audit log for a document (requires auth + ownership)
- `GET /api/document-types` — List available document types (public)

- `GET /api/reminders` — List authenticated user's active reminders (requires auth)
- `GET /api/reminders/{id}` — Get specific reminder (requires auth + ownership)
- `POST /api/reminders` — Create a reminder for a document (requires auth + ownership)
- `POST /api/reminders/generate` — Auto-generate reminders with configurable offsets (requires auth)
- `PUT /api/reminders/{id}` — Update reminder status (requires auth + ownership)
- `DELETE /api/reminders/{id}` — Delete a reminder (requires auth + ownership)
- `GET /api/reminders/document/{docId}` — List reminders for a document (requires auth + ownership)

- `GET /api/knowledge/search?q=...&countryCode=MY&maxResults=5` — Semantic search (requires auth)
- `GET /api/knowledge/sources` — List government sources (requires auth + X-Admin-Key)
- `POST /api/knowledge/sources` — Register a government source (requires auth + X-Admin-Key)
- `GET /api/knowledge/sources/{id}` — Get a government source (requires auth + X-Admin-Key)
- `DELETE /api/knowledge/sources/{id}` — Deactivate a government source (requires auth + X-Admin-Key)
- `POST /api/knowledge/sources/{id}/ingest` — Ingest content into knowledge chunks (requires auth + X-Admin-Key)
- `POST /api/knowledge/embeddings/generate` — Generate embeddings for pending chunks (requires auth + X-Admin-Key)

- `POST /api/assistant/conversations` — Create a new assistant conversation (requires auth)
- `GET /api/assistant/conversations` — List authenticated user's conversations (requires auth)
- `GET /api/assistant/conversations/{id}` — Get conversation with messages (requires auth + ownership)
- `POST /api/assistant/conversations/{id}/messages` — Send message, get RAG-grounded response (requires auth + ownership)
- `DELETE /api/assistant/conversations/{id}` — Delete conversation (requires auth + ownership)

- `POST /api/translation/translate` — Translate text between supported languages (requires auth)
- `GET /api/translation/languages` — List supported languages (requires auth)

- `POST /api/emergency/assist` — AI emergency communication assistance with optional translation (requires auth)

- `GET /api/profile/options` — List valid visa types, employment/family statuses, supported languages (public)
- `GET /api/profile/checklist` — Get personalized onboarding checklist (requires auth)

Old endpoints `GET /api/users/{id}` and `POST /api/users` have been removed.

Backend runs on `http://localhost:5000` (configured in launchSettings.json).

## How to Run
```bash
# Backend
export PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH"
export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"
cd src/backend/ExpatOne.Api && dotnet run

# Flutter (in another terminal)
cd src/mobile && flutter run

# Tests (build first, then test with --no-build to avoid MSBuild tlog issue with space in path)
cd src/backend && dotnet build ExpatOne.sln && dotnet test ExpatOne.sln --no-build
cd src/mobile && flutter test
```

## Environment Variables
Required in `appsettings.Development.json` (gitignored):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=expatone;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

Backend also requires Firebase configuration in `appsettings.Development.json`:
```json
{
  "Firebase": {
    "ProjectId": "your-firebase-project-id",
    "CredentialPath": "/path/to/firebase-service-account.json"
  }
}
```

Gemini API key in `appsettings.Development.json` (for Phase 5+):
```json
{
  "Gemini": {
    "ApiKey": "your-gemini-api-key",
    "Model": "gemini-2.5-flash",
    "EmbeddingModel": "gemini-embedding-2"
  },
  "Knowledge": {
    "AdminKey": "your-admin-key"
  },
  "Assistant": {
    "RelevanceThreshold": "0.7"
  }
}
```

## Key Abstractions (in Application layer)
- `IAIService` — AI provider abstraction (GeminiAIService: AnalyzeDocumentAsync Phase 5, GenerateResponseAsync Phase 7, GenerateEmbeddingAsync Phase 6, TranslateAsync Phase 8)
- `ITranslationService` — Stateless text translation via Gemini, language validation, same-language no-op (TranslationService implemented Phase 8)
- `IEmergencyAssistService` — AI emergency communication assistance with translation, safety-constrained prompt (EmergencyAssistService implemented Phase 9)
- `IDocumentAnalysisService` — Document AI analysis + caching (DocumentAnalysisService implemented Phase 5)
- `IStorageService` — File storage abstraction (S3StorageService implemented)
- `IDocumentService` — Document CRUD, upload, access, ownership (DocumentService implemented)
- `IReminderService` — Reminder CRUD, auto-generation, processing (ReminderService implemented)
- `INotificationService` — Push notification abstraction (FCM deferred)
- `IKnowledgeSearchService` — Semantic vector search (KnowledgeSearchService implemented Phase 6)
- `IKnowledgeIngestionService` — Source registration, content chunking, embedding generation (KnowledgeIngestionService implemented Phase 6)
- `IAssistantService` — RAG chat pipeline: conversation CRUD, message sending with knowledge retrieval + Gemini generation (AssistantService implemented Phase 7)
- `IProfileService` — Profile options + deterministic onboarding checklist generation (ProfileService implemented Phase 10.2)
- `IUserService` — User CRUD operations with profile field validation (implemented in Infrastructure)

## Critical Rules
1. **Malaysia-first** — country-aware architecture but only MY data for MVP
2. **Gemini, not OpenAI** — all AI goes through IAIService -> GeminiAIService
3. **No business logic in Flutter** — Flutter calls backend APIs only
4. **S3 for documents, PostgreSQL for metadata** — never store files in DB
5. **Never expose API keys in Flutter** — all external API calls from backend
6. **Build incrementally** — only implement the current phase
7. **Security is first-class** — auth tokens validated server-side, no client-trust
8. **AI safety** — never present unverified info as fact, show sources/disclaimers
9. **User identity is provider-agnostic** — ExternalId + ExternalProvider, not FirebaseUid

## Firebase Setup (for developers)
1. Create a Firebase project at https://console.firebase.google.com
2. Enable Authentication → Email/Password and Google Sign-In
3. Add Android app (package: `com.expatone.expatone_app`) → download `google-services.json` to `src/mobile/android/app/`
4. Add iOS app (bundle ID from Xcode) → download `GoogleService-Info.plist` to `src/mobile/ios/Runner/`
5. Run `flutterfire configure` (or manually place config files)
6. Generate a Firebase Admin SDK service account key → save outside repo
7. Set `Firebase:ProjectId` and `Firebase:CredentialPath` in `appsettings.Development.json`
8. Do NOT commit service account JSON or `google-services.json` / `GoogleService-Info.plist`

## AWS S3 Setup (for developers)
1. Create an S3 bucket (e.g. `expatone-documents`) in `ap-southeast-1`
2. Block all public access on the bucket
3. Enable server-side encryption (AES-256 or KMS)
4. Create an IAM user/role with minimum permissions: `s3:PutObject`, `s3:GetObject`, `s3:DeleteObject` on the bucket
5. Configure credentials via environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`) or IAM role
6. Set `Aws:Region` and `Aws:S3BucketName` in `appsettings.Development.json`
7. Do NOT use `AdministratorAccess` — least privilege only
8. Do NOT commit AWS credentials

## Phase Roadmap
- Phase 0: Architecture (DONE)
- Phase 1: Application Foundation (DONE)
- Phase 2: Authentication & User Management (DONE)
- Phase 3: Smart Document Wallet + S3 (DONE)
- Phase 4: Reminder Center (DONE)
- Phase 5: AI Document Reader (DONE)
- Phase 6: Government Knowledge Base (DONE)
- Phase 7: AI Government Assistant (DONE)
- Phase 8: Translation (DONE)
- Phase 9: Emergency Assistant (DONE)
- Phase 10: MVP Hardening
