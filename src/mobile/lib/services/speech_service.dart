import 'package:speech_to_text/speech_to_text.dart';

// Maps translation language codes to BCP-47 locales for speech recognition.
const Map<String, String> speechLocaleMap = {
  'en': 'en_US',
  'ms': 'ms_MY',
  'zh': 'zh_CN',
  'ta': 'ta_IN',
  'hi': 'hi_IN',
  'ar': 'ar_SA',
  'ja': 'ja_JP',
  'ko': 'ko_KR',
};

abstract class ISpeechService {
  bool get isListening;
  bool get isAvailable;
  Future<bool> initialize();
  Future<bool> startListening({
    required String languageCode,
    required void Function(String text) onResult,
    required void Function() onDone,
  });
  Future<void> stopListening();
  void dispose();
}

class SpeechService implements ISpeechService {
  final SpeechToText _speech;

  SpeechService({SpeechToText? speech}) : _speech = speech ?? SpeechToText();

  bool _available = false;

  @override
  bool get isListening => _speech.isListening;

  @override
  bool get isAvailable => _available;

  @override
  Future<bool> initialize() async {
    try {
      _available = await _speech.initialize(
        onError: (_) {},
        onStatus: (_) {},
      );
    } catch (_) {
      _available = false;
    }
    return _available;
  }

  @override
  Future<bool> startListening({
    required String languageCode,
    required void Function(String text) onResult,
    required void Function() onDone,
  }) async {
    if (!_available) return false;

    final requestedLocale = speechLocaleMap[languageCode] ?? 'en_US';

    // Determine the best available locale, falling back gracefully.
    String localeId = requestedLocale;
    try {
      final locales = await _speech.locales();
      final match = locales.firstWhere(
        (l) => l.localeId == requestedLocale,
        orElse: () => locales.firstWhere(
          (l) => l.localeId.startsWith(requestedLocale.split('_').first),
          orElse: () => locales.isNotEmpty ? locales.first : LocaleName('en_US', 'English'),
        ),
      );
      localeId = match.localeId;
    } catch (_) {
      localeId = requestedLocale;
    }

    try {
      await _speech.listen(
        onResult: (result) => onResult(result.recognizedWords),
        listenOptions: SpeechListenOptions(
          localeId: localeId,
          listenFor: const Duration(seconds: 60),
          pauseFor: const Duration(seconds: 3),
        ),
      );
      _speech.statusListener = (status) {
        if (status == SpeechToText.doneStatus || status == SpeechToText.notListeningStatus) {
          onDone();
        }
      };
      return true;
    } catch (_) {
      return false;
    }
  }

  @override
  Future<void> stopListening() async {
    try {
      await _speech.stop();
    } catch (_) {}
  }

  @override
  void dispose() {
    try {
      _speech.cancel();
    } catch (_) {}
  }
}
