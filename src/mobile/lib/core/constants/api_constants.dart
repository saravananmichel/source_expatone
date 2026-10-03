class ApiConstants {
  ApiConstants._();

  static const String _rawBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5000/api',
  );

  // Release assertions are disabled, so enforce HTTPS with a runtime check.
  static String get baseUrl {
    if (const bool.fromEnvironment('dart.vm.product') &&
        !isProductionUrlForTest(_rawBaseUrl)) {
      throw StateError(
        'Release builds require --dart-define=API_BASE_URL=https://<host>/api',
      );
    }
    return _rawBaseUrl;
  }

  static bool isProductionUrlForTest(String url) {
    final uri = Uri.tryParse(url);
    if (uri == null ||
        uri.scheme != 'https' ||
        uri.host.isEmpty ||
        uri.userInfo.isNotEmpty ||
        uri.hasQuery ||
        uri.hasFragment) {
      return false;
    }
    final host = uri.host.toLowerCase();
    return host != 'localhost' &&
        !host.endsWith('.localhost') &&
        host != '10.0.2.2' &&
        !host.startsWith('127.') &&
        host != '::1';
  }

  static const Duration connectTimeout = Duration(seconds: 30);
  static const Duration receiveTimeout = Duration(seconds: 30);
  static const Duration aiTimeout = Duration(seconds: 60);

  static const String auth = '/auth';
  static const String users = '/users';
  static const String documents = '/documents';
  static const String documentTypes = '/document-types';
  static const String reminders = '/reminders';
  static const String assistant = '/assistant';
  static const String translation = '/translation';
  static const String emergency = '/emergency';
}
