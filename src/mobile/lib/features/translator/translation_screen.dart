import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../core/error/app_exception.dart';
import '../../core/services/translation_service.dart';
import '../../core/theme/app_theme.dart';
import '../../services/speech_service.dart';
import '../../services/tts_service.dart';

class TranslationScreen extends StatefulWidget {
  final TranslationService translationService;
  final ISpeechService? speechService;
  final ITtsService? ttsService;

  const TranslationScreen({
    super.key,
    required this.translationService,
    this.speechService,
    this.ttsService,
  });

  @override
  State<TranslationScreen> createState() => _TranslationScreenState();
}

class _TranslationScreenState extends State<TranslationScreen> {
  final _inputController = TextEditingController();
  static const int _maxLength = 5000;

  String _sourceLanguage = 'auto';
  String _targetLanguage = 'ms';
  bool _isLoading = false;
  String? _error;
  TranslationResult? _result;

  // Voice input state
  bool _isListening = false;
  bool _speechAvailable = false;
  late ISpeechService _speechService;

  // TTS state
  bool _isSpeaking = false;
  bool _conversationMode = false;
  bool _processingTurn = false;
  Timer? _silenceTimer;
  Timer? _resumeTimer;
  int _conversationEpoch = 0;
  String _lastRecognized = '';
  String _voiceStatus = 'Ready';
  late ITtsService _ttsService;

  static const _languages = [
    SupportedLanguage(code: 'auto', name: 'Auto-detect'),
    SupportedLanguage(code: 'en', name: 'English'),
    SupportedLanguage(code: 'ms', name: 'Malay'),
    SupportedLanguage(code: 'zh', name: 'Chinese'),
    SupportedLanguage(code: 'ta', name: 'Tamil'),
    SupportedLanguage(code: 'hi', name: 'Hindi'),
    SupportedLanguage(code: 'ar', name: 'Arabic'),
    SupportedLanguage(code: 'ja', name: 'Japanese'),
    SupportedLanguage(code: 'ko', name: 'Korean'),
  ];

  static const _targetLanguages = [
    SupportedLanguage(code: 'en', name: 'English'),
    SupportedLanguage(code: 'ms', name: 'Malay'),
    SupportedLanguage(code: 'zh', name: 'Chinese'),
    SupportedLanguage(code: 'ta', name: 'Tamil'),
    SupportedLanguage(code: 'hi', name: 'Hindi'),
    SupportedLanguage(code: 'ar', name: 'Arabic'),
    SupportedLanguage(code: 'ja', name: 'Japanese'),
    SupportedLanguage(code: 'ko', name: 'Korean'),
  ];

  @override
  void initState() {
    super.initState();
    _speechService = widget.speechService ?? SpeechService();
    _ttsService = widget.ttsService ?? TtsService();
    _ttsService.onStopped = () {
      if (mounted) setState(() => _isSpeaking = false);
      _resumeConversation();
    };
    _initSpeech();
  }

  Future<void> _initSpeech() async {
    final available = await _speechService.initialize();
    if (mounted) {
      setState(() => _speechAvailable = available);
    }
  }

  @override
  void dispose() {
    _conversationMode = false;
    _silenceTimer?.cancel();
    _resumeTimer?.cancel();
    _inputController.dispose();
    // Clear callback before stop() to avoid setState on a disposed element.
    _ttsService.onStopped = null;
    _ttsService.stop();
    _speechService.dispose();
    _ttsService.dispose();
    super.dispose();
  }

  // ── Existing translation logic (unchanged) ──────────────────────────────────

  Future<void> _translate() async {
    final text = _inputController.text.trim();
    if (text.isEmpty) return;

    // Stop any ongoing speech when a new translation starts.
    if (_isSpeaking) {
      await _ttsService.stop();
      if (mounted) setState(() => _isSpeaking = false);
    }

    setState(() {
      _isLoading = true;
      _error = null;
      _result = null;
    });

    try {
      final result = await widget.translationService.translate(
        text: text,
        sourceLanguage: _sourceLanguage,
        targetLanguage: _targetLanguage,
      );
      if (mounted) {
        setState(() {
          _result = result;
          _isLoading = false;
        });
      }
    } on NetworkException catch (e) {
      if (mounted) {
        setState(() {
          _error = e.statusCode == 503
              ? 'Translation is temporarily unavailable. Please try again later.'
              : e.message;
          _isLoading = false;
        });
      }
    } on AuthException {
      if (mounted) {
        setState(() {
          _error = 'Authentication required. Please sign in again.';
          _isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = 'Translation failed. Please try again.';
          _isLoading = false;
        });
      }
    }
  }

