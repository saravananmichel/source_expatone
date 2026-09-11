class AppConstants {
  AppConstants._();

  static const String appName = 'ExpatOne';
  static const String appVersion = '0.1.0';

  static const String defaultCountryCode = 'MY';
  static const String defaultCountryName = 'Malaysia';

  static const int defaultPageSize = 20;
  static const int maxFileUploadSizeMb = 25;

  static const List<String> supportedDocumentExtensions = [
    'pdf',
    'jpg',
    'jpeg',
    'png',
  ];

  static const List<String> supportedLanguages = [
    'en',
    'ms',
    'ta',
    'hi',
    'zh',
  ];
}
