# ExpatOne Business dashboard

Responsive corporate workspace based on the approved navy/teal design:

- Overview with live HR metrics, overdue/upcoming task shortcut, employee/task previews.
- Dedicated Employees and Tasks views, reached by overview links or navigation.
- Mobile bottom navigation; desktop sidebar at 1000 logical pixels.
- Employee name/email search with membership status and role filters.
- Task title, assignee name/email and purpose search with status, destination and due-date filters/sorting.
- Empty results, refresh, error/retry, company switching and role-specific controls.
- Employee details and existing owner-only role/status editing.
- Task details plus existing edit, status, linked visa viewer and employee link/unlink actions.
- Company creation and invitation acceptance/decline remain available.

The mockup's illustrative task names, dates and metrics are not embedded in the app.
Search is case-insensitive and trims surrounding whitespace. Filters combine with AND.
HR metrics count active Employee/Viewer memberships as defined by the existing API;
the Employees list includes administrative and inactive memberships too.
The deadline shortcut includes overdue cases, matching the API metric's semantics.
Task status labels use actual backend values (Open, In progress, Blocked, Closed).
Destination filters currently display the stored country code.

## Deployment

Deploy the API update before or together with the frontend. The cases endpoint keeps
its array response and 250-row page size, and adds non-negative `skip` with stable
ordering by due date then ID. The frontend loads all pages before presenting search.
An older API ignoring `skip` is detected and reported rather than silently truncating
search or looping forever. No database migration or new dependency is required.
Authorization remains in the existing API; filtering does not grant access.

## Validation

From `src/mobile`:

```sh
flutter pub get
flutter analyze lib/features/corporate/business_dashboard.dart lib/features/corporate/corporate_screen.dart lib/core/services/corporate_service.dart test/business_dashboard_test.dart
flutter test test/business_dashboard_test.dart
```

From `src/backend`:

```sh
dotnet test ExpatOne.Tests/ExpatOne.Tests.csproj --filter FullyQualifiedName~CorporateInvitationEndpointTests
```

Check with an admin/HR account and an employee account, including a company with
more than 250 tasks. Verify existing invite, edit, linked visa/document access and
company switching against staging before releasing. The pull request does not deploy.