  void _swapLanguages() {
    if (_sourceLanguage == 'auto') return;
    setState(() {
      final temp = _sourceLanguage;
      _sourceLanguage = _targetLanguage;
      _targetLanguage = temp;
      if (_result != null) {
        _inputController.text = _result!.translatedText;
        _result = null;
        _error = null;
      }
    });
  }

  void _clear() {
    if (_isSpeaking) {
      _ttsService.stop();
    }
    setState(() {
      _inputController.clear();
      _result = null;
      _error = null;
      _isSpeaking = false;
    });
  }

  void _copyResult() {
    if (_result == null) return;
    Clipboard.setData(ClipboardData(text: _result!.translatedText));
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Translation copied to clipboard'),
        duration: Duration(seconds: 2),
      ),
    );
  }

  // ── Voice input ─────────────────────────────────────────────────────────────

  Future<void> _toggleListening() async {
    if (_isListening) {
      await _speechService.stopListening();
      if (mounted) setState(() => _isListening = false);
      return;
    }

    if (!_speechAvailable) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Speech recognition is not available on this device.'),
          duration: Duration(seconds: 3),
        ),
      );
      return;
    }

    // Use the selected source language; fall back to English for auto-detect.
    final locale = _sourceLanguage == 'auto' ? 'en' : _sourceLanguage;

    setState(() => _isListening = true);

    final started = await _speechService.startListening(
      languageCode: locale,
      onResult: (text) {
        if (mounted && text.isNotEmpty) {
          setState(() => _inputController.text = text);
        }
      },
      onDone: () {
        if (mounted) setState(() => _isListening = false);
      },
    );

    if (!started && mounted) {
      setState(() => _isListening = false);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
              'Could not start speech recognition. Check microphone permissions.'),
          duration: Duration(seconds: 3),
        ),
      );
    }
  }

  Future<void> _toggleConversation() async {
    if (_conversationMode) {
      _conversationMode = false;
      _conversationEpoch++;
      _silenceTimer?.cancel();
      _resumeTimer?.cancel();
      _processingTurn = false;
      await _speechService.stopListening();
      await _ttsService.stop();
      if (mounted) setState(() { _isListening = false; _isSpeaking = false; _voiceStatus = 'Stopped'; });
      return;
    }
    if (!_speechAvailable) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Microphone unavailable or permission denied.')));
      return;
    }
    if (_sourceLanguage == 'auto') setState(() => _sourceLanguage = 'en');
    if (_isListening) await _speechService.stopListening();
    _conversationMode = true;
    _conversationEpoch++;
    await _listenTurn();
  }

  Future<void> _listenTurn() async {
    if (!mounted || !_conversationMode || _processingTurn || _isListening) return;
    final epoch = _conversationEpoch;
    _lastRecognized = '';
    _silenceTimer?.cancel();
    setState(() => _voiceStatus = 'Listening...');
    final started = await _speechService.startListening(
      languageCode: _sourceLanguage,
      onResult: (text) {
        if (!mounted || !_conversationMode || epoch != _conversationEpoch || _processingTurn || text.trim().isEmpty) return;
        if (text.trim() == _lastRecognized) return;
        _lastRecognized = text.trim();
        setState(() => _inputController.text = _lastRecognized);
        _silenceTimer?.cancel();
        _silenceTimer = Timer(const Duration(seconds: 2), _finishTurn);
      },
      onDone: () {
        if (!mounted || !_conversationMode || epoch != _conversationEpoch || _processingTurn) return;
        _silenceTimer?.cancel();
        setState(() => _isListening = false);
        if (_lastRecognized.isNotEmpty) {
          _finishTurn();
        } else {
          _scheduleResume();
        }
      },
    );
    if (!mounted || !_conversationMode || epoch != _conversationEpoch) {
      if (started) await _speechService.stopListening();
      return;
    }
    if (!started) {
      _conversationMode = false;
      setState(() => _voiceStatus = 'Could not start microphone');
    } else {
      setState(() => _isListening = true);
    }
  }

  Future<void> _finishTurn() async {
    _silenceTimer?.cancel();
    if (!_conversationMode || _processingTurn || _lastRecognized.isEmpty) return;
    _processingTurn = true;
    setState(() { _isListening = false; _isLoading = true; _voiceStatus = 'Translating...'; });
    await _speechService.stopListening();
    final epoch = _conversationEpoch;
    try {
      final result = await widget.translationService.translate(
        text: _lastRecognized, sourceLanguage: _sourceLanguage,
        targetLanguage: _targetLanguage);
      if (!mounted || !_conversationMode || epoch != _conversationEpoch) return;
      setState(() { _result = result; _isLoading = false; _isSpeaking = true; _voiceStatus = 'Speaking...'; });
      await _ttsService.speak(result.translatedText, _targetLanguage);
      if (!_ttsService.isSpeaking) _resumeConversation();
    } catch (_) {
      if (mounted) setState(() { _isLoading = false; _error = 'Voice translation failed'; });
      _resumeConversation();
    }
  }

  void _resumeConversation() {
    if (!mounted || !_conversationMode || !_processingTurn) return;
    _processingTurn = false;
    setState(() { _isSpeaking = false; _voiceStatus = 'Ready for next phrase'; });
    _scheduleResume();
  }

  void _scheduleResume() {
    _resumeTimer?.cancel();
    final epoch = _conversationEpoch;
    _resumeTimer = Timer(const Duration(milliseconds: 650), () {
      if (mounted && _conversationMode && epoch == _conversationEpoch) _listenTurn();
    });
  }

  Widget _conversationControls() => Card(
    child: Padding(padding: const EdgeInsets.all(16), child: Column(children: [
      const Text('Live Voice Translator', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
      const SizedBox(height: 8),
      const Text('Speak, pause for 2 seconds, hear the translation, then speak again.'),
      const SizedBox(height: 12),
      Icon(_isSpeaking ? Icons.volume_up : _isListening ? Icons.graphic_eq : Icons.mic,
        size: 44, color: AppTheme.primaryColor),
      Text(_voiceStatus),
      const SizedBox(height: 8),
      FilledButton.icon(onPressed: _toggleConversation,
        icon: Icon(_conversationMode ? Icons.stop : Icons.mic),
        label: Text(_conversationMode ? 'Stop conversation' : 'Start conversation')),
    ])),
  );

  // ── TTS ─────────────────────────────────────────────────────────────────────

  Future<void> _toggleSpeaking() async {
    if (_result == null) return;

    if (_isSpeaking) {
      await _ttsService.stop();
      if (mounted) setState(() => _isSpeaking = false);
      return;
    }

    setState(() => _isSpeaking = true);
    await _ttsService.speak(_result!.translatedText, _targetLanguage);
    // _isSpeaking is reset via the onStopped callback.
  }

  // ── Build ────────────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Translate'),
        actions: [
          if (_inputController.text.isNotEmpty || _result != null)
            IconButton(
              icon: const Icon(Icons.clear),
              onPressed: _clear,
              tooltip: 'Clear',
            ),
        ],
      ),
      body: GestureDetector(
        onTap: () => FocusScope.of(context).unfocus(),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _buildLanguageSelector(),
              _conversationControls(),

              const SizedBox(height: 16),
              _buildInputCard(),
              const SizedBox(height: 12),
              _buildTranslateButton(),
              if (_error != null) ...[
                const SizedBox(height: 12),
                _buildErrorCard(),
              ],
              if (_result != null) ...[
                const SizedBox(height: 16),
                _buildResultCard(),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildLanguageSelector() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        child: Row(
          children: [
            Expanded(
              child: DropdownButtonHideUnderline(
                child: DropdownButton<String>(
                  value: _sourceLanguage,
                  isExpanded: true,
                  items: _languages
                      .map((lang) => DropdownMenuItem(
                            value: lang.code,
                            child: Text(lang.name,
                                style: const TextStyle(fontSize: 14)),
                          ))
                      .toList(),
                  onChanged: _conversationMode ? null : (value) {
                    if (value != null) {
                      setState(() => _sourceLanguage = value);
                    }
                  },
                ),
              ),
            ),
            IconButton(
              icon: Icon(
                Icons.swap_horiz,
                color: _sourceLanguage == 'auto'
                    ? AppTheme.textSecondary.withValues(alpha: 0.4)
                    : AppTheme.primaryColor,
              ),
              onPressed: _conversationMode || _sourceLanguage == 'auto' ? null : _swapLanguages,
              tooltip: 'Swap languages',
            ),
            Expanded(
              child: DropdownButtonHideUnderline(
                child: DropdownButton<String>(
                  value: _targetLanguage,
                  isExpanded: true,
                  alignment: AlignmentDirectional.centerEnd,
                  items: _targetLanguages
                      .map((lang) => DropdownMenuItem(
                            value: lang.code,
                            child: Text(lang.name,
                                style: const TextStyle(fontSize: 14),
                                textAlign: TextAlign.end),
                          ))
                      .toList(),
                  onChanged: _conversationMode ? null : (value) {
                    if (value != null) {
                      setState(() => _targetLanguage = value);
                    }
                  },
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildInputCard() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            TextField(
              controller: _inputController,
              maxLines: 6,
              minLines: 4,
              maxLength: _maxLength,
              decoration: const InputDecoration(
                hintText: 'Enter text to translate...',
                border: InputBorder.none,
                counterText: '',
              ),
              style: const TextStyle(fontSize: 16),
              onChanged: (_) => setState(() {}),
            ),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                // Listening indicator
                if (_isListening)
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      SizedBox(
                        width: 12,
                        height: 12,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: AppTheme.errorColor,
                        ),
                      ),
                      const SizedBox(width: 6),
                      Text(
                        'Listening...',
                        style: TextStyle(
                          fontSize: 12,
                          color: AppTheme.errorColor,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                  )
                else
                  const SizedBox.shrink(),
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      '${_inputController.text.length} / $_maxLength',
                      style: TextStyle(
                        fontSize: 12,
                        color: _inputController.text.length > _maxLength * 0.9
                            ? AppTheme.warningColor
                            : AppTheme.textSecondary,
                      ),
                    ),
                    const SizedBox(width: 4),
                    // Microphone button
                    SizedBox(
                      width: 36,
                      height: 36,
                      child: IconButton(
                        padding: EdgeInsets.zero,
                        icon: Icon(
                          _isListening ? Icons.mic : Icons.mic_none,
                          size: 20,
                          color: _isListening
                              ? AppTheme.errorColor
                              : (_speechAvailable
                                  ? AppTheme.primaryColor
                                  : AppTheme.textSecondary
                                      .withValues(alpha: 0.4)),
                        ),
                        onPressed: _conversationMode ? null : _toggleListening,
                        tooltip: _isListening ? 'Stop listening' : 'Voice input',
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildTranslateButton() {
    final canTranslate = _inputController.text.trim().isNotEmpty && !_isLoading;
    return SizedBox(
      height: 48,
      child: ElevatedButton(
        onPressed: canTranslate && !_conversationMode ? _translate : null,
        child: _isLoading
            ? const SizedBox(
                height: 20,
                width: 20,
                child: CircularProgressIndicator(
                  strokeWidth: 2,
                  color: Colors.white,
                ),
              )
            : const Text('Translate', style: TextStyle(fontSize: 16)),
      ),
    );
  }

  Widget _buildErrorCard() {
    return Card(
      color: AppTheme.errorColor.withValues(alpha: 0.08),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Row(
          children: [
            const Icon(Icons.error_outline, color: AppTheme.errorColor, size: 20),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                _error!,
                style: const TextStyle(color: AppTheme.errorColor, fontSize: 14),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildResultCard() {
    return Card(
      color: AppTheme.primaryColor.withValues(alpha: 0.04),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.translate, size: 18, color: AppTheme.primaryColor),
                const SizedBox(width: 8),
                Text(
                  'Translation',
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                    color: AppTheme.primaryColor.withValues(alpha: 0.7),
                  ),
                ),
                const Spacer(),
                // Speaker button
                SizedBox(
                  width: 32,
                  height: 32,
                  child: IconButton(
                    padding: EdgeInsets.zero,
                    icon: Icon(
                      _isSpeaking ? Icons.volume_up : Icons.volume_up_outlined,
                      size: 18,
                      color: _isSpeaking
                          ? AppTheme.primaryColor
                          : AppTheme.primaryColor.withValues(alpha: 0.7),
                    ),
                    onPressed: _result != null ? _toggleSpeaking : null,
                    tooltip: _isSpeaking ? 'Stop speaking' : 'Read aloud',
                  ),
                ),
                const SizedBox(width: 4),
                // Copy button (unchanged)
                SizedBox(
                  width: 32,
                  height: 32,
                  child: IconButton(
                    icon: const Icon(Icons.copy, size: 18),
                    color: AppTheme.primaryColor,
                    onPressed: _copyResult,
                    tooltip: 'Copy translation',
                    padding: EdgeInsets.zero,
                    constraints: const BoxConstraints(),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            SelectableText(
              _result!.translatedText,
              style: const TextStyle(fontSize: 16, height: 1.5),
            ),
          ],
        ),
      ),
    );
  }
}
