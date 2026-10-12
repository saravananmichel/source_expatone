# ExpatOne Application Assistant — Singapore visit visa

Prepared 12 October 2026. Source base: GitHub `feature/family-dashboard`, commit
`db04102ef4aa16b6cfa9ab9a979a7f007ae7c574`.

## What this release contains

- Selection checkboxes for every owned/family-shared document on Documents.
- Contextual application action and More → Application Assistant.
- Database-backed destination catalogue: Singapore, Malaysia, India, Indonesia,
  China, UAE, Saudi Arabia, Qatar, Kuwait, Bahrain and Oman.
- Singapore → Tourism is enabled. Other destinations and employment/dependent/
  renewal purposes are visibly planned, not falsely represented as supported.
- Saved drafts, document roles, reviewed suggestions, checklist evidence and
  computed Draft / InProgress / ChecklistComplete / Submitted / AccessBlocked states.
- Prefill from current private local analysis and passport metadata. Exactly one
  applicant passport is allowed. Missing or ambiguous values remain for manual entry.
- Editable official Form 14A and conditional Form V39A exports. Signature fields
  and signing dates stay blank. No forms are automatically submitted to government.
- Revision checks prevent overwriting another edit. Document authorization and
  version fingerprints are rechecked before read, prefill and export.
- Expired/revoked/changed shared documents block the draft and hide derived values.
  Resetting document links clears old values. The screen also clears at the supplied
  access deadline and checks access every 45 seconds and on resume.

## Important release scope

The first reviewed readiness rule pack covers **Indian ordinary-passport holders
visiting Singapore**, based on ICA guidance reviewed 12 October 2026. Other
nationalities can prepare forms, but cannot receive the green readiness status.
Their nationality/document-specific rules must be reviewed and added first.
Do not remove this restriction to make the checklist green for unsupported cases.

The green indicator means application checklist complete, including applicant
attestations. It does not authenticate a passport, verify a signature, certify a
photo's authenticity, guarantee visa approval, or guarantee admission. Users must
review every field and confirm the selected mission/agent/contact requirements.
The system requires linked evidence for passport/photo/signed forms/translations;
photo quality, signatures and route-specific additional evidence are reviewed by
people rather than inferred by the model.

Form generation is deterministic, uses your supplied SG.zip templates, and does
not send personal documents to third-party AI. Templates and rules are versioned
and bundled, not downloaded and trusted automatically at request time. Rules block
readiness after 10 January 2027 until reviewed and republished. Future rule changes
need a new template/rules version and database migration, with matching exporter
support. Keep the .NET and Python sg-visit.json files identical.

Selected itinerary/accommodation fields are suggested only when existing analysis
contains supported exact labels. There is no new synchronous LLM pass, and the
module does not enqueue or modify a family member's analysis. If no suggestion is
available, enter the value or ask the owner to finish Document Analysis. Ambiguous
non-ISO date strings are not guessed. A suggestion is never a verified requirement.

Forms have limited boxes; the UI enforces known lengths. PDF export rejects text
that cannot fit legibly or is not supported by the form's Latin fonts. Use the
Latin-script spelling in the passport. Very long answers may require an officially
accepted continuation page; this release does not create one automatically.
The form's less-than/more-than-30-day wording omits exactly 30 days; such a case
remains blocked for filing-contact review instead of choosing a category silently.

Travel readiness is displayed separately as guidance: onward travel, funds,
next-destination entry permission, applicable health requirements and SG Arrival
Card timing. It is not a second automated completeness certificate in this release.
Existing downloaded copies cannot be recalled after sharing ends. Server-side
drafts retain protected data until deleted/reset, but the API withholds it once
access/version validation fails. Existing S3 originals are not changed.

## Merge on your laptop (PowerShell)

Extract the delivery ZIP outside the repository. Use a clean working tree first;
commit only intended work or keep it on its own branch. Do not use git add . on a
repository containing Firebase JSON credentials, backups or task-definition secrets.

