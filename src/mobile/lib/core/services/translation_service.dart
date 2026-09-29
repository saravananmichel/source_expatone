import '../constants/api_constants.dart';
import '../networking/api_client.dart';

class SupportedLanguage {
  final String code;
  final String name;

  const SupportedLanguage({required this.code, required this.name});

  factory SupportedLanguage.fromJson(Map<String, dynamic> json) =>
      SupportedLanguage(
        code: json['code'] as String,
        name: json['name'] as String,
      );
}

class TranslationResult {
  final String translatedText;
  final String sourceLanguage;
  final String targetLanguage;

  const TranslationResult({
    required this.translatedText,
    required this.sourceLanguage,
    required this.targetLanguage,
  });

  factory TranslationResult.fromJson(Map<String, dynamic> json) =>
      TranslationResult(
        translatedText: json['translatedText'] as String,
        sourceLanguage: json['sourceLanguage'] as String,
        targetLanguage: json['targetLanguage'] as String,
      );
}

class TranslationService {
  final ApiClient _apiClient;

  TranslationService(this._apiClient);

  Future<List<SupportedLanguage>> getSupportedLanguages() async {
    final response = await _apiClient.getList('/translation/languages');
    return response.map((e) => SupportedLanguage.fromJson(e)).toList();
  }

  Future<TranslationResult> translate({
    required String text,
    required String sourceLanguage,
    required String targetLanguage,
  }) async {
    final response = await _apiClient.post('/translation/translate',
        body: {
          'text': text,
          'sourceLanguage': sourceLanguage,
          'targetLanguage': targetLanguage,
        },
        timeout: ApiConstants.aiTimeout);
    return TranslationResult.fromJson(response);
  }
}
