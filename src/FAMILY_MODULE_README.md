# ExpatOne family document access

Implementation added to the supplied backend and Flutter project. This package has not been deployed.

## How to use

1. Both users must already have signed in to ExpatOne so their accounts exist in the backend.
2. Open More → Family, or the Family icon in Documents.
3. Search using the other user's complete registered email, then choose Send request.
4. The recipient opens Family and chooses Review & accept or Reject.
5. Review & accept lists the recipient's own completed documents. Select all current documents or tick individual documents, then Accept & share.
6. The requester's Documents tab displays their own documents together with the granted family documents, labelled with their owner. Opening a family document uses a read-only screen.
7. Recipients can Manage documents to change the selection. Either participant can revoke an accepted request; the requester can cancel a pending request. Refresh Family to see changes made on another device. Documents refreshes when selected, after returning from its Family shortcut, and through pull-to-refresh or the refresh button.

Sharing is directional: A requests B's documents, B selects B's documents, A receives read access. It does not share A's documents with B. B can send a separate request to A if reciprocal access is wanted.

Select all selects the documents currently shown, not future uploads. Accepting with no documents grants no file access and allows later selection. A grant covers the selected document's current file, including a replacement version of that same document. Access to historical versions, AI analysis/Q&A, audits, deletion, uploads and resharing remains owner-only. Existing direct document sharing remains a separate feature and its grants are unaffected by family revocation.

Requests appear in-app; no email or push notification delivery was added. Search uses exact case-insensitive email matching, excludes self and inactive users, and fails closed if more than one account matches. Family endpoints are authenticated and have a separate per-user rate limit of 60 requests/minute.

## Database changes

| Table | Purpose |
| --- | --- |
| family_request_statuses | Lookup: 1 Pending, 2 Accepted, 3 Rejected, 4 Cancelled, 5 Revoked |
| family_requests | Directional participants, status FK, timestamps, concurrency revision |
| family_document_grants | Request/document composite key; explicit selected documents |

Migration: `20261011020000_AddFamilySharing`. Includes migration target metadata and an updated EF model snapshot. A partial unique index permits only one pending/accepted request per directional pair. Self-requests have a database check constraint. Status transitions and grant updates use an atomic SaveChanges transaction with optimistic concurrency. Old requests remain available as history; grants are removed on revocation. Document deletion cascades to its grants.

Every family document list/detail/access-url lookup verifies an accepted request, an explicit grant, and matching ownership. Guessing a document ID does not grant access. File-access URLs expire after the existing five-minute interval: revocation prevents new URLs, but cannot invalidate a URL already issued or a file already downloaded.

## API

All routes use the existing Firebase bearer authentication. Requester/recipient identity comes from the server's authenticated user, never a user ID supplied by the client.

| Method | Route | Body / result |
| --- | --- | --- |
| GET | /api/family/search?email=... | Exact account lookup |
| GET | /api/family/requests | Incoming and outgoing requests, states, revisions, selected document IDs |
| POST | /api/family/requests | `{ "email": "member@example.com" }` |
| POST | /api/family/requests/{id}/respond | `{ "revision": "GUID", "accept": true, "documentIds": ["GUID"] }` |
| PUT | /api/family/requests/{id}/documents | `{ "revision": "GUID", "documentIds": ["GUID"] }` |
| POST | /api/family/requests/{id}/end | `{ "revision": "GUID" }` |

Use the current revision returned by the list/response. A stale revision produces HTTP 409: reload the request before retrying. Acceptance and document updates are recipient-only. Only owned, completed documents are accepted. Unknown or unauthorized resources return 404. Existing `/api/documents`, `/api/documents/{id}` and `/api/documents/{id}/access-url` support the new family grants. New document DTO fields: `isSharedWithMe`, `ownerEmail`, `ownerDisplayName`.

## Validation and rollout

The editing environment did not contain `dotnet`, `flutter` or `dart`. Compilation, EF migration execution, Flutter analysis and the added tests could not be run here. Source/route review and archive integrity checks were performed. Do not treat the package as build-verified.

Run in your development environment from the extracted `src` directory:

```powershell
# .NET 8 SDK
cd backend
dotnet restore ExpatOne.sln
dotnet test ExpatOne.Tests/ExpatOne.Tests.csproj
cd ../mobile
flutter pub get
flutter analyze
flutter test
```

Added tests: `backend/ExpatOne.Tests/FamilyServiceTests.cs` (six relational SQLite tests) and `mobile/test/family_test.dart` (two widget tests). They cover selected sharing, access denial, owner-only deletion, invalid selections, stale requests, rejection/cancellation, future-upload privacy, permission changes/revocation, mobile select-all/deselect, and the read-only screen. SQLite tests do not prove PostgreSQL migration compatibility; run the migration and two-account acceptance flow against staging PostgreSQL.

Apply the migration against the intended database before exposing the new API/mobile screens. The existing API supports a migration-only startup command:

```powershell
# From src/backend, using your existing secure database configuration:
dotnet run --project ExpatOne.Api -- --migrate-database
```

For the built container, run `dotnet ExpatOne.Api.dll --migrate-database` as a one-off ECS task with the same database secrets, network and permissions as the API. Check the task exits successfully and logs `Database migrations applied.` before rolling out the normal API service. Build/deploy the updated API image and rebuild/release Flutter; updating ECS alone does not update the mobile client.

Staging acceptance checklist:

- A requests B. Pending request exposes none of B's documents.
- B selects 2 of 10 documents. A sees only those 2 alongside A's own documents and opens both.
- Direct GET/access-url for one of B's other 8 documents is denied to A; DELETE for a granted document is also denied.
- B removes one grant, then revokes the request. Refresh A's list and verify new access-url requests fail.
- Test all-current selection, rejection, cancellation, empty selection, a newly uploaded private document, duplicate send, and concurrent/stale edits.
- Verify existing upload, AI analysis, direct sharing and owner document operations still work.
