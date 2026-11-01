import 'dart:async';

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

// Preserve the spelling returned by the device (for example, en_GB).
String? selectTtsLocale(String languageCode, Iterable<String> installed) {
  String normalize(String value) => value.toLowerCase().replaceAll('_', '-');
  final requested = ttsLocaleMap[languageCode] ?? languageCode;
  final voices = installed.where((voice) => voice.trim().isNotEmpty).toList();
  if (voices.isEmpty) return requested;
  for (final preference in [requested, 'en-US']) {
    final tag = normalize(preference);
    for (final voice in voices) {
      if (normalize(voice) == tag) return voice;
    }
    for (final voice in voices) {
      if (normalize(voice).split('-').first == tag.split('-').first) return voice;
    }
  }
  return null; // Keep the engine's default voice.
}

class TtsService implements ITtsService {
  final FlutterTts _tts;
  int _generation = 0;
  bool _speaking = false;
  Completer<void>? _interrupt;
  void Function()? _onStopped;

  TtsService({FlutterTts? tts}) : _tts = tts ?? FlutterTts();

  void _endPendingPlayback() {
    final pending = _interrupt;
    if (pending != null && !pending.isCompleted) pending.complete();
  }

  @override
  bool get isSpeaking => _speaking;

  @override
  set onStopped(void Function()? callback) => _onStopped = callback;

  @override
  Future<void> speak(String text, String languageCode) async {
    if (text.isEmpty) return;
    _endPendingPlayback();
    final turn = ++_generation;
    final notify = _onStopped;
    final interrupt = _speaking;
    _speaking = true;
    try {
      if (interrupt) await _tts.stop();
      if (turn != _generation) return;
      await _tts.awaitSpeakCompletion(true);
      if (turn != _generation) return;
      final languages = await _tts.getLanguages as List?;
      if (turn != _generation) return;
      final locale = selectTtsLocale(languageCode, languages?.whereType<String>() ?? const <String>[]);
      if (locale != null) await _tts.setLanguage(locale);
      if (turn != _generation) return;
      await _tts.setSpeechRate(0.5);
      if (turn != _generation) return;
      await _tts.setVolume(1.0);
      if (turn != _generation) return;
      final interrupted = Completer<void>();
      _interrupt = interrupted;
      _tts.setErrorHandler((message) {
        if (turn != _generation || interrupted.isCompleted) return;
        // Cancel/interrupt notifications can belong to the previous playback.
        final reason = message.toString().toLowerCase();
        if (!reason.contains('cancel') && !reason.contains('interrupt')) {
          interrupted.complete();
        }
      });
      await Future.any<void>([
        _tts.speak(text).then<void>((_) {}),
        interrupted.future,
      ]);
    } catch (_) {
      // Engine failures end this turn just like normal completion.
    } finally {
      if (turn == _generation) {
        _interrupt = null;
        _speaking = false;
        notify?.call();
      }
    }
  }

  @override
  Future<void> stop() async {
    final stopping = ++_generation;
    _endPendingPlayback();
    _interrupt = null;
    final notify = _speaking ? _onStopped : null;
    _speaking = false;
    try {
      await _tts.stop();
    } catch (_) {}
    if (stopping == _generation) notify?.call();
  }

  @override
  void dispose() {
    _onStopped = null;
    stop();
  }
}
