import 'dart:async';

import 'package:expatone_app/services/tts_service.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_tts/flutter_tts.dart';

class _Engine implements FlutterTts {
  final utterances = <Completer<void>>[];
  void Function(dynamic)? onError;

  @override
  void setErrorHandler(ErrorHandler handler) => onError = handler;

  @override
  Future<dynamic> speak(String text, {bool focus = false}) {
    final utterance = Completer<void>();
    utterances.add(utterance);
    return utterance.future;
  }

  @override
  Future<dynamic> get getLanguages async => ['en-GB'];
  @override
  dynamic noSuchMethod(Invocation invocation) => Future<dynamic>.value(1);
}

void main() {
  test('stopped playback cannot finish the replacement utterance', () async {
    final engine = _Engine();
    final service = TtsService(tts: engine);
    var notifications = 0;
    service.onStopped = () => notifications++;
    final first = service.speak('First', 'en');
    await Future<void>.delayed(Duration.zero);
    final oldError = engine.onError!;
    await service.stop();
    await first;
    expect(notifications, 1);

    final second = service.speak('Second', 'en');
    await Future<void>.delayed(Duration.zero);
    engine.utterances.first.complete();
    oldError('Late error');
    await Future<void>.delayed(Duration.zero);
    expect(service.isSpeaking, isTrue);
    expect(notifications, 1);
    engine.utterances.last.complete();
    await second;
    expect(notifications, 2);
    await service.stop();
    expect(notifications, 2);
  });
}
