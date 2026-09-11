enum Environment { development, staging, production }

class AppConfig {
  final Environment environment;
  final String apiBaseUrl;

  const AppConfig({
    required this.environment,
    required this.apiBaseUrl,
  });

  static const AppConfig development = AppConfig(
    environment: Environment.development,
    apiBaseUrl: 'http://localhost:5000/api',
  );

  static const AppConfig staging = AppConfig(
    environment: Environment.staging,
    apiBaseUrl: 'https://staging-api.expatone.com/api',
  );

  static const AppConfig production = AppConfig(
    environment: Environment.production,
    apiBaseUrl: 'https://api.expatone.com/api',
  );

  bool get isDevelopment => environment == Environment.development;
}
