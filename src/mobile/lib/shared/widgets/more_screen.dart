import 'package:flutter/material.dart';
import '../../core/services/auth_service.dart';
import '../../core/services/emergency_service.dart';
import '../../core/services/translation_service.dart';
import '../../core/services/user_service.dart';
import '../../core/theme/app_theme.dart';
import '../../features/emergency/emergency_screen.dart';
import '../../features/profile/profile_screen.dart';
import '../../features/translator/translation_screen.dart';

class MoreScreen extends StatelessWidget {
  final IAuthService authService;
  final UserService userService;
  final TranslationService translationService;
  final EmergencyService emergencyService;
  final VoidCallback onLogout;

  const MoreScreen({
    super.key,
    required this.authService,
    required this.userService,
    required this.translationService,
    required this.emergencyService,
    required this.onLogout,
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('More'),
        centerTitle: false,
      ),
      body: ListView(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        children: [
          // Emergency is safety-critical — displayed at the top with distinct styling
          _EmergencyCard(
            onTap: () => Navigator.push(
              context,
              MaterialPageRoute(
                builder: (_) => EmergencyScreen(
                  emergencyService: emergencyService,
                ),
              ),
            ),
          ),
          const SizedBox(height: 16),
          // Standard menu items
          _MenuTile(
            icon: Icons.translate,
            iconColor: AppTheme.primaryColor,
            title: 'Translation',
            subtitle: 'Translate text between 8 languages',
            onTap: () => Navigator.push(
              context,
              MaterialPageRoute(
                builder: (_) => TranslationScreen(
                  translationService: translationService,
                ),
              ),
            ),
          ),
          const Divider(height: 1, indent: 56),
          _MenuTile(
            icon: Icons.person_outline,
            iconColor: AppTheme.primaryColor,
            title: 'Profile',
            subtitle: 'Manage your ExpatOne profile',
            onTap: () => Navigator.push(
              context,
              MaterialPageRoute(
                builder: (_) => ProfileScreen(
                  authService: authService,
                  userService: userService,
                  onLogout: onLogout,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _EmergencyCard extends StatelessWidget {
  final VoidCallback onTap;

  const _EmergencyCard({required this.onTap});

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 0,
      color: AppTheme.errorColor.withValues(alpha: 0.08),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(12),
        side: BorderSide(color: AppTheme.errorColor.withValues(alpha: 0.25)),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: AppTheme.errorColor.withValues(alpha: 0.12),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(Icons.emergency, color: AppTheme.errorColor, size: 24),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Emergency',
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w600,
                        color: AppTheme.errorColor,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      'Emergency assistance and essential contacts',
                      style: TextStyle(
                        fontSize: 13,
                        color: AppTheme.errorColor.withValues(alpha: 0.8),
                      ),
                    ),
                  ],
                ),
              ),
              Icon(Icons.chevron_right, color: AppTheme.errorColor.withValues(alpha: 0.7)),
            ],
          ),
        ),
      ),
    );
  }
}

class _MenuTile extends StatelessWidget {
  final IconData icon;
  final Color iconColor;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  const _MenuTile({
    required this.icon,
    required this.iconColor,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 14),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: iconColor.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Icon(icon, color: iconColor, size: 22),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w500),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    style: const TextStyle(fontSize: 13, color: AppTheme.textSecondary),
                  ),
                ],
              ),
            ),
            const Icon(Icons.chevron_right, color: AppTheme.textSecondary, size: 20),
          ],
        ),
      ),
    );
  }
}
