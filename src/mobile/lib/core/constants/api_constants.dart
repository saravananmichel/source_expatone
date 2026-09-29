class ApiConstants {
  ApiConstants._();

  static const String _rawBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5000/api',
  );

  // In release mode, reject any URL that points to a local/emulator address.
  // A release build must be given an explicit --dart-define=API_BASE_URL=https://...
  // Production builds that omit this define will assert-fail at startup rather
  // than silently shipping with the Android emulator address.
  static String get baseUrl {
    assert(
      !const bool.fromEnvironment('dart.vm.product') || _isProductionUrl(_rawBaseUrl),
      'RELEASE BUILD ERROR: API_BASE_URL is "$_rawBaseUrl". '
      'Production builds must pass --dart-define=API_BASE_URL=https://<production-domain>/api. '
      'The emulator/localhost address must never ship in a production APK.',
    );
    return _rawBaseUrl;
  }

  static bool _isProductionUrl(String url) => isProductionUrlForTest(url);

  // Exposed for tests only. Do not call from application code.
  static bool isProductionUrlForTest(String url) {
    final lower = url.toLowerCase();
    if (lower.contains('10.0.2.2')) return false;
    if (lower.contains('localhost')) return false;
    if (lower.contains('127.0.0.1')) return false;
    if (!lower.startsWith('https://')) return false;
    return true;
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
