import '../constants/api_constants.dart';
import '../networking/api_client.dart';

class EmergencyAssistResult {
  final String response;
  final String? translatedMessage;
  final String? targetLanguage;

  const EmergencyAssistResult({
    required this.response,
    this.translatedMessage,
    this.targetLanguage,
  });

  factory EmergencyAssistResult.fromJson(Map<String, dynamic> json) =>
      EmergencyAssistResult(
        response: json['response'] as String,
        translatedMessage: json['translatedMessage'] as String?,
        targetLanguage: json['targetLanguage'] as String?,
      );
}

class EmergencyService {
  final ApiClient _apiClient;

  EmergencyService(this._apiClient);

  Future<EmergencyAssistResult> assist({
    required String message,
    String? targetLanguage,
  }) async {
    final body = <String, dynamic>{
      'message': message,
    };
    if (targetLanguage != null) {
      body['targetLanguage'] = targetLanguage;
    }
    final response = await _apiClient.post('/emergency/assist',
        body: body, timeout: ApiConstants.aiTimeout);
    return EmergencyAssistResult.fromJson(response);
  }
}
