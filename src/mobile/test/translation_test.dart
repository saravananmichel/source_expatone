import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart' as http_testing;
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/translation_service.dart';
import 'package:expatone_app/features/translator/translation_screen.dart';
import 'package:expatone_app/services/speech_service.dart';
import 'package:expatone_app/services/tts_service.dart';

// ── Mock speech service ──────────────────────────────────────────────────────

class MockSpeechService implements ISpeechService {
  bool _listening = false;
  final bool _available;
  void Function(String)? _onResult;
  void Function()? _onDone;

  MockSpeechService({bool available = true}) : _available = available; // ignore: prefer_initializing_formals

  @override
  bool get isListening => _listening;

  @override
  bool get isAvailable => _available;

  @override
  Future<bool> initialize() async => _available;

  @override
  Future<bool> startListening({
    required String languageCode,
    required void Function(String text) onResult,
    required void Function() onDone,
  }) async {
    if (!_available) return false;
    _listening = true;
    _onResult = onResult;
    _onDone = onDone;
    return true;
  }

  @override
  Future<void> stopListening() async {
    _listening = false;
    _onDone?.call();
  }

  @override
  void dispose() {}

  // Test helper — simulate speech result
  void simulateResult(String text) => _onResult?.call(text);

  // Test helper — simulate recognition ended
  void simulateDone() {
    _listening = false;
    _onDone?.call();
  }
}

// ── Mock TTS service ─────────────────────────────────────────────────────────

class MockTtsService implements ITtsService {
  bool _speaking = false;
  bool shouldFail = false;
  String? lastSpokenText;
  String? lastLanguage;
  void Function()? _onStopped;

  @override
  bool get isSpeaking => _speaking;

  @override
  set onStopped(void Function()? callback) => _onStopped = callback;

  @override
  Future<void> speak(String text, String languageCode) async {
    if (shouldFail) {
      _onStopped?.call();
      return;
    }
    _speaking = true;
    lastSpokenText = text;
    lastLanguage = languageCode;
    // Does NOT call onStopped — speaking stays active until stop() is called.
  }

  @override
  Future<void> stop() async {
    _speaking = false;
    _onStopped?.call();
  }

  @override
  void dispose() {}
}

// ── Helpers ──────────────────────────────────────────────────────────────────

const _mockTranslationResponse = {
  'translatedText': 'Selamat pagi',
  'sourceLanguage': 'en',
  'targetLanguage': 'ms',
};

TranslationService _buildService({http.Client? httpClient}) {
  final mockClient = httpClient ??
      http_testing.MockClient((request) async {
        final path = request.url.path;

        if (path.endsWith('/translate') && request.method == 'POST') {
          return http.Response(
            jsonEncode(_mockTranslationResponse),
            200,
            headers: {'content-type': 'application/json'},
          );
        }

        if (path.endsWith('/languages') && request.method == 'GET') {
          return http.Response(
            jsonEncode([
              {'code': 'en', 'name': 'English'},
              {'code': 'ms', 'name': 'Malay'},
              {'code': 'zh', 'name': 'Chinese (Simplified)'},
            ]),
            200,
            headers: {'content-type': 'application/json'},
          );
        }

        return http.Response('Not found', 404);
      });

  final apiClient = ApiClient(httpClient: mockClient);
  return TranslationService(apiClient);
}

Widget _buildScreen({
  TranslationService? translationService,
  ISpeechService? speechService,
  ITtsService? ttsService,
}) {
  return MaterialApp(
    home: TranslationScreen(
      translationService: translationService ?? _buildService(),
      speechService: speechService ?? MockSpeechService(),
      ttsService: ttsService ?? MockTtsService(),
    ),
  );
}

// ─────────────────────────────────────────────────────────────────────────────

