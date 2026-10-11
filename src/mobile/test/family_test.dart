import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/document_service.dart';
import 'package:expatone_app/features/family/family_screen.dart';
import 'package:expatone_app/features/documents/document_detail_screen.dart';

void main() {
  testWidgets('Recipient can select all, deselect one, and submit only the selected owned documents', (tester) async {
    Map<String, dynamic>? decision;
    final client = MockClient((request) async {
      if (request.method == 'POST') {
        decision = jsonDecode(request.body) as Map<String, dynamic>;
        return http.Response('{}', 200);
      }
      if (request.url.path.endsWith('/family/requests')) {
        return http.Response(jsonEncode([{
          'id': 'request', 'revision': 'revision', 'status': 'Pending', 'isIncoming': true,
          'requester': {'id': 'a', 'email': 'a@test.com'},
          'recipient': {'id': 'b', 'email': 'b@test.com'}, 'documentIds': [],
        }]), 200);
      }
      if (request.url.path.endsWith('/documents')) {
        return http.Response(jsonEncode([
          for (final id in ['Passport', 'Visa', 'Other family file']) {
            'id': id, 'name': id, 'documentType': 'PDF', 'documentTypeId': 'type',
            'createdAt': '2026-10-11T00:00:00Z', 'isSharedWithMe': id == 'Other family file',
          },
        ]), 200);
      }
      return http.Response('{}', 404);
    });
    final service = DocumentService(ApiClient(httpClient: client));
    await tester.pumpWidget(MaterialApp(home: FamilyScreen(documentService: service)));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Review & accept'));
    await tester.pumpAndSettle();
    expect(find.text('Other family file'), findsNothing);
    await tester.tap(find.text('Select all current documents'));
    await tester.pumpAndSettle();
    expect(find.text('Accept & share (2)'), findsOneWidget);
    await tester.tap(find.text('Visa'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Accept & share (1)'));
    await tester.pumpAndSettle();
    expect(decision!['accept'], true);
    expect(decision!['documentIds'], ['Passport']);
    expect(decision!['revision'], 'revision');
  });

  testWidgets('Shared document detail has no owner actions or private data requests', (tester) async {
    var calls = 0;
    final service = DocumentService(ApiClient(httpClient: MockClient((_) async {
      calls++; return http.Response('{}', 403);
    })));
    await tester.pumpWidget(MaterialApp(home: DocumentDetailScreen(documentService: service,
      document: DocumentItem(id: 'd', name: 'Family passport', documentType: 'Passport',
        documentTypeId: 't', fileSizeBytes: 100, status: 'Active', createdAt: DateTime(2026),
        isSharedWithMe: true, ownerEmail: 'owner@test.com'))));
    await tester.pumpAndSettle();
    expect(find.text('Read-only family document'), findsOneWidget);
    expect(find.text('View document'), findsOneWidget);
    expect(find.byIcon(Icons.delete_outlined), findsNothing);
    expect(calls, 0);
  });
}
