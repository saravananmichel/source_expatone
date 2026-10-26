import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/document_service.dart';
import 'package:expatone_app/core/services/visa_service.dart';
import 'package:expatone_app/features/applications/application_assistant_screen.dart';

void main() {
  test(
    'Selected own and shared IDs are sent without document content',
    () async {
      final api = ApiClient(
        httpClient: MockClient((request) async {
          expect(request.url.path.endsWith('/visa-applications'), isTrue);
          final body = jsonDecode(request.body) as Map;
          expect(body['documents'], [
            {'id': 'owned', 'role': 'Passport'},
            {'id': 'shared', 'role': 'Itinerary'},
          ]);
          expect(body.containsKey('content'), isFalse);
          return http.Response('{"id":"draft"}', 200);
        }),
      );
      final service = VisaService(api);
      await service.create('SG-VISIT-2026-10-12', [
        {'id': 'owned', 'role': 'Passport'},
        {'id': 'shared', 'role': 'Itinerary'},
      ]);
    },
  );
  testWidgets('Revoked draft never renders saved personal values', (
    tester,
  ) async {
    final api = ApiClient(
      httpClient: MockClient(
        (_) async => http.Response(
          jsonEncode({
            'id': 'draft',
            'revision': 'rev',
            'accessBlocked': true,
            'message': 'Shared-document access expired.',
          }),
          200,
        ),
      ),
    );
    await tester.pumpWidget(
      MaterialApp(
        home: VisaDraftScreen(
          documentService: DocumentService(api),
          id: 'draft',
          rules: {'fields': [], 'checks': []},
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Shared-document access expired.'), findsOneWidget);
    expect(find.text('Reset links and clear old form'), findsOneWidget);
    expect(find.text('Download Form 14A'), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });
  testWidgets(
    'Non-Singapore draft uses its title and never offers Singapore PDFs',
    (tester) async {
      final api = ApiClient(
        httpClient: MockClient(
          (_) async => http.Response(
            jsonEncode({
              'id': 'draft',
              'revision': 'rev',
              'accessBlocked': false,
              'status': 'Draft',
              'values': {},
              'checks': {},
              'documents': [],
              'assessment': {
                'blockers': [],
                'travelNote': 'Country-specific travel guidance',
              },
            }),
            200,
          ),
        ),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: VisaDraftScreen(
            documentService: DocumentService(api),
            id: 'draft',
            rules: {
              'id': 'MY-EMPLOYMENT-2026-10-26',
              'title': 'Malaysia employment preparation',
              'fields': [],
              'checks': [],
              'sources': [],
              'readinessScope': 'Preparation only',
              'guidance': 'Employer confirms the applicable route',
              'reviewedOn': '2026-10-26',
              'reviewUntil': '2027-01-24',
            },
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Malaysia employment preparation'), findsOneWidget);
      expect(find.text('Download Form 14A'), findsNothing);
      expect(find.text('Download V39A'), findsNothing);
      await tester.pumpWidget(const SizedBox());
    },
  );
  testWidgets('Disabled destination with an existing template cannot create', (
    tester,
  ) async {
    final api = ApiClient(
      httpClient: MockClient(
        (r) async => http.Response(
          jsonEncode(
            r.url.path.endsWith('/catalog')
                ? {
                    'countries': [
                      {'code': 'SG', 'name': 'Singapore', 'enabled': false},
                    ],
                    'templates': [
                      {
                        'id': 'SG-VISIT-2026-10-12',
                        'countryCode': 'SG',
                        'purpose': 'Tourism',
                      },
                    ],
                  }
                : [],
          ),
          200,
        ),
      ),
    );
    await tester.pumpWidget(
      MaterialApp(
        home: ApplicationAssistantScreen(documentService: DocumentService(api)),
      ),
    );
    await tester.pumpAndSettle();
    expect(
      tester
          .widget<FilledButton>(
            find.widgetWithText(FilledButton, 'Create draft'),
          )
          .onPressed,
      isNull,
    );
  });
  testWidgets('Planned destination cannot create a draft', (tester) async {
    final api = ApiClient(
      httpClient: MockClient((r) async {
        if (r.url.path.endsWith('/catalog')) {
          return http.Response(
            jsonEncode({
              'countries': [
                {'code': 'SG', 'name': 'Singapore', 'enabled': false},
              ],
              'templates': [],
            }),
            200,
          );
        }
        return http.Response('[]', 200);
      }),
    );
    await tester.pumpWidget(
      MaterialApp(
        home: ApplicationAssistantScreen(documentService: DocumentService(api)),
      ),
    );
    await tester.pumpAndSettle();
    final button = tester.widget<FilledButton>(
      find.widgetWithText(FilledButton, 'Create draft'),
    );
    expect(button.onPressed, isNull);
  });
}
