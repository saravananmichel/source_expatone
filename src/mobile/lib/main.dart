import 'package:flutter/material.dart';
import 'package:firebase_core/firebase_core.dart';
import 'core/networking/api_client.dart';
import 'core/services/auth_service.dart';
import 'core/services/user_service.dart';
import 'core/theme/app_theme.dart';
import 'features/auth/login_screen.dart';
import 'shared/services/health_service.dart';
import 'shared/widgets/app_shell.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  bool firebaseInitialized = false;
  String? initError;

  try {
    await Firebase.initializeApp();
    firebaseInitialized = true;
  } catch (e) {
    initError = 'Firebase not configured. See setup docs for instructions.';
  }

  final apiClient = ApiClient();
  final healthService = HealthService(apiClient);

  IAuthService? authService;
  if (firebaseInitialized) {
    authService = FirebaseAuthService();
    apiClient.setAuthService(authService);
  }

  final userService = UserService(apiClient);

  runApp(ExpatOneApp(
    healthService: healthService,
    authService: authService,
    userService: userService,
    initError: initError,
  ));
}

class ExpatOneApp extends StatefulWidget {
  final HealthService healthService;
  final IAuthService? authService;
  final UserService userService;
  final String? initError;

  const ExpatOneApp({
    super.key,
    required this.healthService,
    this.authService,
    required this.userService,
    this.initError,
  });

  @override
  State<ExpatOneApp> createState() => _ExpatOneAppState();
}

class _ExpatOneAppState extends State<ExpatOneApp> {
  AuthState _authState = const AuthState();

  @override
  void initState() {
    super.initState();
    if (widget.authService != null) {
      widget.authService!.authStateChanges.listen((state) {
        if (mounted) setState(() => _authState = state);
      });
    }
  }

  void _onAuthSuccess() {
    // Auth state stream will update the UI automatically
  }

  void _onLogout() {
    // Auth state stream will update the UI automatically
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'ExpatOne',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.lightTheme,
      home: _buildHome(),
    );
  }

  Widget _buildHome() {
    if (widget.initError != null) {
      return _ConfigErrorScreen(message: widget.initError!);
    }

    if (widget.authService == null) {
      return const _ConfigErrorScreen(
        message: 'Firebase is not configured. See setup docs for instructions.',
      );
    }

    switch (_authState.status) {
      case AuthStatus.unknown:
      case AuthStatus.loading:
        return const Scaffold(
          body: Center(child: CircularProgressIndicator()),
        );
      case AuthStatus.unauthenticated:
      case AuthStatus.error:
        return LoginScreen(
          authService: widget.authService!,
          onLoginSuccess: _onAuthSuccess,
        );
      case AuthStatus.authenticated:
        return AppShell(
          healthService: widget.healthService,
          authService: widget.authService!,
          userService: widget.userService,
          onLogout: _onLogout,
          userName: _authState.user?.displayName,
        );
    }
  }
}

class _ConfigErrorScreen extends StatelessWidget {
  final String message;

  const _ConfigErrorScreen({required this.message});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(Icons.settings_outlined, size: 64, color: AppTheme.warningColor),
              const SizedBox(height: 24),
              Text(
                'Configuration Required',
                style: Theme.of(context).textTheme.headlineMedium,
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 12),
              Text(
                message,
                style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                      color: AppTheme.textSecondary,
                    ),
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
