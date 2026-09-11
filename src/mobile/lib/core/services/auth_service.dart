import 'package:firebase_auth/firebase_auth.dart' as fb;
import 'package:google_sign_in/google_sign_in.dart';

enum AuthStatus { unknown, unauthenticated, authenticated, loading, error }

class AuthState {
  final AuthStatus status;
  final AuthUser? user;
  final String? errorMessage;

  const AuthState({
    this.status = AuthStatus.unknown,
    this.user,
    this.errorMessage,
  });

  AuthState copyWith({AuthStatus? status, AuthUser? user, String? errorMessage}) {
    return AuthState(
      status: status ?? this.status,
      user: user ?? this.user,
      errorMessage: errorMessage,
    );
  }
}

class AuthUser {
  final String uid;
  final String? email;
  final String? displayName;

  const AuthUser({required this.uid, this.email, this.displayName});
}

abstract class IAuthService {
  Stream<AuthState> get authStateChanges;
  AuthState get currentState;
  Future<AuthUser> signInWithEmail(String email, String password);
  Future<AuthUser> signInWithGoogle();
  Future<AuthUser> registerWithEmail(String email, String password, String displayName);
  Future<void> signOut();
  Future<void> sendPasswordResetEmail(String email);
  Future<String?> getIdToken();
}

class FirebaseAuthService implements IAuthService {
  final fb.FirebaseAuth _auth;
  AuthState _currentState = const AuthState();

  FirebaseAuthService({fb.FirebaseAuth? auth})
      : _auth = auth ?? fb.FirebaseAuth.instance;

  @override
  AuthState get currentState => _currentState;

  @override
  Stream<AuthState> get authStateChanges {
    return _auth.authStateChanges().map((fbUser) {
      if (fbUser == null) {
        _currentState = const AuthState(status: AuthStatus.unauthenticated);
      } else {
        _currentState = AuthState(
          status: AuthStatus.authenticated,
          user: AuthUser(
            uid: fbUser.uid,
            email: fbUser.email,
            displayName: fbUser.displayName,
          ),
        );
      }
      return _currentState;
    });
  }

  @override
  Future<AuthUser> signInWithEmail(String email, String password) async {
    try {
      final credential = await _auth.signInWithEmailAndPassword(
        email: email,
        password: password,
      );
      return _mapUser(credential.user!);
    } on fb.FirebaseAuthException catch (e) {
      throw _mapFirebaseError(e);
    }
  }

  @override
  Future<AuthUser> signInWithGoogle() async {
    try {
      final googleSignIn = GoogleSignIn.instance;
      await googleSignIn.initialize();
      final googleAccount = await googleSignIn.authenticate();

      final idToken = googleAccount.authentication.idToken;
      final credential = fb.GoogleAuthProvider.credential(idToken: idToken);

      final result = await _auth.signInWithCredential(credential);
      return _mapUser(result.user!);
    } on GoogleSignInException {
      throw const AuthServiceException('Google sign-in failed. Please try again.');
    } on fb.FirebaseAuthException catch (e) {
      throw _mapFirebaseError(e);
    }
  }

  @override
  Future<AuthUser> registerWithEmail(String email, String password, String displayName) async {
    try {
      final credential = await _auth.createUserWithEmailAndPassword(
        email: email,
        password: password,
      );
      await credential.user!.updateDisplayName(displayName);
      await credential.user!.reload();
      final updatedUser = _auth.currentUser!;
      return _mapUser(updatedUser);
    } on fb.FirebaseAuthException catch (e) {
      throw _mapFirebaseError(e);
    }
  }

  @override
  Future<void> signOut() async {
    await Future.wait([
      _auth.signOut(),
      GoogleSignIn.instance.signOut(),
    ]);
  }

  @override
  Future<void> sendPasswordResetEmail(String email) async {
    try {
      await _auth.sendPasswordResetEmail(email: email);
    } on fb.FirebaseAuthException catch (e) {
      throw _mapFirebaseError(e);
    }
  }

  @override
  Future<String?> getIdToken() async {
    return await _auth.currentUser?.getIdToken();
  }

  AuthUser _mapUser(fb.User user) {
    return AuthUser(
      uid: user.uid,
      email: user.email,
      displayName: user.displayName,
    );
  }

  AuthServiceException _mapFirebaseError(fb.FirebaseAuthException e) {
    final message = switch (e.code) {
      'user-not-found' => 'No account found with this email address.',
      'wrong-password' => 'Incorrect password. Please try again.',
      'invalid-credential' => 'Invalid email or password. Please try again.',
      'email-already-in-use' => 'An account already exists with this email address.',
      'weak-password' => 'Password is too weak. Please use at least 6 characters.',
      'invalid-email' => 'Please enter a valid email address.',
      'user-disabled' => 'This account has been disabled. Please contact support.',
      'too-many-requests' => 'Too many attempts. Please try again later.',
      'network-request-failed' => 'Network error. Please check your connection.',
      'operation-not-allowed' => 'This sign-in method is not enabled.',
      _ => 'Something went wrong. Please try again.',
    };
    return AuthServiceException(message);
  }
}

class AuthServiceException implements Exception {
  final String message;
  const AuthServiceException(this.message);

  @override
  String toString() => message;
}
