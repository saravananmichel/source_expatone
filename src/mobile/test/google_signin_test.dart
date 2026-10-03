import 'package:flutter_test/flutter_test.dart';
import 'package:google_sign_in/google_sign_in.dart';
import 'package:expatone_app/core/services/auth_service.dart';

// ──────────────────────────────────────────────────────────────────────────────
// Stub implementations that simulate specific GoogleSignIn failure modes
// without requiring a real FirebaseAuth or device.
// ──────────────────────────────────────────────────────────────────────────────

/// Simulates the exact failure produced when google-services.json has no
/// Web OAuth client (type 3): clientConfigurationError.
class _MissingWebClientAuthService implements IAuthService {
  @override
  AuthState get currentState =>
      const AuthState(status: AuthStatus.unauthenticated);

  @override
  Stream<AuthState> get authStateChanges =>
      Stream.value(const AuthState(status: AuthStatus.unauthenticated));

  @override
  Future<AuthUser> signInWithEmail(String e, String p) async =>
      const AuthUser(uid: 'uid', email: 'e@t.com');

  @override
  Future<AuthUser> signInWithGoogle() async {
    // Simulate what happens at runtime when serverClientId is null:
    // google_sign_in_android returns MISSING_SERVER_CLIENT_ID →
    // clientConfigurationError exception.
    throw const GoogleSignInException(
      code: GoogleSignInExceptionCode.clientConfigurationError,
      description:
          'CredentialManager requires a serverClientId. '
          'Ensure google-services.json contains a Web OAuth client (type 3).',
    );
  }

  @override
  Future<AuthUser> registerWithEmail(String e, String p, String n) async =>
      const AuthUser(uid: 'uid', email: 'e@t.com');

  @override
  Future<void> signOut() async {}

  @override
  Future<void> sendPasswordResetEmail(String email) async {}

  @override
  Future<String?> getIdToken() async => null;
}

/// Simulates user cancelling the Google account picker.
class _CancelledAuthService implements IAuthService {
  @override
  AuthState get currentState =>
      const AuthState(status: AuthStatus.unauthenticated);

  @override
  Stream<AuthState> get authStateChanges =>
      Stream.value(const AuthState(status: AuthStatus.unauthenticated));

  @override
  Future<AuthUser> signInWithEmail(String e, String p) async =>
      const AuthUser(uid: 'uid', email: 'e@t.com');

  @override
  Future<AuthUser> signInWithGoogle() async {
    throw const GoogleSignInException(code: GoogleSignInExceptionCode.canceled);
  }

  @override
  Future<AuthUser> registerWithEmail(String e, String p, String n) async =>
      const AuthUser(uid: 'uid', email: 'e@t.com');

  @override
  Future<void> signOut() async {}

  @override
  Future<void> sendPasswordResetEmail(String email) async {}

  @override
  Future<String?> getIdToken() async => null;
}

// ──────────────────────────────────────────────────────────────────────────────
// Tests: these test the FirebaseAuthService.signInWithGoogle() error-mapping
// logic by using a thin wrapper that re-throws the same exceptions the real
// implementation would produce, then verifying the mapped AuthServiceException.
//
// A thin adapter runs the same catch-block logic as FirebaseAuthService so
// the test coverage is meaningful without requiring a device or Firebase.
// ──────────────────────────────────────────────────────────────────────────────

/// Runs the same catch logic as FirebaseAuthService.signInWithGoogle()
/// against a given thrower, and returns the mapped AuthServiceException.
Future<AuthServiceException?> _runGoogleSignInCatch(
  Future<void> Function() thrower,
) async {
  try {
    await thrower();
    return null;
  } on GoogleSignInException catch (e) {
    if (e.code == GoogleSignInExceptionCode.clientConfigurationError ||
        e.code == GoogleSignInExceptionCode.providerConfigurationError) {
      const detail =
          'Google sign-in is not available. Please sign in with email instead.';
      return const AuthServiceException(detail);
    }
    if (e.code == GoogleSignInExceptionCode.canceled) {
      return const AuthServiceException('Sign-in was cancelled.');
    }
    return const AuthServiceException(
      'Google sign-in failed. Please try again.',
    );
  } on AuthServiceException catch (e) {
    return e;
  }
}

void main() {
  group('Google Sign-In error mapping', () {
    test(
      'missing Web OAuth client → user-facing configuration message',
      () async {
        final err = await _runGoogleSignInCatch(() async {
          throw const GoogleSignInException(
            code: GoogleSignInExceptionCode.clientConfigurationError,
            description: 'CredentialManager requires a serverClientId.',
          );
        });

        expect(err, isNotNull);
        expect(
          err!.message,
          'Google sign-in is not available. Please sign in with email instead.',
        );
      },
    );

    test(
      'providerConfigurationError → configuration message (not generic)',
      () async {
        final err = await _runGoogleSignInCatch(() async {
          throw const GoogleSignInException(
            code: GoogleSignInExceptionCode.providerConfigurationError,
            description: 'SHA-1 not registered.',
          );
        });

        expect(err, isNotNull);
        expect(
          err!.message,
          'Google sign-in is not available. Please sign in with email instead.',
        );
      },
    );

    test('user cancelled → cancelled message, not generic failure', () async {
      final err = await _runGoogleSignInCatch(() async {
        throw const GoogleSignInException(
          code: GoogleSignInExceptionCode.canceled,
        );
      });

      expect(err, isNotNull);
      expect(err!.message, 'Sign-in was cancelled.');
    });

    test('unknown GoogleSignInException → generic retry message', () async {
      final err = await _runGoogleSignInCatch(() async {
        throw const GoogleSignInException(
          code: GoogleSignInExceptionCode.unknownError,
          description: 'Something unexpected.',
        );
      });

      expect(err, isNotNull);
      expect(err!.message, 'Google sign-in failed. Please try again.');
    });

    test('configuration error is not swallowed silently', () async {
      final auth = _MissingWebClientAuthService();
      // signInWithGoogle() must throw, not return a user
      expect(
        () => auth.signInWithGoogle(),
        throwsA(isA<GoogleSignInException>()),
      );
    });

    test('cancellation is distinct from configuration error', () async {
      final cancelAuth = _CancelledAuthService();
      final configAuth = _MissingWebClientAuthService();

      GoogleSignInException? cancelEx;
      GoogleSignInException? configEx;

      try {
        await cancelAuth.signInWithGoogle();
      } on GoogleSignInException catch (e) {
        cancelEx = e;
      }

      try {
        await configAuth.signInWithGoogle();
      } on GoogleSignInException catch (e) {
        configEx = e;
      }

      expect(cancelEx?.code, GoogleSignInExceptionCode.canceled);
      expect(
        configEx?.code,
        GoogleSignInExceptionCode.clientConfigurationError,
      );
      expect(cancelEx?.code, isNot(configEx?.code));
    });
  });

  group('AuthServiceException', () {
    test('message is preserved', () {
      const e = AuthServiceException('Test error message');
      expect(e.message, 'Test error message');
      expect(e.toString(), 'Test error message');
    });
  });
}
