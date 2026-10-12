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
  test('Selected own and shared IDs are sent without document content', () async {
    final api = ApiClient(httpClient: MockClient((request) async {
      expect(request.url.path.endsWith('/visa-applications'), isTrue);
      final body = jsonDecode(request.body) as Map;
      expect(body['documents'], [{'id': 'owned', 'role': 'Passport'}, {'id': 'shared', 'role': 'Itinerary'}]);
      expect(body.containsKey('content'), isFalse);
      return http.Response('{"id":"draft"}', 200);
    }));
    final service = VisaService(api);
    await service.create('SG-VISIT-2026-10-12', [{'id': 'owned', 'role': 'Passport'}, {'id': 'shared', 'role': 'Itinerary'}]);
  });
  testWidgets('Revoked draft never renders saved personal values', (tester) async {
    final api = ApiClient(httpClient: MockClient((_) async => http.Response(jsonEncode({'id':'draft', 'revision':'rev', 'accessBlocked':true, 'message':'Shared-document access expired.'}), 200)));
    await tester.pumpWidget(MaterialApp(home: VisaDraftScreen(documentService: DocumentService(api), id:'draft', rules:{'fields':[], 'checks':[]})));
    await tester.pumpAndSettle();
    expect(find.text('Shared-document access expired.'), findsOneWidget);
    expect(find.text('Reset links and clear old form'), findsOneWidget);
    expect(find.text('Download Form 14A'), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });
  testWidgets('Planned destination cannot create a draft', (tester) async {
    final api = ApiClient(httpClient: MockClient((r) async {
      if(r.url.path.endsWith('/catalog')) {
        return http.Response(jsonEncode({'countries':[{'code':'SG','name':'Singapore','enabled':false}], 'templates':[]}),200);
      }
      return http.Response('[]',200);
    }));
    await tester.pumpWidget(MaterialApp(home: ApplicationAssistantScreen(documentService: DocumentService(api))));
    await tester.pumpAndSettle();
    final button = tester.widget<FilledButton>(find.widgetWithText(FilledButton, 'Create draft'));
    expect(button.onPressed,isNull);
  });
}
