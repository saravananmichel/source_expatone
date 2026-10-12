import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/document_service.dart';
import 'package:expatone_app/core/services/family_service.dart';
import 'package:expatone_app/features/family/family_screen.dart';

Map<String, dynamic> requestJson({String status = 'Accepted', String? expiry}) => {
  'id': 'r', 'revision': 'v', 'status': status, 'isIncoming': true,
  'requester': {'id': 'viewer', 'email': 'viewer@test.com'},
  'recipient': {'id': 'owner', 'email': 'owner@test.com'},
  'documentIds': [], 'accessExpiresAt': expiry,
};
void main() {
  test('Expired accepted sharing is derived correctly; cancelled requests are not labelled expired', () {
    final expired = DateTime.now().subtract(const Duration(days: 1)).toUtc().toIso8601String();
    expect(FamilyRequest.fromJson(requestJson(expiry: expired)).isExpired, true);
    expect(FamilyRequest.fromJson(requestJson(status: 'Cancelled', expiry: expired)).isExpired, false);
    expect(FamilyRequest.fromJson(requestJson()).isExpired, false);
  });
  test('API preserves omitted expiry and explicitly clears or sets expiry', () async {
    final bodies = <Map<String, dynamic>>[];
    final service = FamilyService(ApiClient(httpClient: MockClient((request) async {
      bodies.add(jsonDecode(request.body) as Map<String, dynamic>); return http.Response('{}', 200);
    })));
    final r = FamilyRequest.fromJson(requestJson());
    await service.update(r, []);
    await service.update(r, [], updateAccessExpiry: true);
    final date = DateTime.parse('2030-01-01T12:00:00+08:00');
    await service.update(r, [], updateAccessExpiry: true, accessExpiresAt: date);
    expect(bodies[0].containsKey('accessExpiresAt'), false);
    expect(bodies[1]['updateAccessExpiry'], true); expect(bodies[1]['accessExpiresAt'], null);
    expect(bodies[2]['accessExpiresAt'], '2030-01-01T04:00:00.000Z');
  });
  testWidgets('Owner can accept with temporary access and a preset deadline', (tester) async {
    Map<String, dynamic>? body;
    final service = DocumentService(ApiClient(httpClient: MockClient((request) async {
      if (request.method == 'POST') { body = jsonDecode(request.body) as Map<String, dynamic>; return http.Response('{}', 200); }
      if (request.url.path.endsWith('/family/requests')) return http.Response(jsonEncode([requestJson(status: 'Pending')]), 200);
      return http.Response('[]', 200);
    })));
    await tester.pumpWidget(MaterialApp(home: FamilyScreen(documentService: service)));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Review & accept'));
    await tester.tap(find.text('Review & accept')); await tester.pumpAndSettle();
    await tester.tap(find.text('Temporary access')); await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('1 hour'));
    final before = DateTime.now();
    await tester.tap(find.text('1 hour')); await tester.pumpAndSettle();
    await tester.tap(find.text('Accept & share (0)')); await tester.pumpAndSettle();
    final deadline = DateTime.parse(body!['accessExpiresAt'] as String);
    expect(deadline.isUtc, true);
    expect(deadline.isAfter(before.add(const Duration(minutes: 59))), true);
    expect(deadline.isBefore(DateTime.now().add(const Duration(minutes: 61))), true);
  });
}
