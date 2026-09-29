import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart' as http_testing;
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/assistant_service.dart';
import 'package:expatone_app/features/assistant/assistant_screen.dart';

const _mockConversationJson = {
  'id': '00000000-0000-0000-0000-000000000001',
  'title': null,
  'module': 'government-assistant',
  'createdAt': '2026-01-01T00:00:00Z',
  'updatedAt': '2026-01-01T00:00:00Z',
  'messages': <dynamic>[],
};

const _mockMessageJson = {
  'id': '00000000-0000-0000-0000-000000000002',
  'role': 'assistant',
  'content':
      'The minimum salary for Employment Pass Category II is RM10,000 - RM19,999.',
  'sources': [
    {
      'title': 'Employment Pass Salary Policy Effective 1 June 2026',
      'url':
          'https://esd.imi.gov.my/portal/latest-news/announcement/announcement-266-ep-salary-policy-2026/',
      'department': 'Immigration Department of Malaysia',
      'category': 'employment-pass',
      'countryCode': 'MY',
      'relevanceScore': 0.78,
    }
  ],
  'responseMode': 'official_grounded',
  'createdAt': '2026-01-01T00:00:01Z',
};

const _mockGeneralUnverifiedMessageJson = {
  'id': '00000000-0000-0000-0000-000000000003',
  'role': 'assistant',
  'content': 'Based on general knowledge, the Malaysian driving test involves...',
  'sources': null,
  'responseMode': 'general_unverified',
  'createdAt': '2026-01-01T00:00:01Z',
};

const _mockCasualMessageJson = {
  'id': '00000000-0000-0000-0000-000000000004',
  'role': 'assistant',
  'content': 'Hi! I can help you with Malaysian government requirements.',
  'sources': null,
  'responseMode': 'casual',
  'createdAt': '2026-01-01T00:00:01Z',
};

AssistantService _buildService({http.Client? httpClient}) {
  final mockClient = httpClient ??
      http_testing.MockClient((request) async {
        final path = request.url.path;

        if (path.endsWith('/conversations') && request.method == 'POST' &&
            !path.contains('/messages')) {
          return http.Response(
            jsonEncode(_mockConversationJson),
            200,
            headers: {'content-type': 'application/json'},
          );
        }

        if (path.endsWith('/messages') && request.method == 'POST') {
          return http.Response(
            jsonEncode(_mockMessageJson),
            200,
            headers: {'content-type': 'application/json'},
          );
        }

        return http.Response('Not found', 404);
      });

  final apiClient = ApiClient(httpClient: mockClient);
  return AssistantService(apiClient);
}

