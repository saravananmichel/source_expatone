import 'package:flutter/material.dart';
import 'package:firebase_core/firebase_core.dart';
import 'core/networking/api_client.dart';
import 'core/services/assistant_service.dart';
import 'core/services/auth_service.dart';
import 'core/services/document_service.dart';
import 'core/services/emergency_service.dart';
import 'core/services/reminder_service.dart';
import 'core/services/translation_service.dart';
import 'core/services/user_service.dart';
import 'core/theme/app_theme.dart';
import 'features/auth/login_screen.dart';
import 'features/onboarding/onboarding_screen.dart';
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
  final documentService = DocumentService(apiClient);
  final reminderService = ReminderService(apiClient);
  final assistantService = AssistantService(apiClient);
  final translationService = TranslationService(apiClient);
  final emergencyService = EmergencyService(apiClient);

  runApp(ExpatOneApp(
    healthService: healthService,
    authService: authService,
    userService: userService,
    documentService: documentService,
    reminderService: reminderService,
    assistantService: assistantService,
    translationService: translationService,
    emergencyService: emergencyService,
    initError: initError,
  ));
}

class ExpatOneApp extends StatefulWidget {
  final HealthService healthService;
  final IAuthService? authService;
  final UserService userService;
  final DocumentService documentService;
  final ReminderService reminderService;
  final AssistantService assistantService;
  final TranslationService translationService;
  final EmergencyService emergencyService;
  final String? initError;

  const ExpatOneApp({
    super.key,
    required this.healthService,
    this.authService,
    required this.userService,
    required this.documentService,
    required this.reminderService,
    required this.assistantService,
    required this.translationService,
    required this.emergencyService,
    this.initError,
  });

  @override
  State<ExpatOneApp> createState() => _ExpatOneAppState();
}

class _ExpatOneAppState extends State<ExpatOneApp> {
  AuthState _authState = const AuthState();
  bool? _onboardingCompleted;
  bool _isCheckingProfile = false;

  @override
  void initState() {
    super.initState();
    if (widget.authService != null) {
      widget.authService!.authStateChanges.listen((state) {
        if (mounted) {
          final wasAuthenticated = _authState.status == AuthStatus.authenticated;
          setState(() => _authState = state);
          if (state.status == AuthStatus.authenticated && !wasAuthenticated) {
            _checkOnboarding();
          }
          if (state.status != AuthStatus.authenticated) {
            _onboardingCompleted = null;
          }
        }
      });
    }
  }

  Future<void> _checkOnboarding() async {
    setState(() => _isCheckingProfile = true);
    try {
      final profile = await widget.userService.getMe();
      if (mounted) {
        setState(() {
          _onboardingCompleted = profile.onboardingCompleted;
          _isCheckingProfile = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _onboardingCompleted = true;
          _isCheckingProfile = false;
        });
      }
    }
  }

  void _onAuthSuccess() {
    // Auth state stream will update the UI automatically
  }

  void _onLogout() {
    // Auth state stream will update the UI automatically
  }

  void _onOnboardingComplete() {
    setState(() => _onboardingCompleted = true);
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
        if (_isCheckingProfile || _onboardingCompleted == null) {
          return const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          );
        }

        if (_onboardingCompleted == false) {
          return OnboardingScreen(
            userService: widget.userService,
            onComplete: _onOnboardingComplete,
          );
        }

        return AppShell(
          healthService: widget.healthService,
          authService: widget.authService!,
          userService: widget.userService,
          documentService: widget.documentService,
          reminderService: widget.reminderService,
          assistantService: widget.assistantService,
          translationService: widget.translationService,
          emergencyService: widget.emergencyService,
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
