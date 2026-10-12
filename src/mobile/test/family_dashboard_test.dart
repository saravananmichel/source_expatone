import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/document_service.dart';
import 'package:expatone_app/core/services/family_service.dart';
import 'package:expatone_app/features/family/family_dashboard_screen.dart';
import 'package:expatone_app/features/family/family_alerts_panel.dart';

Map<String, dynamic> document(String id, int? days, {bool owned = false, bool confirmed = false}) => {
  'id': id, 'name': id, 'documentType': 'Passport', 'fingerprint': 'fingerprint',
  'owner': {'id': owned ? 'me' : 'member', 'email': owned ? 'me@test.com' : 'member@test.com'},
  'isOwned': owned, 'expiryConfirmed': confirmed, 'remindersAllowed': true,
  'expiryDate': days == null ? null : '2026-11-01T00:00:00Z', 'daysRemaining': days,
  'expiryStatus': days == null ? 'No expiry date' : days < 0 ? 'Expired' : days <= 90 ? 'Due soon' : 'Valid',
};
void main() {
  test('Date filters include today and boundaries, without treating missing dates as expired', () {
    for (final days in [0, 7, 30]) {
      expect(FamilyDashboardDocument.fromJson(document('d', days)).matches('Next 30 days'), true);
    }
    expect(FamilyDashboardDocument.fromJson(document('d', 31)).matches('Next 30 days'), false);
    expect(FamilyDashboardDocument.fromJson(document('d', 90)).matches('Next 90 days'), true);
    expect(FamilyDashboardDocument.fromJson(document('d', -1)).matches('Next 90 days'), false);
    expect(FamilyDashboardDocument.fromJson(document('d', null)).matches('Expired'), false);
    expect(FamilyDashboardDocument.fromJson(document('d', null)).matches('No expiry date'), true);
  });
  testWidgets('Dashboard filters by expiry and only owner sees confirmation action', (tester) async {
    final service = DocumentService(ApiClient(httpClient: MockClient((request) async {
      if (request.url.path.endsWith('/family/dashboard')) {
        return http.Response(jsonEncode([document('My passport', 20, owned: true), document('Shared visa', -1)]), 200);
      }
      return http.Response('[]', 200);
    })));
    await tester.pumpWidget(MaterialApp(home: FamilyDashboardScreen(documentService: service)));
    await tester.pumpAndSettle();
    expect(find.text('Confirm expiry date'), findsOneWidget);
    await tester.tap(find.widgetWithText(ChoiceChip, 'Expired'));
    await tester.pumpAndSettle();
    expect(find.text('My passport'), findsNothing);
    expect(find.text('Shared visa'), findsOneWidget);
    expect(find.text('Confirm expiry date'), findsNothing);
  });
  testWidgets('Owner confirmation sends fingerprint and accepts a 204 response', (tester) async {
    var confirmed = false;
    Map<String, dynamic>? submitted;
    final service = DocumentService(ApiClient(httpClient: MockClient((request) async {
      if (request.method == 'POST') {
        submitted = jsonDecode(request.body) as Map<String, dynamic>;
        confirmed = true; return http.Response('', 204);
      }
      if (request.url.path.endsWith('/family/dashboard')) {
        return http.Response(jsonEncode([document('My passport', 20, owned: true, confirmed: confirmed)]), 200);
      }
      return http.Response('[]', 200);
    })));
    await tester.pumpWidget(MaterialApp(home: FamilyDashboardScreen(documentService: service)));
    await tester.pumpAndSettle();
    final confirmButton =
    find.widgetWithText(TextButton, 'Confirm expiry date');

    await tester.ensureVisible(confirmButton);
    await tester.pumpAndSettle();

    expect(confirmButton.hitTestable(), findsOneWidget);
    await tester.tap(confirmButton);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Confirm date'));
    await tester.pumpAndSettle();
    expect(submitted, {'fingerprint': 'fingerprint'});
    expect(find.text('Date confirmed by owner'), findsOneWidget);
    expect(find.text('Confirm expiry date'), findsNothing);
  });
  testWidgets('Dismiss refreshes alerts and hides the dismissed item', (tester) async {
    var dismissed = false;
    final service = FamilyService(ApiClient(httpClient: MockClient((request) async {
      if (request.method == 'POST') { dismissed = true; return http.Response('', 204); }
      return http.Response(jsonEncode(dismissed ? [] : [{
        'id': 'alert', 'kind': 'expiry', 'title': 'Passport due', 'message': 'Expires in 7 days',
      }]), 200);
    })));
    await tester.pumpWidget(MaterialApp(home: Scaffold(body: FamilyAlertsPanel(service: service))));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Dismiss'));
    await tester.pumpAndSettle();
    expect(dismissed, true); expect(find.text('Passport due'), findsNothing);
    expect(find.textContaining('No new family alerts.'), findsOneWidget);
  });
}
