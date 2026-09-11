import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:expatone_app/core/services/auth_service.dart';
import 'package:expatone_app/features/auth/login_screen.dart';
import 'package:expatone_app/features/auth/register_screen.dart';
import 'package:expatone_app/features/auth/forgot_password_screen.dart';

class MockAuthService implements IAuthService {
  AuthState _state = const AuthState(status: AuthStatus.unauthenticated);
  String? lastEmail;
  String? lastPassword;
  bool signOutCalled = false;
  bool shouldFail = false;

  @override
  AuthState get currentState => _state;

  @override
  Stream<AuthState> get authStateChanges => Stream.value(_state);

  @override
  Future<AuthUser> signInWithEmail(String email, String password) async {
    lastEmail = email;
    lastPassword = password;
    if (shouldFail) throw const AuthServiceException('Invalid email or password.');
    return const AuthUser(uid: 'test-uid', email: 'test@example.com', displayName: 'Test');
  }

  @override
  Future<AuthUser> signInWithGoogle() async {
    if (shouldFail) throw const AuthServiceException('Google sign-in failed.');
    return const AuthUser(uid: 'google-uid', email: 'google@example.com');
  }

  @override
  Future<AuthUser> registerWithEmail(String email, String password, String displayName) async {
    if (shouldFail) throw const AuthServiceException('Registration failed.');
    return AuthUser(uid: 'new-uid', email: email, displayName: displayName);
  }

  @override
  Future<void> signOut() async {
    signOutCalled = true;
    _state = const AuthState(status: AuthStatus.unauthenticated);
  }

  @override
  Future<void> sendPasswordResetEmail(String email) async {
    lastEmail = email;
    if (shouldFail) throw const AuthServiceException('No account found.');
  }

  @override
  Future<String?> getIdToken() async => 'mock-token';
}

