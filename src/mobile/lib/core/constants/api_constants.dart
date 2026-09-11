class ApiConstants {
  ApiConstants._();

  static const String baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://localhost:5000/api',
  );

  static const Duration connectTimeout = Duration(seconds: 30);
  static const Duration receiveTimeout = Duration(seconds: 30);

  static const String auth = '/auth';
  static const String users = '/users';
  static const String documents = '/documents';
  static const String documentTypes = '/document-types';
  static const String reminders = '/reminders';
  static const String governmentAssistant = '/government-assistant';
  static const String translation = '/translation';
  static const String emergency = '/emergency';
}