void main() {
  group('TranslationScreen', () {
    // ── Original 11 tests (preserved unchanged) ────────────────────────────

    testWidgets('renders initial state with language selectors and input',
        (tester) async {
      await tester.pumpWidget(_buildScreen());

      expect(find.text('Auto-detect'), findsOneWidget);
      expect(find.text('Malay'), findsOneWidget);
      expect(
        find.widgetWithText(TextField, 'Enter text to translate...'),
        findsOneWidget,
      );
      expect(find.text('0 / 5000'), findsOneWidget);
      expect(find.widgetWithText(ElevatedButton, 'Translate'), findsOneWidget);
    });

    testWidgets('swap button is disabled when source is auto-detect',
        (tester) async {
      await tester.pumpWidget(_buildScreen());

      final iconButton = tester.widget<IconButton>(
        find.widgetWithIcon(IconButton, Icons.swap_horiz),
      );
      expect(iconButton.onPressed, isNull);
    });

    testWidgets('translate button is disabled when input is empty',
        (tester) async {
      await tester.pumpWidget(_buildScreen());

      final button = tester.widget<ElevatedButton>(
        find.widgetWithText(ElevatedButton, 'Translate'),
      );
      expect(button.onPressed, isNull);
    });

    testWidgets('translate button is enabled when input has text',
        (tester) async {
      await tester.pumpWidget(_buildScreen());

      await tester.enterText(find.byType(TextField), 'Good morning');
      await tester.pump();

      final button = tester.widget<ElevatedButton>(
        find.widgetWithText(ElevatedButton, 'Translate'),
      );
      expect(button.onPressed, isNotNull);
    });

    testWidgets('shows translation result after successful translate',
        (tester) async {
      await tester.pumpWidget(_buildScreen());

      await tester.enterText(find.byType(TextField), 'Good morning');
      await tester.pump();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Translate'));
      await tester.pumpAndSettle();

      expect(find.text('Translation'), findsOneWidget);
      expect(find.text('Selamat pagi'), findsOneWidget);
    });

    testWidgets('shows error on API failure', (tester) async {
      final failService = _buildService(
        httpClient: http_testing.MockClient((request) async {
          return http.Response(
            jsonEncode({'message': 'Service unavailable'}),
            503,
            headers: {'content-type': 'application/json'},
          );
        }),
      );

      await tester.pumpWidget(_buildScreen(translationService: failService));

      await tester.enterText(find.byType(TextField), 'Hello');
      await tester.pump();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Translate'));
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.error_outline), findsOneWidget);
      expect(find.textContaining('temporarily unavailable'), findsOneWidget);
    });

    testWidgets('shows general error on non-503 failure', (tester) async {
      final failService = _buildService(
        httpClient: http_testing.MockClient((request) async {
          return http.Response(
            jsonEncode({'message': 'Bad request'}),
            500,
            headers: {'content-type': 'application/json'},
          );
        }),
      );

      await tester.pumpWidget(_buildScreen(translationService: failService));

      await tester.enterText(find.byType(TextField), 'Hello');
      await tester.pump();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Translate'));
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.error_outline), findsOneWidget);
    });

    testWidgets('shows loading indicator while translating', (tester) async {
      final completer = Completer<http.Response>();

      final slowService = _buildService(
        httpClient: http_testing.MockClient((request) async {
          return completer.future;
        }),
      );

      await tester.pumpWidget(_buildScreen(translationService: slowService));

      await tester.enterText(find.byType(TextField), 'Hello');
      await tester.pump();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Translate'));
      await tester.pump();

      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      completer.complete(http.Response(
        jsonEncode(_mockTranslationResponse),
        200,
        headers: {'content-type': 'application/json'},
      ));
      await tester.pumpAndSettle();
    });

    testWidgets('copy button triggers clipboard copy', (tester) async {
      final List<MethodCall> clipboardLog = [];
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(SystemChannels.platform,
              (MethodCall methodCall) async {
        clipboardLog.add(methodCall);
        return null;
      });

      await tester.pumpWidget(_buildScreen());

      await tester.enterText(find.byType(TextField), 'Good morning');
      await tester.pump();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Translate'));
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.copy));
      await tester.pumpAndSettle();

      expect(
        clipboardLog.any((c) => c.method == 'Clipboard.setData'),
        isTrue,
      );
      expect(find.text('Translation copied to clipboard'), findsOneWidget);

      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(SystemChannels.platform, null);
    });

    testWidgets('character count updates as user types', (tester) async {
      await tester.pumpWidget(_buildScreen());

      expect(find.text('0 / 5000'), findsOneWidget);

      await tester.enterText(find.byType(TextField), 'Hello');
      await tester.pump();

      expect(find.text('5 / 5000'), findsOneWidget);
    });

    testWidgets('clear button resets state', (tester) async {
      await tester.pumpWidget(_buildScreen());

      await tester.enterText(find.byType(TextField), 'Good morning');
      await tester.pump();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Translate'));
      await tester.pumpAndSettle();

      expect(find.text('Selamat pagi'), findsOneWidget);

      await tester.tap(find.byIcon(Icons.clear));
      await tester.pump();

      expect(find.text('Selamat pagi'), findsNothing);
      expect(find.text('0 / 5000'), findsOneWidget);
    });

    // ── New voice tests ───────────────────────────────────────────────────

    testWidgets('1. renders microphone button in input card', (tester) async {
      await tester.pumpWidget(_buildScreen());

      expect(find.byIcon(Icons.mic_none), findsOneWidget);
    });

    testWidgets('2. renders speaker button when translation result exists',
        (tester) async {
      await tester.pumpWidget(_buildScreen());

      // No result yet — speaker should not be visible
      expect(find.byIcon(Icons.volume_up_outlined), findsNothing);

      await tester.enterText(find.byType(TextField), 'Good morning');
      await tester.pump();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Translate'));
      await tester.pumpAndSettle();

      expect(
        find.byIcon(Icons.volume_up_outlined).evaluate().isNotEmpty ||
            find.byIcon(Icons.volume_up).evaluate().isNotEmpty,
        isTrue,
      );
    });

    testWidgets('3. microphone permission denied does not crash', (tester) async {
      final unavailableSpeech = MockSpeechService(available: false);
      await tester.pumpWidget(
          _buildScreen(speechService: unavailableSpeech));

      await tester.tap(find.byIcon(Icons.mic_none));
      await tester.pumpAndSettle();

      // App still running; snackbar shown
      expect(find.byType(SnackBar), findsOneWidget);
    });

    testWidgets('4. speech error (start fails) does not crash', (tester) async {
      final brokenSpeech = _BrokenStartSpeechService();
      await tester.pumpWidget(_buildScreen(speechService: brokenSpeech));

      await tester.tap(find.byIcon(Icons.mic_none));
      await tester.pumpAndSettle();

      // App still running; snackbar shown with error
      expect(find.byType(SnackBar), findsOneWidget);
    });

    testWidgets('5. listening state is shown while recording', (tester) async {
      final speech = MockSpeechService();
      await tester.pumpWidget(_buildScreen(speechService: speech));

      await tester.tap(find.byIcon(Icons.mic_none));
      await tester.pump();

      expect(find.text('Listening...'), findsOneWidget);
      expect(find.byIcon(Icons.mic), findsOneWidget);
    });

    testWidgets('6. speaker button disabled when no translation', (tester) async {
      await tester.pumpWidget(_buildScreen());

      // No result → no speaker button rendered
      expect(find.byIcon(Icons.volume_up_outlined), findsNothing);
      expect(find.byIcon(Icons.volume_up), findsNothing);
    });

    testWidgets('7. speaker button shows active state while speaking',
        (tester) async {
      final tts = MockTtsService();
      await tester.pumpWidget(_buildScreen(ttsService: tts));

      await tester.enterText(find.byType(TextField), 'Good morning');
      await tester.pump();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Translate'));
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.volume_up_outlined));
      await tester.pump();

      // After tapping speak, isSpeaking=true → icon switches to filled volume_up
      expect(find.byIcon(Icons.volume_up), findsWidgets);
    });

    testWidgets('8. TTS failure is handled gracefully', (tester) async {
      final failingTts = MockTtsService()..shouldFail = true;
      await tester.pumpWidget(_buildScreen(ttsService: failingTts));

      await tester.enterText(find.byType(TextField), 'Good morning');
      await tester.pump();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Translate'));
      await tester.pumpAndSettle();

      // Should not crash even when TTS fails
      await tester.tap(find.byIcon(Icons.volume_up_outlined));
      await tester.pumpAndSettle();

      expect(find.byType(TranslationScreen), findsOneWidget);
    });

    testWidgets('9. existing copy translation functionality still works',
        (tester) async {
      final List<MethodCall> clipboardLog = [];
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(SystemChannels.platform,
              (MethodCall methodCall) async {
        clipboardLog.add(methodCall);
        return null;
      });

      await tester.pumpWidget(_buildScreen());

      await tester.enterText(find.byType(TextField), 'Good morning');
      await tester.pump();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Translate'));
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.copy));
      await tester.pumpAndSettle();

      expect(
        clipboardLog.any((c) => c.method == 'Clipboard.setData'),
        isTrue,
      );

      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(SystemChannels.platform, null);
    });

    testWidgets('10. existing translation functionality still works',
        (tester) async {
      await tester.pumpWidget(_buildScreen());

      await tester.enterText(find.byType(TextField), 'Good morning');
      await tester.pump();
      await tester.tap(find.widgetWithText(ElevatedButton, 'Translate'));
      await tester.pumpAndSettle();

      expect(find.text('Selamat pagi'), findsOneWidget);
    });

    testWidgets('11. existing language selector still works', (tester) async {
      await tester.pumpWidget(_buildScreen());

      // Swap is disabled on auto-detect
      final swapBtn = tester.widget<IconButton>(
        find.widgetWithIcon(IconButton, Icons.swap_horiz),
      );
      expect(swapBtn.onPressed, isNull);

      // Dropdowns exist
      expect(find.text('Auto-detect'), findsOneWidget);
      expect(find.text('Malay'), findsOneWidget);
    });

    testWidgets('12. existing 5000-character validation still works',
        (tester) async {
      await tester.pumpWidget(_buildScreen());

      expect(find.text('0 / 5000'), findsOneWidget);

      await tester.enterText(find.byType(TextField), 'Hi');
      await tester.pump();

      expect(find.text('2 / 5000'), findsOneWidget);
    });
  });
}

// ── Helper for test 4 ─────────────────────────────────────────────────────────

class _BrokenStartSpeechService implements ISpeechService {
  @override
  bool get isListening => false;
  @override
  bool get isAvailable => true;
  @override
  Future<bool> initialize() async => true;
  @override
  Future<bool> startListening({
    required String languageCode,
    required void Function(String text) onResult,
    required void Function() onDone,
  }) async =>
      false; // simulates a failed start
  @override
  Future<void> stopListening() async {}
  @override
  void dispose() {}
}
