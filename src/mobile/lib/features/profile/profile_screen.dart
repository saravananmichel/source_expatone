import 'package:flutter/material.dart';
import '../../core/services/auth_service.dart';
import '../../core/services/user_service.dart';
import '../../core/theme/app_theme.dart';

class ProfileScreen extends StatefulWidget {
  final IAuthService authService;
  final UserService userService;
  final VoidCallback onLogout;

  const ProfileScreen({
    super.key,
    required this.authService,
    required this.userService,
    required this.onLogout,
  });

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  UserProfile? _profile;
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadProfile();
  }

  Future<void> _loadProfile() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    try {
      final profile = await widget.userService.getMe();
      if (mounted) {
        setState(() {
          _profile = profile;
          _isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = 'Unable to load profile';
          _isLoading = false;
        });
      }
    }
  }

  Future<void> _logout() async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Sign Out'),
        content: const Text('Are you sure you want to sign out?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Sign Out'),
          ),
        ],
      ),
    );

    if (confirm == true) {
      await widget.authService.signOut();
      if (mounted) widget.onLogout();
    }
  }

  void _showEditDialog() {
    if (_profile == null) return;

    final nameController = TextEditingController(text: _profile!.displayName ?? '');
    final phoneController = TextEditingController(text: _profile!.phoneNumber ?? '');

    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Edit Profile'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: nameController,
              decoration: const InputDecoration(labelText: 'Display Name'),
              textCapitalization: TextCapitalization.words,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: phoneController,
              decoration: const InputDecoration(labelText: 'Phone Number'),
              keyboardType: TextInputType.phone,
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () async {
              final messenger = ScaffoldMessenger.of(context);
              Navigator.pop(context);
              try {
                final updated = await widget.userService.updateMe(
                  displayName: nameController.text.trim(),
                  phoneNumber: phoneController.text.trim(),
                );
                if (mounted) setState(() => _profile = updated);
              } catch (_) {
                if (mounted) {
                  messenger.showSnackBar(
                    const SnackBar(content: Text('Failed to update profile')),
                  );
                }
              }
            },
            child: const Text('Save'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Profile'),
        actions: [
          if (_profile != null)
            IconButton(
              icon: const Icon(Icons.edit_outlined),
              onPressed: _showEditDialog,
            ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(_error!, style: TextStyle(color: AppTheme.errorColor)),
                      const SizedBox(height: 12),
                      ElevatedButton(onPressed: _loadProfile, child: const Text('Retry')),
                    ],
                  ),
                )
              : _buildProfileContent(),
    );
  }

  Widget _buildProfileContent() {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Center(
          child: CircleAvatar(
            radius: 48,
            backgroundColor: AppTheme.primaryColor.withValues(alpha: 0.1),
            child: Text(
              (_profile!.displayName?.isNotEmpty == true)
                  ? _profile!.displayName![0].toUpperCase()
                  : (_profile!.email[0].toUpperCase()),
              style: TextStyle(
                fontSize: 36,
                fontWeight: FontWeight.bold,
                color: AppTheme.primaryColor,
              ),
            ),
          ),
        ),
        const SizedBox(height: 16),
        Text(
          _profile!.displayName ?? 'ExpatOne User',
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.headlineMedium,
        ),
        const SizedBox(height: 4),
        Text(
          _profile!.email,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                color: AppTheme.textSecondary,
              ),
        ),
        const SizedBox(height: 32),
        _buildInfoTile(Icons.email_outlined, 'Email', _profile!.email),
        _buildInfoTile(Icons.person_outlined, 'Name', _profile!.displayName ?? 'Not set'),
        _buildInfoTile(Icons.phone_outlined, 'Phone', _profile!.phoneNumber ?? 'Not set'),
        _buildInfoTile(Icons.flag_outlined, 'Country', _profile!.countryCode),
        _buildInfoTile(Icons.language_outlined, 'Language', _profile!.preferredLanguage),
        _buildInfoTile(Icons.security_outlined, 'Auth Provider',
            _profile!.externalProvider.substring(0, 1).toUpperCase() +
                _profile!.externalProvider.substring(1)),
        const SizedBox(height: 32),
        OutlinedButton.icon(
          onPressed: _logout,
          icon: const Icon(Icons.logout, color: AppTheme.errorColor),
          label: const Text('Sign Out', style: TextStyle(color: AppTheme.errorColor)),
          style: OutlinedButton.styleFrom(
            side: BorderSide(color: AppTheme.errorColor.withValues(alpha: 0.5)),
            padding: const EdgeInsets.symmetric(vertical: 14),
          ),
        ),
      ],
    );
  }

  Widget _buildInfoTile(IconData icon, String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          Icon(icon, size: 20, color: AppTheme.textSecondary),
          const SizedBox(width: 12),
          Text(label, style: TextStyle(color: AppTheme.textSecondary, fontSize: 14)),
          const Spacer(),
          Text(value, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500)),
        ],
      ),
    );
  }
}
