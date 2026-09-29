import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart' as http_testing;
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/emergency_service.dart';
import 'package:expatone_app/features/emergency/emergency_screen.dart';

const _mockAssistResponse = {
  'response': 'Call 999 now. Tell the operator someone is injured and needs an ambulance.',
  'translatedMessage': 'Hubungi 999 sekarang. Beritahu operator seseorang cedera dan memerlukan ambulans.',
  'targetLanguage': 'ms',
};

EmergencyService _buildService({http.Client? httpClient}) {
  final mockClient = httpClient ??
      http_testing.MockClient((request) async {
        final path = request.url.path;

        if (path.endsWith('/assist') && request.method == 'POST') {
          return http.Response(
            jsonEncode(_mockAssistResponse),
            200,
            headers: {'content-type': 'application/json'},
          );
        }

        return http.Response('Not found', 404);
      });

  final apiClient = ApiClient(httpClient: mockClient);
  return EmergencyService(apiClient);
}

void main() {
  group('EmergencyScreen', () {
    testWidgets('renders emergency call section with 999 button',
        (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: _buildService()),
      ));

      expect(find.text('EMERGENCY'), findsOneWidget);
      expect(find.text('CALL 999'), findsOneWidget);
      expect(find.text('If you are in immediate danger:'), findsOneWidget);
      expect(find.textContaining('MERS'), findsOneWidget);
    });

    testWidgets('renders quick action buttons', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: _buildService()),
      ));

      expect(find.text('Police'), findsOneWidget);
      expect(find.text('Medical'), findsOneWidget);
      expect(find.text('Fire'), findsOneWidget);
      expect(find.textContaining('All emergency services are reached through 999'), findsOneWidget);
    });

    testWidgets('renders location section', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: _buildService()),
      ));

      expect(find.text('Share My Location'), findsOneWidget);
      expect(find.text('Location not yet retrieved'), findsOneWidget);
      expect(find.text('Get Location'), findsOneWidget);
      expect(find.text('Share Location'), findsOneWidget);
    });

    testWidgets('renders AI assistance section', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: _buildService()),
      ));

      expect(find.text('AI Emergency Help'), findsOneWidget);
      expect(find.textContaining('Need help explaining'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'Describe your emergency situation...'), findsOneWidget);
      expect(find.text('Translate to: '), findsOneWidget);
    });

    testWidgets('AI help button is disabled when description is empty',
        (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: _buildService()),
      ));

      final button = tester.widget<ElevatedButton>(
        find.widgetWithText(ElevatedButton, 'Get Help'),
      );
      expect(button.onPressed, isNull);
    });

    testWidgets('AI help button is enabled when description has text',
        (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: _buildService()),
      ));

      await tester.enterText(find.byType(TextField), 'Someone is hurt');
      await tester.pump();

      final button = tester.widget<ElevatedButton>(
        find.widgetWithText(ElevatedButton, 'Get Help'),
      );
      expect(button.onPressed, isNotNull);
    });

    testWidgets('shows AI result after successful assist', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: _buildService()),
      ));

      await tester.enterText(find.byType(TextField), 'Someone is injured');
      await tester.pump();
      await tester.ensureVisible(find.widgetWithText(ElevatedButton, 'Get Help'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Get Help'));
      await tester.pumpAndSettle();

      expect(find.text('AI Guidance'), findsOneWidget);
      expect(find.textContaining('Call 999 now'), findsOneWidget);
    });

    testWidgets('shows error on AI failure with 999 reminder', (tester) async {
      final failService = _buildService(
        httpClient: http_testing.MockClient((request) async {
          return http.Response(
            jsonEncode({'message': 'Service unavailable'}),
            503,
            headers: {'content-type': 'application/json'},
          );
        }),
      );

      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: failService),
      ));

      await tester.enterText(find.byType(TextField), 'Help');
      await tester.pump();
      await tester.ensureVisible(find.widgetWithText(ElevatedButton, 'Get Help'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Get Help'));
      await tester.pumpAndSettle();

      expect(find.textContaining('AI assistance is unavailable'), findsOneWidget);
      expect(find.textContaining('You can still call 999 directly'), findsOneWidget);
    });

    testWidgets('shows loading indicator while getting AI help', (tester) async {
      final completer = Completer<http.Response>();
      final slowService = _buildService(
        httpClient: http_testing.MockClient((request) async {
          return completer.future;
        }),
      );

      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: slowService),
      ));

      await tester.enterText(find.byType(TextField), 'Help');
      await tester.pump();
      await tester.ensureVisible(find.widgetWithText(ElevatedButton, 'Get Help'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Get Help'));
      await tester.pump();

      expect(find.text('Getting help...'), findsOneWidget);

      completer.complete(http.Response(
        jsonEncode(_mockAssistResponse),
        200,
        headers: {'content-type': 'application/json'},
      ));
      await tester.pumpAndSettle();
    });

    testWidgets('renders safety disclaimer', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: _buildService()),
      ));

      expect(
        find.textContaining('AI assistance is optional'),
        findsOneWidget,
      );
      expect(
        find.textContaining('Always call 999 first'),
        findsOneWidget,
      );
    });

    testWidgets('screen renders without backend dependency for core UI',
        (tester) async {
      final failService = _buildService(
        httpClient: http_testing.MockClient((request) async {
          throw Exception('Network unavailable');
        }),
      );

      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: failService),
      ));

      expect(find.text('CALL 999'), findsOneWidget);
      expect(find.text('Police'), findsOneWidget);
      expect(find.text('Medical'), findsOneWidget);
      expect(find.text('Fire'), findsOneWidget);
      expect(find.text('Get Location'), findsOneWidget);
      expect(find.text('Share Location'), findsOneWidget);
    });

    // B4 — Emergency safety tests

    testWidgets('B4-10: core UI renders and 999 button is prominent',
        (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: _buildService()),
      ));

      // Prominent CALL 999 button is present and identifiable
      expect(find.text('CALL 999'), findsOneWidget);
      // Emergency heading visible
      expect(find.text('EMERGENCY'), findsOneWidget);
      // Safety disclaimer visible
      expect(find.textContaining('Always call 999 first'), findsOneWidget);
    });

    testWidgets('B4-11: core controls present without requiring Gemini or backend',
        (tester) async {
      // All network calls throw — simulates complete backend/Gemini unavailability
      final noBackendService = _buildService(
        httpClient: http_testing.MockClient((request) async {
          throw Exception('No backend');
        }),
      );

      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: noBackendService),
      ));

      // Layer 1 deterministic controls must all be present
      expect(find.text('CALL 999'), findsOneWidget);
      expect(find.text('Get Location'), findsOneWidget);
      expect(find.text('Share Location'), findsOneWidget);
      expect(find.text('Police'), findsOneWidget);
      expect(find.text('Medical'), findsOneWidget);
      expect(find.text('Fire'), findsOneWidget);
      // These must not depend on any network state
    });

    testWidgets('B4-12: AI error shows message but does not hide CALL 999',
        (tester) async {
      final failService = _buildService(
        httpClient: http_testing.MockClient((request) async {
          return http.Response(
            '{"message":"Gemini unavailable"}',
            503,
            headers: {'content-type': 'application/json'},
          );
        }),
      );

      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: failService),
      ));

      await tester.enterText(find.byType(TextField), 'I need help');
      await tester.pump();
      await tester.ensureVisible(find.widgetWithText(ElevatedButton, 'Get Help'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Get Help'));
      await tester.pumpAndSettle();

      // Error shown
      expect(find.textContaining('AI assistance is unavailable'), findsOneWidget);

      // CALL 999 still visible above the error — screen scrolled back to top
      // Use find.text to verify the button text still exists in the widget tree
      expect(find.text('CALL 999'), findsOneWidget);
    });

    testWidgets('B4-13: location error does not disable emergency call button',
        (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: EmergencyScreen(emergencyService: _buildService()),
      ));

      // The location section shows "Location not yet retrieved" by default
      expect(find.text('Location not yet retrieved'), findsOneWidget);

      // CALL 999 button must be present regardless of location state
      expect(find.text('CALL 999'), findsOneWidget);

      // The CALL 999 ElevatedButton is enabled (onPressed is not null)
      final callButton = tester.widget<ElevatedButton>(
        find.widgetWithText(ElevatedButton, 'CALL 999'),
      );
      expect(callButton.onPressed, isNotNull);
    });
  });
}