void main() {
  late MockAuthService authService;
  bool loginSuccessCalled = false;

  setUp(() {
    authService = MockAuthService();
    loginSuccessCalled = false;
  });

  group('LoginScreen', () {
    testWidgets('shows email and password fields', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: LoginScreen(
          authService: authService,
          onLoginSuccess: () => loginSuccessCalled = true,
        ),
      ));

      expect(find.text('Email'), findsOneWidget);
      expect(find.text('Password'), findsOneWidget);
      expect(find.text('Sign In'), findsOneWidget);
    });

    testWidgets('shows ExpatOne branding', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: LoginScreen(
          authService: authService,
          onLoginSuccess: () {},
        ),
      ));

      expect(find.text('ExpatOne'), findsOneWidget);
      expect(find.text('Your life in Malaysia, simplified'), findsOneWidget);
    });

    testWidgets('validates empty email', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: LoginScreen(
          authService: authService,
          onLoginSuccess: () {},
        ),
      ));

      await tester.tap(find.text('Sign In'));
      await tester.pump();

      expect(find.text('Please enter your email'), findsOneWidget);
    });

    testWidgets('validates invalid email', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: LoginScreen(
          authService: authService,
          onLoginSuccess: () {},
        ),
      ));

      await tester.enterText(find.byType(TextFormField).first, 'notanemail');
      await tester.enterText(find.byType(TextFormField).last, 'password123');
      await tester.tap(find.text('Sign In'));
      await tester.pump();

      expect(find.text('Please enter a valid email'), findsOneWidget);
    });

    testWidgets('shows error on failed login', (tester) async {
      authService.shouldFail = true;
      await tester.pumpWidget(MaterialApp(
        home: LoginScreen(
          authService: authService,
          onLoginSuccess: () {},
        ),
      ));

      await tester.enterText(find.byType(TextFormField).first, 'test@example.com');
      await tester.enterText(find.byType(TextFormField).last, 'wrongpassword');
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(find.text('Invalid email or password.'), findsOneWidget);
    });

    testWidgets('calls onLoginSuccess on successful login', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: LoginScreen(
          authService: authService,
          onLoginSuccess: () => loginSuccessCalled = true,
        ),
      ));

      await tester.enterText(find.byType(TextFormField).first, 'test@example.com');
      await tester.enterText(find.byType(TextFormField).last, 'password123');
      await tester.tap(find.text('Sign In'));
      await tester.pumpAndSettle();

      expect(loginSuccessCalled, isTrue);
    });

    testWidgets('has forgot password link', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: LoginScreen(
          authService: authService,
          onLoginSuccess: () {},
        ),
      ));

      expect(find.text('Forgot password?'), findsOneWidget);
    });

    testWidgets('has sign up link', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: LoginScreen(
          authService: authService,
          onLoginSuccess: () {},
        ),
      ));

      expect(find.text("Don't have an account?"), findsOneWidget);
      expect(find.text('Sign Up'), findsOneWidget);
    });

    testWidgets('has Google sign-in button', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: LoginScreen(
          authService: authService,
          onLoginSuccess: () {},
        ),
      ));

      expect(find.text('Continue with Google'), findsOneWidget);
    });
  });

  group('RegisterScreen', () {
    testWidgets('shows all registration fields', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: RegisterScreen(
          authService: authService,
          onRegisterSuccess: () {},
        ),
      ));

      expect(find.text('Full Name'), findsOneWidget);
      expect(find.text('Email'), findsOneWidget);
      expect(find.text('Password'), findsOneWidget);
      expect(find.text('Confirm Password'), findsOneWidget);
      expect(find.text('Create Account'), findsWidgets);
    });

    testWidgets('validates empty fields', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: RegisterScreen(
          authService: authService,
          onRegisterSuccess: () {},
        ),
      ));

      await tester.tap(find.byType(ElevatedButton));
      await tester.pump();

      expect(find.text('Please enter your name'), findsOneWidget);
      expect(find.text('Please enter your email'), findsOneWidget);
    });

    testWidgets('validates password mismatch', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: RegisterScreen(
          authService: authService,
          onRegisterSuccess: () {},
        ),
      ));

      final fields = find.byType(TextFormField);
      await tester.enterText(fields.at(0), 'Test User');
      await tester.enterText(fields.at(1), 'test@example.com');
      await tester.enterText(fields.at(2), 'password123');
      await tester.enterText(fields.at(3), 'differentpassword');
      await tester.ensureVisible(find.byType(ElevatedButton));
      await tester.tap(find.byType(ElevatedButton));
      await tester.pump();

      expect(find.text('Passwords do not match'), findsOneWidget);
    });

    testWidgets('validates short password', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: RegisterScreen(
          authService: authService,
          onRegisterSuccess: () {},
        ),
      ));

      final fields = find.byType(TextFormField);
      await tester.enterText(fields.at(0), 'Test User');
      await tester.enterText(fields.at(1), 'test@example.com');
      await tester.enterText(fields.at(2), '123');
      await tester.enterText(fields.at(3), '123');
      await tester.ensureVisible(find.byType(ElevatedButton));
      await tester.tap(find.byType(ElevatedButton));
      await tester.pump();

      expect(find.text('Password must be at least 6 characters'), findsOneWidget);
    });

    testWidgets('has sign in link', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: RegisterScreen(
          authService: authService,
          onRegisterSuccess: () {},
        ),
      ));

      expect(find.text('Already have an account?'), findsOneWidget);
      expect(find.text('Sign In'), findsOneWidget);
    });
  });

  group('ForgotPasswordScreen', () {
    testWidgets('shows email field and send button', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: ForgotPasswordScreen(authService: authService),
      ));

      expect(find.text('Forgot your password?'), findsOneWidget);
      expect(find.text('Email'), findsOneWidget);
      expect(find.text('Send Reset Link'), findsOneWidget);
    });

    testWidgets('shows success after sending', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: ForgotPasswordScreen(authService: authService),
      ));

      await tester.enterText(find.byType(TextFormField).first, 'test@example.com');
      await tester.tap(find.text('Send Reset Link'));
      await tester.pumpAndSettle();

      expect(find.text('Check your email'), findsOneWidget);
      expect(find.text('Back to Sign In'), findsOneWidget);
    });

    testWidgets('shows error on failure', (tester) async {
      authService.shouldFail = true;
      await tester.pumpWidget(MaterialApp(
        home: ForgotPasswordScreen(authService: authService),
      ));

      await tester.enterText(find.byType(TextFormField).first, 'test@example.com');
      await tester.tap(find.text('Send Reset Link'));
      await tester.pumpAndSettle();

      expect(find.text('No account found.'), findsOneWidget);
    });
  });

  group('AuthState', () {
    test('defaults to unknown', () {
      const state = AuthState();
      expect(state.status, AuthStatus.unknown);
      expect(state.user, isNull);
    });

    test('copyWith updates fields', () {
      const state = AuthState(status: AuthStatus.authenticated);
      final updated = state.copyWith(status: AuthStatus.unauthenticated);
      expect(updated.status, AuthStatus.unauthenticated);
    });
  });

  group('AuthServiceException', () {
    test('toString returns message', () {
      const e = AuthServiceException('test error');
      expect(e.toString(), 'test error');
      expect(e.message, 'test error');
    });
  });
}
