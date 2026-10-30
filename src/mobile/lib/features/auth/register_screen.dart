import 'package:flutter/material.dart';
import 'package:file_picker/file_picker.dart';
import '../../core/networking/api_client.dart';
import '../../core/services/user_service.dart';
import '../../core/services/auth_service.dart';
import '../../core/theme/app_theme.dart';

class RegisterScreen extends StatefulWidget {
  final IAuthService authService;
  final VoidCallback onRegisterSuccess;

  const RegisterScreen({
    super.key,
    required this.authService,
    required this.onRegisterSuccess,
  });

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  bool _isLoading = false;
  bool _obscurePassword = true;
  bool _obscureConfirmPassword = true;
  String? _errorMessage;
  PlatformFile? _selectedPhoto;
  List<int>? _selectedPhotoBytes;

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  Future<void> _pickPhoto() async {
    final photo = await FilePicker.pickFile(
      type: FileType.custom,
      allowedExtensions: ['jpg', 'jpeg', 'png', 'webp'],
    );
    if (photo == null || !mounted) return;
    final length = photo.lengthSync() ?? await photo.length();
    if (length == 0 || length > 2 * 1024 * 1024) {
      setState(() => _errorMessage = 'Photo must be smaller than 2 MB.');
      return;
    }
    final bytes = await photo.readAsBytes();
    if (!mounted) return;
    if (bytes.isEmpty || bytes.length > 2 * 1024 * 1024) {
      setState(() => _errorMessage = 'Photo must be smaller than 2 MB.');
      return;
    }
    setState(() {
      _selectedPhoto = photo;
      _selectedPhotoBytes = bytes;
      _errorMessage = null;
    });
  }