```powershell
cd C:\Saravanan\Pers\Projects\ExpatOne\GIT\Latest\source_expatone
git status
git fetch origin
git switch feature/family-dashboard
git pull --ff-only origin feature/family-dashboard
git switch -c feature/sg-visa-assistant

# Set this to the EXTRACTED delivery folder on your laptop.
$package = Read-Host "Full path to extracted ExpatOne-SG-Visa-Assistant folder"
git apply --check "$package\sg-visa-assistant.patch"
# Run the next command ONLY if the check succeeded.
git apply "$package\sg-visa-assistant.patch"
git diff --stat
git status
```

The patch includes new source files, the official PDF assets, the migration and
snapshot. Do not also copy `changed-files/` over it. That folder is provided for
review, not as a second installation step. If the patch check fails on newer
source, stop and inspect the affected files; do not force-overwrite them. In
particular preserve your CodeCommit Document AI pipeline customizations.

## Validate before deployment

```powershell
cd src/backend
dotnet restore ExpatOne.sln
dotnet test ExpatOne.Tests/ExpatOne.Tests.csproj
cd ../mobile
flutter pub get
flutter analyze
flutter test
cd ../../document-ai
python -m pytest tests/test_visa_forms.py -q
```

Use your normal Python virtual environment with document-ai requirements installed.
The delivery fixes one existing test constructor call that omitted the required
DocumentAnalysisJobs logger. No extraction/reasoning pipeline logic was changed.

Review CHANGED_FILES.txt. Stage those exact paths (or use the VS Code Source Control
file list), then commit and push the new branch:

```powershell
git commit -m "Add Singapore visa application drafts, checklist and PDF export"
git push -u origin feature/sg-visa-assistant
```

Do not commit backup ZIPs, local credentials or task-definition files.

## Bring the branch into CodeCommit / CodeBuild

Your build branch is `github-sync`. The build must include this commit on that
branch; pushing only a separate feature branch will not update that build.
In your AWS checkout, inspect `git status` and `git remote -v` first. Preserve any
uncommitted Document AI changes before merging. Assuming `origin` is GitHub and
`codecommit` is your existing AWS remote:

```bash
git fetch origin feature/sg-visa-assistant
git switch github-sync
git merge --no-ff origin/feature/sg-visa-assistant
# Resolve/review any conflicts, especially document-ai/app/main.py, then:
git push codecommit github-sync
```

If your remotes use different names, substitute the names shown by git remote -v.
Do not choose "take GitHub for everything"; the only main.py change is importing
and registering the new visa router next to app.add_middleware(IngestionLimits).

## AWS deployment order

1. Build **expatone-api** and **document-ai** from the merged commit using your
   existing CodeBuild/ECR workflow. Ollama model/config does not need changing.
   The new Python router and `app/visa_templates/` must be in the document-ai image.
2. Register an ECS task definition revision referencing BOTH new images. Preserve
   working database/Firebase secrets, task role, CPU/memory, ports and networking.
   Reuse existing API `DocumentIntelligence__ServiceUrl=http://localhost:8090/`
   and `DocumentIntelligence__ServiceKey`, matching document-ai's
   `DOCUMENT_AI_SERVICE_KEY`. Do not put that service key in Flutter.
3. Before updating the running API service, run a standalone migration task using
   the new API image and the **same VPC subnets/security group and database secret
   as the working service**. Override only the expatone-api container command to
   `["--migrate-database"]`. Do not add `dotnet ExpatOne.Api.dll` to that command;
   the image entrypoint already supplies it. Wait for API exit code 0 and successful
   migration log. A connection timeout means networking/config must be fixed first.
4. Confirm migration `20261012190000_AddVisaApplications` was applied. It creates
   `visa_destinations`, `visa_templates`, `visa_applications`, their indexes and
   foreign keys; it does not change existing family grants or document contents.
   All previous family migrations must already be applied.
5. Update `expatone-api-service` in `expatone-cluster` (ap-southeast-5) to the new
   revision. Wait for desired/running counts to match and rollout COMPLETED.
6. Build and deploy Flutter web as below. Existing Firebase configuration and CORS
   settings remain required. API CloudFront caching must remain disabled for
   authenticated endpoints, including `/api/visa-applications`.