void main() {
  group('AssistantScreen', () {
    testWidgets('renders empty state with example questions', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: AssistantScreen(assistantService: _buildService()),
      ));

      expect(find.text('Government Assistant'), findsOneWidget);
      expect(find.textContaining('Ask about Malaysian'), findsOneWidget);
      expect(
        find.text(
            'What is the minimum salary for Employment Pass Category II?'),
        findsOneWidget,
      );
    });

    testWidgets('renders message input and send button', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: AssistantScreen(assistantService: _buildService()),
      ));

      expect(
        find.widgetWithText(TextField, 'Ask about government requirements...'),
        findsOneWidget,
      );
      expect(find.byIcon(Icons.send), findsOneWidget);
    });

    testWidgets('shows loading indicator after sending message',
        (tester) async {
      final messageCompleter = Completer<http.Response>();

      final service = _buildService(
        httpClient: http_testing.MockClient((request) async {
          final path = request.url.path;
          if (path.endsWith('/conversations') && request.method == 'POST' &&
              !path.contains('/messages')) {
            return http.Response(
              jsonEncode(_mockConversationJson),
              200,
              headers: {'content-type': 'application/json'},
            );
          }
          return messageCompleter.future;
        }),
      );

      await tester.pumpWidget(MaterialApp(
        home: AssistantScreen(assistantService: service),
      ));

      await tester.enterText(
        find.byType(TextField),
        'What is the minimum salary?',
      );
      await tester.tap(find.byIcon(Icons.send));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 100));

      expect(find.text('Searching official sources...'), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      messageCompleter.complete(http.Response(
        jsonEncode(_mockMessageJson),
        200,
        headers: {'content-type': 'application/json'},
      ));
      await tester.pumpAndSettle();
    });

    testWidgets('renders user message and assistant response with sources',
        (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: AssistantScreen(assistantService: _buildService()),
      ));

      await tester.enterText(
        find.byType(TextField),
        'What is the minimum salary for EP Category II?',
      );
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(
        find.text('What is the minimum salary for EP Category II?'),
        findsOneWidget,
      );

      expect(
        find.textContaining('RM10,000'),
        findsOneWidget,
      );

      expect(find.text('Sources'), findsOneWidget);
      expect(
        find.text('Employment Pass Salary Policy Effective 1 June 2026'),
        findsOneWidget,
      );
      expect(
        find.text('Immigration Department of Malaysia'),
        findsOneWidget,
      );
      expect(find.text('View official source'), findsOneWidget);
    });

    testWidgets('tapping example question sends it', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: AssistantScreen(assistantService: _buildService()),
      ));

      await tester.tap(find.text(
          'What is the minimum salary for Employment Pass Category II?'));
      await tester.pumpAndSettle();

      expect(find.textContaining('RM10,000'), findsOneWidget);
    });

    testWidgets('shows error banner on API failure', (tester) async {
      final failService = _buildService(
        httpClient: http_testing.MockClient((request) async {
          final path = request.url.path;
          if (path.endsWith('/conversations') && request.method == 'POST' &&
              !path.contains('/messages')) {
            return http.Response(
              jsonEncode(_mockConversationJson),
              200,
              headers: {'content-type': 'application/json'},
            );
          }
          return http.Response(
            jsonEncode({'message': 'Service unavailable'}),
            500,
            headers: {'content-type': 'application/json'},
          );
        }),
      );

      await tester.pumpWidget(MaterialApp(
        home: AssistantScreen(assistantService: failService),
      ));

      await tester.enterText(find.byType(TextField), 'Test question');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.error_outline), findsOneWidget);
    });

    testWidgets('shows clean message on HTTP 503 service unavailable',
        (tester) async {
      final unavailService = _buildService(
        httpClient: http_testing.MockClient((request) async {
          final path = request.url.path;
          if (path.endsWith('/conversations') &&
              request.method == 'POST' &&
              !path.contains('/messages')) {
            return http.Response(
              jsonEncode(_mockConversationJson),
              200,
              headers: {'content-type': 'application/json'},
            );
          }
          return http.Response(
            jsonEncode({
              'message':
                  'The Government Assistant is temporarily unavailable. Please try again in a moment.'
            }),
            503,
            headers: {'content-type': 'application/json'},
          );
        }),
      );

      await tester.pumpWidget(MaterialApp(
        home: AssistantScreen(assistantService: unavailService),
      ));

      await tester.enterText(find.byType(TextField), 'Test question');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.error_outline), findsOneWidget);
      expect(
        find.textContaining('temporarily unavailable'),
        findsOneWidget,
      );
    });

    testWidgets('shows clean message on timeout', (tester) async {
      final timeoutCompleter = Completer<http.Response>();

      final timeoutService = _buildService(
        httpClient: http_testing.MockClient((request) async {
          final path = request.url.path;
          if (path.endsWith('/conversations') &&
              request.method == 'POST' &&
              !path.contains('/messages')) {
            return http.Response(
              jsonEncode(_mockConversationJson),
              200,
              headers: {'content-type': 'application/json'},
            );
          }
          return timeoutCompleter.future;
        }),
      );

      await tester.pumpWidget(MaterialApp(
        home: AssistantScreen(assistantService: timeoutService),
      ));

      await tester.enterText(find.byType(TextField), 'Test question');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 100));

      expect(find.text('Searching official sources...'), findsOneWidget);

      timeoutCompleter.completeError(TimeoutException('Connection timed out'));
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.error_outline), findsOneWidget);
      expect(
        find.textContaining('temporarily unavailable'),
        findsOneWidget,
      );
    });

    testWidgets('new conversation button clears messages', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: AssistantScreen(assistantService: _buildService()),
      ));

      await tester.enterText(find.byType(TextField), 'Hello');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.add_comment_outlined), findsOneWidget);
      await tester.tap(find.byIcon(Icons.add_comment_outlined));
      await tester.pumpAndSettle();

      expect(find.textContaining('Ask about Malaysian'), findsOneWidget);
    });

    // ---- Response mode rendering tests ----

    testWidgets('official_grounded: shows source cards, no unverified banner',
        (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: AssistantScreen(assistantService: _buildService()),
      ));

      await tester.enterText(find.byType(TextField), 'EP salary question');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(find.text('Sources'), findsOneWidget);
      expect(find.text('View official source'), findsOneWidget);
      expect(find.textContaining('General information'), findsNothing);
    });

    testWidgets('general_unverified: shows unverified banner, no source cards',
        (tester) async {
      final service = _buildService(
        httpClient: http_testing.MockClient((request) async {
          final path = request.url.path;
          if (path.endsWith('/conversations') && request.method == 'POST' &&
              !path.contains('/messages')) {
            return http.Response(jsonEncode(_mockConversationJson), 200,
                headers: {'content-type': 'application/json'});
          }
          if (path.endsWith('/messages') && request.method == 'POST') {
            return http.Response(jsonEncode(_mockGeneralUnverifiedMessageJson),
                200, headers: {'content-type': 'application/json'});
          }
          return http.Response('Not found', 404);
        }),
      );

      await tester.pumpWidget(MaterialApp(
        home: AssistantScreen(assistantService: service),
      ));

      await tester.enterText(find.byType(TextField), 'General question');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.info_outline), findsOneWidget);
      expect(find.textContaining('General information'), findsOneWidget);
      expect(find.text('Sources'), findsNothing);
      expect(find.text('View official source'), findsNothing);
    });

    testWidgets('casual: no unverified banner, no source cards', (tester) async {
      final service = _buildService(
        httpClient: http_testing.MockClient((request) async {
          final path = request.url.path;
          if (path.endsWith('/conversations') && request.method == 'POST' &&
              !path.contains('/messages')) {
            return http.Response(jsonEncode(_mockConversationJson), 200,
                headers: {'content-type': 'application/json'});
          }
          if (path.endsWith('/messages') && request.method == 'POST') {
            return http.Response(jsonEncode(_mockCasualMessageJson), 200,
                headers: {'content-type': 'application/json'});
          }
          return http.Response('Not found', 404);
        }),
      );

      await tester.pumpWidget(MaterialApp(
        home: AssistantScreen(assistantService: service),
      ));

      await tester.enterText(find.byType(TextField), 'hello');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(find.textContaining('General information'), findsNothing);
      expect(find.text('Sources'), findsNothing);
    });
  });

  group('AssistantMessage model', () {
    test('fromJson parses responseMode correctly', () {
      final msg = AssistantMessage.fromJson(
          Map<String, dynamic>.from(_mockGeneralUnverifiedMessageJson));
      expect(msg.responseMode, 'general_unverified');
      expect(msg.isGeneralUnverified, isTrue);
    });

    test('fromJson treats missing responseMode as null', () {
      final json = Map<String, dynamic>.from(_mockMessageJson)
        ..remove('responseMode');
      final msg = AssistantMessage.fromJson(json);
      expect(msg.responseMode, isNull);
      expect(msg.isGeneralUnverified, isFalse);
    });

    test('official_grounded is not isGeneralUnverified', () {
      final msg = AssistantMessage.fromJson(
          Map<String, dynamic>.from(_mockMessageJson));
      expect(msg.responseMode, 'official_grounded');
      expect(msg.isGeneralUnverified, isFalse);
    });

    test('casual is not isGeneralUnverified', () {
      final msg = AssistantMessage.fromJson(
          Map<String, dynamic>.from(_mockCasualMessageJson));
      expect(msg.responseMode, 'casual');
      expect(msg.isGeneralUnverified, isFalse);
    });
  });
}
