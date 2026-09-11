import 'package:flutter/material.dart';
import 'core/networking/api_client.dart';
import 'core/theme/app_theme.dart';
import 'shared/services/health_service.dart';
import 'shared/widgets/app_shell.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  final apiClient = ApiClient();
  final healthService = HealthService(apiClient);
  runApp(ExpatOneApp(healthService: healthService));
}

class ExpatOneApp extends StatelessWidget {
  final HealthService healthService;

  const ExpatOneApp({super.key, required this.healthService});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'ExpatOne',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.lightTheme,
      home: AppShell(healthService: healthService),
    );
  }
}
