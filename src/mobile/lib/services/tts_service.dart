import 'package:flutter_tts/flutter_tts.dart';

// Maps translation language codes to BCP-47 TTS locales.
const Map<String, String> ttsLocaleMap = {
  'en': 'en-US',
  'ms': 'ms-MY',
  'zh': 'zh-CN',
  'ta': 'ta-IN',
  'hi': 'hi-IN',
  'ar': 'ar-SA',
  'ja': 'ja-JP',
  'ko': 'ko-KR',
};

abstract class ITtsService {
  bool get isSpeaking;
  // Called when speech finishes or is cancelled.
  set onStopped(void Function()? callback);
  Future<void> speak(String text, String languageCode);
  Future<void> stop();
  void dispose();
}

class TtsService implements ITtsService {
  final FlutterTts _tts;
  bool _speaking = false;
  void Function()? _onStopped;

  TtsService({FlutterTts? tts}) : _tts = tts ?? FlutterTts() {
    _tts.setCompletionHandler(_done);
    _tts.setCancelHandler(_done);
    _tts.setErrorHandler((_) => _done());
  }

  void _done() {
    _speaking = false;
    _onStopped?.call();
  }

  @override
  bool get isSpeaking => _speaking;

  @override
  set onStopped(void Function()? callback) => _onStopped = callback;

  @override
  Future<void> speak(String text, String languageCode) async {
    if (text.isEmpty) return;
    final locale = ttsLocaleMap[languageCode] ?? 'en-US';

    try {
      final languages = await _tts.getLanguages as List?;
      if (languages != null && languages.isNotEmpty) {
        final available = languages.any((l) {
          final s = l.toString().toLowerCase();
          return s == locale.toLowerCase() ||
              s.startsWith(locale.split('-').first.toLowerCase());
        });
        await _tts.setLanguage(available ? locale : 'en-US');
      } else {
        await _tts.setLanguage(locale);
      }

      await _tts.setSpeechRate(0.5);
      await _tts.setVolume(1.0);
      _speaking = true;
      await _tts.speak(text);
      // _done() fires via completion handler, not here.
    } catch (_) {
      _done();
    }
  }

  @override
  Future<void> stop() async {
    try {
      await _tts.stop();
    } catch (_) {}
    _done();
  }

  @override
  void dispose() {
    try {
      _tts.stop();
    } catch (_) {}
  }
}
