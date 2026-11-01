import 'dart:convert';

import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/document_service.dart';
import 'package:expatone_app/features/documents/documents_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  testWidgets('expanded direct and family shares lay out their document rows', (
    tester,
  ) async {
    final client = MockClient((request) async {
      final path = request.url.path;
      final rows = path.endsWith('/documents/shared-with-me')
          ? [
              {
                'shareId': 's',
                'documentId': 'd',
                'documentName': 'Direct letter',
                'documentType': 'Letter',
                'ownerEmail': 'owner@example.com',
                'sharedAt': '2026-10-01',
              },
            ]
          : path.endsWith('/family/dashboard')
          ? [
              {
                'id': 'f',
                'name': 'Family passport',
                'documentType': 'Passport',
                'expiryStatus': 'Valid',
                'fingerprint': 'abc',
                'isOwned': false,
                'expiryConfirmed': true,
                'remindersAllowed': false,
                'owner': {'id': 'o', 'email': 'family@example.com'},
              },
            ]
          : [];
      return http.Response(
        jsonEncode(rows),
        200,
        headers: {'content-type': 'application/json'},
      );
    });
    await tester.pumpWidget(
      MaterialApp(
        home: DocumentsScreen(
          documentService: DocumentService(ApiClient(httpClient: client)),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('Shared With Me'));
    await tester.pumpAndSettle();
    expect(find.text('Direct letter'), findsOneWidget);
    await tester.tap(find.text('family@example.com (1)'));
    await tester.pumpAndSettle();
    expect(find.text('Family passport'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
