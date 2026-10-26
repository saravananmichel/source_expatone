# Priority-country visa preparation templates

Based on `github-sync` commit `936531d`. Adds the first requested batch: Malaysia,
Vanuatu, India and China, each with Tourism, Employment and Dependent purposes.
Singapore's existing JSON matches the supplied `sg-visit.json` byte-for-byte and is
unchanged. Other destination rows and purposes are not presented as newly supported.

| Destination | Tourism | Employment | Dependent / family |
| --- | --- | --- | --- |
| Malaysia | `my-visit.json` | `my-employment.json` | `my-dependent.json` |
| Vanuatu | `vu-visit.json` | `vu-employment.json` | `vu-dependent.json` |
| India | `in-visit.json` | `in-employment.json` | `in-dependent.json` |
| China | `cn-visit.json` | `cn-employment.json` | `cn-dependent.json` |

Files are under `src/backend/ExpatOne.Infrastructure/VisaTemplates/`.
Each includes applicant/travel details, route-specific fields, document roles,
conditional evidence checks, official source URLs and review metadata.

## Scope and country differences

These are **application preparation checklists**, not replicas of every official
form and not automatic eligibility decisions. The user must confirm nationality,
filing jurisdiction, current authority checklist and additional evidence. No fee,
salary threshold, duration entitlement or approval is inferred. A completed
checklist means the user's preparation checks are confirmed, not government approval.

- Malaysia employment covers the ESD expatriate route in Peninsular Malaysia.
  Employer review includes the policy effective 1 June 2026; legacy salary guidance
  is not used as a hard-coded eligibility rule. Family applicants confirm DP versus
  LT-SVP and the principal pass conditions.
- Vanuatu has visitor/tourist, employment and residence/family pathways. Employment
  preparation distinguishes the Labour work permit from immigration permission.
  Some detailed Immigration pages returned HTTP 403 during research; indexed
  official descriptions and the eVisa portal were available. Its detailed checklists
  require confirmation with Immigration before filing.
- India distinguishes e-Tourist and regular tourist filing, regular employment,
  and the handling mission's Employment Dependent / Entry (X) category. Kuala Lumpur
  mission-specific evidence must not be assumed universal for other filing locations.
- China distinguishes L tourism, Z employment and S1/S2 family visits to foreign
  residents. Q routes for family of citizens/permanent residents are outside this
  batch. The Malaysia embassy's Chinese guidance (June 2026 update) is newer than
  its English page. Nationality exemptions must be checked separately.

Official guidance was checked on 2026-10-26. Review due 2027-01-24 is an internal
content-maintenance date, not a government guarantee that requirements stay unchanged.
Sources are recorded in each JSON. Preparation field limits are app limits and
are not represented as official form limits.

## Integration

- All 13 packs (original SG plus 12 additions) are embedded and seeded for fresh DBs.
- Migration `20261026100000_AddPriorityCountryVisaTemplates` inserts the new immutable
  template IDs, enables MY/IN/CN and registers/enables VU in `visa_destinations`.
  It preserves existing template IDs/content on conflict and does not modify other
  destinations. The general `countries` table is not the Application Assistant catalog.
- The migration is intentionally forward-only to avoid deleting templates referenced
  by saved drafts. Corrective changes should use a new version/forward migration;
  take a database backup before applying data migrations.
- Validation profile `preparation-v1` uses common data/evidence consistency checks;
  Singapore's nationality scope, 30-day window, stay bands and photograph rule remain
  scoped to the original SG pack. Conditional `eq:field:Value` checks have matching
  backend and Flutter evaluation.
- Form 14A/V39A export is guarded server-side and hidden client-side for non-SG packs.
  The new packs link to official application services; new official PDF mappings
  and direct government submission are not implemented.
- Draft titles and guidance come from the selected pack. Disabled destinations
  cannot create drafts even if a template exists. HR country/purpose selectors
  continue using the existing catalog without hard-coded additions.

## Validation and release

Passed locally:

- `python scripts/validate_visa_templates.py` verifies schemas, unique IDs, all 12
  combinations, conditions, document roles, and frozen migration/snapshot payloads.
- All backend projects, including API and tests, compiled under .NET 8. Existing
  dependency-version and nullable warnings remain.
- 35 compiled visa test cases passed through direct invocation of the test methods,
  including new catalog/evidence checks and existing draft/access/SG regression tests.
  Standard VSTest crashed during runner setup in this container before executing tests.
- A separate compiled smoke run of the real VisaRules/DTO code passed all 12
  preparation states, evidence/route/review blockers and SG guidance retention.
- Dart formatter parsed the changed Flutter files; `git diff --check` passed.

Still required before merge/release: normal CI test execution, Flutter analyze/widget
checks and web build, PostgreSQL migration verification and staging review. No
migration has been applied to a live database and nothing is deployed by this PR.

```sh
python scripts/validate_visa_templates.py
dotnet test src/backend/ExpatOne.Tests/ExpatOne.Tests.csproj --filter 'FullyQualifiedName~VisaApplicationTests|FullyQualifiedName~VisaTemplateCatalogTests'
cd src/mobile
flutter pub get
flutter analyze lib/features/applications/application_assistant_screen.dart test/visa_application_test.dart
flutter test test/visa_application_test.dart
flutter build web --release # supply the deployment's existing dart-defines
```

Deploy the API containing the migration/resources, apply the migration through the
existing migration process, then deploy the frontend. Verify all four enabled
countries expose three purposes, each can create/save a draft and link evidence,
and Singapore export remains available only for the original SG visit template.