  Future<void> _register() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    try {
      await widget.authService.registerWithEmail(
        _emailController.text.trim(),
        _passwordController.text,
        _nameController.text.trim(),
      );
      if (_selectedPhoto != null) {
        final client = ApiClient()..setAuthService(widget.authService);
        try {
          final photo = _selectedPhoto!;
          final ext = photo.extension?.toLowerCase();
          final contentType = ext == 'png' ? 'image/png'
              : ext == 'webp' ? 'image/webp' : 'image/jpeg';
          await UserService(client).uploadMyProfilePhoto(
            bytes: _selectedPhotoBytes!, fileName: photo.name,
            contentType: contentType,
          );
        } catch (_) {
          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text(
                'Account created, but photo upload failed. Add it later in Profile.',
              )),
            );
          }
        } finally {
          client.dispose();
        }
      }
      // Firebase signs in newly registered users automatically.
      // Return to the root auth-state-driven screen. The app will show
      // onboarding or Home without leaving registration on the route stack.
      if (mounted) {
        widget.onRegisterSuccess();
        Navigator.of(context).popUntil((route) => route.isFirst);
      }
    } on AuthServiceException catch (e) {
      if (mounted) setState(() => _errorMessage = e.message);
    } catch (_) {
      if (mounted) setState(() => _errorMessage = 'Something went wrong. Please try again.');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF2F6FC),
      appBar: AppBar(title: const Text('Create Account'), backgroundColor: const Color(0xFFF2F6FC)),
      body: SafeArea(
        child: Center(child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 580),
          child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(18, 14, 18, 30),
          child: Container(
            padding: const EdgeInsets.all(24),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(24),
              boxShadow: [BoxShadow(color: const Color(0xFF163B6C).withValues(alpha: .08), blurRadius: 24, offset: const Offset(0, 9))],
            ),
          child: Form(
            key: _formKey,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Container(
                  width: 54, height: 54,
                  alignment: Alignment.center,
                  decoration: BoxDecoration(color: const Color(0xFFEAF3FF), borderRadius: BorderRadius.circular(16)),
                  child: const Icon(Icons.public, size: 30, color: AppTheme.primaryColor),
                ),
                const SizedBox(height: 16),
                Text(
                  'Join ExpatOne',
                  style: Theme.of(context).textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w800, color: const Color(0xFF142C50)),
                ),
                const SizedBox(height: 8),
                Text(
                  'Create your account to get started',
                  style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                        color: AppTheme.textSecondary,
                      ),
                ),
                const SizedBox(height: 8),
                const Text('One account. Every step of your expat journey.', style: TextStyle(color: Color(0xFF536782))),
                const SizedBox(height: 20),
                OutlinedButton.icon(
                  onPressed: _isLoading ? null : _pickPhoto,
                  icon: const Icon(Icons.add_a_photo_outlined),
                  label: Text(_selectedPhoto?.name ?? 'Add profile photo (optional)'),
                ),
                if (_selectedPhoto != null)
                  TextButton(
                    onPressed: _isLoading ? null : () => setState(() => _selectedPhoto = null),
                    child: const Text('Remove selected photo'),
                  ),
                const SizedBox(height: 24),
                if (_errorMessage != null) ...[
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: AppTheme.errorColor.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Text(
                      _errorMessage!,
                      style: TextStyle(color: AppTheme.errorColor, fontSize: 14),
                      textAlign: TextAlign.center,
                    ),
                  ),
                  const SizedBox(height: 16),
                ],
                TextFormField(
                  controller: _nameController,
                  textInputAction: TextInputAction.next,
                  textCapitalization: TextCapitalization.words,
                  decoration: const InputDecoration(
                    labelText: 'Full Name',
                    prefixIcon: Icon(Icons.person_outlined),
                  ),
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return 'Please enter your name';
                    }
                    if (value.trim().length < 2) {
                      return 'Name must be at least 2 characters';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _emailController,
                  keyboardType: TextInputType.emailAddress,
                  textInputAction: TextInputAction.next,
                  decoration: const InputDecoration(
                    labelText: 'Email',
                    prefixIcon: Icon(Icons.email_outlined),
                  ),
                  validator: (value) {
                    if (value == null || value.trim().isEmpty) {
                      return 'Please enter your email';
                    }
                    if (!RegExp(r'^[^@]+@[^@]+\.[^@]+$').hasMatch(value.trim())) {
                      return 'Please enter a valid email';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _passwordController,
                  obscureText: _obscurePassword,
                  textInputAction: TextInputAction.next,
                  decoration: InputDecoration(
                    labelText: 'Password',
                    prefixIcon: const Icon(Icons.lock_outlined),
                    suffixIcon: IconButton(
                      icon: Icon(_obscurePassword ? Icons.visibility_off : Icons.visibility),
                      onPressed: () => setState(() => _obscurePassword = !_obscurePassword),
                    ),
                  ),
                  validator: (value) {
                    if (value == null || value.isEmpty) {
                      return 'Please enter a password';
                    }
                    if (value.length < 6) {
                      return 'Password must be at least 6 characters';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 16),
                TextFormField(
                  controller: _confirmPasswordController,
                  obscureText: _obscureConfirmPassword,
                  textInputAction: TextInputAction.done,
                  onFieldSubmitted: (_) => _register(),
                  decoration: InputDecoration(
                    labelText: 'Confirm Password',
                    prefixIcon: const Icon(Icons.lock_outlined),
                    suffixIcon: IconButton(
                      icon: Icon(_obscureConfirmPassword ? Icons.visibility_off : Icons.visibility),
                      onPressed: () =>
                          setState(() => _obscureConfirmPassword = !_obscureConfirmPassword),
                    ),
                  ),
                  validator: (value) {
                    if (value == null || value.isEmpty) {
                      return 'Please confirm your password';
                    }
                    if (value != _passwordController.text) {
                      return 'Passwords do not match';
                    }
                    return null;
                  },
                ),
                const SizedBox(height: 24),
                SizedBox(
                  height: 50,
                  child: ElevatedButton(
                    onPressed: _isLoading ? null : _register,
                    child: _isLoading
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                          )
                        : const Text('Create Account', style: TextStyle(fontSize: 16)),
                  ),
                ),
                const SizedBox(height: 16),
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Text(
                      'Already have an account?',
                      style: TextStyle(color: AppTheme.textSecondary),
                    ),
                    TextButton(
                      onPressed: _isLoading ? null : () => Navigator.pop(context),
                      child: const Text('Sign In'),
                    ),
                  ],
                ),
              ],
            ),
          )),
        )),
      ),
    );
  }
}