An EF-generated idempotent PostgreSQL script is included as
`database/visa-migration.sql` for review or a DBA-controlled alternative. Prefer
the existing migration task. Do not manually create only the tables without the
EF history entry. Do not run a down migration against drafts you want to preserve.

Read-only database verification:

```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261012190000_AddVisaApplications';
SELECT to_regclass('public.visa_destinations'),
       to_regclass('public.visa_templates'),
       to_regclass('public.visa_applications');
SELECT "Code", "Name", "Enabled" FROM visa_destinations ORDER BY "Name";
```

## Flutter web publication

From src/mobile on your laptop (use your usual Firebase configuration flags too,
if your working build needs them):

```powershell
flutter build web --release --dart-define=API_BASE_URL=https://d38i3e3pvvjux7.cloudfront.net/api
aws s3 sync build/web s3://expatone-my-dev/web/ --region ap-southeast-5
aws cloudfront create-invalidation --distribution-id EKRXPCPU7UM9L --paths "/*"
```

Open https://d2hhd1l6dq87d2.cloudfront.net after the invalidation completes. Hard
refresh or use a new browser session if an older Flutter bundle is cached.
No broad S3 --delete command is required.

## First acceptance test

Use synthetic documents initially.

1. Select an owned passport and an actively shared document in Documents.
2. Open the application action → Singapore → Tourism → Create draft.
3. Link documents and set exactly one primary Passport role; choose appropriate
   roles for photo, itinerary and accommodation evidence.
4. Prefill, review suggestions and fill remaining form fields; save fields.
5. Export both applicable forms; inspect names/dates/ticks, review and sign manually.
   Upload signed copies to Documents, return to the draft, and link them using the
   Signed Form 14A / Signed V39A roles. Adding evidence preserves fields but clears
   prior checklist confirmations.
6. Complete each applicable check and attach the matching evidence. Confirm the
   filing route's current requirements and any extra documents. Save checklist.
   The checklist only turns green when the supported route's checks pass.
7. Change a field: prior confirmations must clear. Set an invalid passport date:
   readiness must be blocked. Try another nationality: drafts work, green is blocked.
8. Set a family grant to expire in two minutes. Once expired, viewing/prefilling/
   exporting derived draft values must be blocked. Confirm the document owner still
   has normal access. Also test revocation and replacing a source document version.
9. Test another logged-in user cannot access someone else's application ID.
10. "I have submitted" records the user's confirmation; it does not submit to ICA.

## Validation performed for this package

- Backend API compiled with .NET 8; final infrastructure including the migration
  compiled again with the .NET 8 C# compiler.
- 13 targeted backend smoke checks passed using EF InMemory: ownership, sharing,
  unknown fields, confirmation reset, revisions, expiry, export, reset, complete
  checklist, passport validity, unresolved requirements, unsupported nationality,
  and PostgreSQL migration SQL generation.
- 8 Python tests passed for PDF mapping, canonical/widget consistency, signatures,
  unanswered declarations, invalid inputs and service-key authorization.
- Rendered all four Form 14A pages and V39A using synthetic values and inspected
  the output. Fixed source-template comb-font clipping and invisible check marks.
- C# and Dart source syntax parsed successfully; git diff whitespace check passed.
- Full dotnet test execution could not run reliably because this environment's
  process namespace information breaks the test/build launcher. The supplied
  xUnit tests still need the normal SDK test run in your environment/CI.
- Flutter SDK download was unavailable here. **flutter analyze, flutter test and
  flutter build web have not been run here and are required before publication.**
- No live AWS/RDS migration or production deployment was performed.

## Official sources used

- https://www.ica.gov.sg/enter-transit-depart/entering-singapore/visa_requirements
- https://www.ica.gov.sg/enter-transit-depart/entering-singapore/visa_requirements/visa-detail-page/india
- https://www.ica.gov.sg/enter-transit-depart/entering-singapore
- https://www.ica.gov.sg/photo-guidelines
- Supplied SG.zip: Visit Visa/form14a.pdf and Visit Visa/formv39a.pdf.
