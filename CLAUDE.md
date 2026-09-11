# ExpatOne — Project Context for Claude Code

## What is this project?
ExpatOne is a mobile app for foreigners/expats living in Malaysia. It's an AI-powered life-admin assistant covering government processes, document management, reminders, translations, and emergency info.

## Current Phase
**Phase 1 — Application Foundation** (COMPLETE)

Next: Phase 2 — Authentication & User Management

## Tech Stack
- **Mobile**: Flutter 3.47.3 (Android + iOS)
- **Backend**: ASP.NET Core 8 Web API (.NET 8.0.131, modular monolith)
- **Database**: PostgreSQL 18 (localhost:5432, user: postgres, db: expatone)
- **ORM**: Entity Framework Core 8.0 + Npgsql
- **AI Provider**: Google Gemini (NOT OpenAI) — interface defined, not yet implemented
- **File Storage**: Amazon S3 — interface defined, not yet implemented
- **Auth**: Firebase Authentication — planned for Phase 2
- **Notifications**: Firebase Cloud Messaging — planned for Phase 4
- **Vector Search**: pgvector — planned for Phase 6

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

Seed data: Malaysia (country code MY).

```bash
# Apply migrations
export PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH:/Users/austincelestia/.dotnet/tools"
export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"
cd src/backend
dotnet ef database update --project ExpatOne.Infrastructure --startup-project ExpatOne.Api
```

## API Endpoints
- `GET /api/health` — Returns API status + database connectivity
- `GET /api/users/{id}` — Get user by ID
- `POST /api/users` — Create user (accepts CreateUserDto)

Backend runs on `http://localhost:5000` (configured in launchSettings.json).

## How to Run
```bash
# Backend
export PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH"
export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"
cd src/backend/ExpatOne.Api && dotnet run

# Flutter (in another terminal)
cd src/mobile && flutter run

# Tests
cd src/backend && dotnet test ExpatOne.sln
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

Future phases will add: GEMINI_API_KEY, AWS credentials, FIREBASE_PROJECT_ID.

## Key Abstractions (in Application layer)
- `IAIService` — AI provider abstraction (Gemini implementation in Phase 5-7)
- `IStorageService` — File storage abstraction (S3 implementation in Phase 3)
- `INotificationService` — Push notification abstraction (FCM in Phase 4)
- `IKnowledgeSearchService` — RAG/vector search abstraction (Phase 6)
- `IUserService` — User CRUD operations (implemented in Infrastructure)

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

## Phase Roadmap
- Phase 0: Architecture (DONE)
- Phase 1: Application Foundation (DONE)
- Phase 2: Authentication & User Management
- Phase 3: Smart Document Wallet
- Phase 4: Reminder Center
- Phase 5: AI Document Reader
- Phase 6: Government Knowledge Base
- Phase 7: AI Government Assistant
- Phase 8: Translation
- Phase 9: Emergency Assistant
- Phase 10: MVP Hardening
