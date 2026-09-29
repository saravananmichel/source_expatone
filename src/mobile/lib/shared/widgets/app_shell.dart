import 'package:flutter/material.dart';
import '../../core/services/assistant_service.dart';
import '../../core/services/auth_service.dart';
import '../../core/services/document_service.dart';
import '../../core/services/emergency_service.dart';
import '../../core/services/reminder_service.dart';
import '../../core/services/translation_service.dart';
import '../../core/services/user_service.dart';
import '../../core/theme/app_theme.dart';
import '../../features/assistant/assistant_screen.dart';
import '../../features/dashboard/dashboard_screen.dart';
import '../../features/documents/documents_screen.dart';
import '../../features/reminders/reminders_screen.dart';
import '../services/health_service.dart';
import 'more_screen.dart';

class AppShell extends StatefulWidget {
  final HealthService healthService;
  final IAuthService authService;
  final UserService userService;
  final DocumentService documentService;
  final ReminderService reminderService;
  final AssistantService assistantService;
  final TranslationService translationService;
  final EmergencyService emergencyService;
  final VoidCallback onLogout;
  final String? userName;

  const AppShell({
    super.key,
    required this.healthService,
    required this.authService,
    required this.userService,
    required this.documentService,
    required this.reminderService,
    required this.assistantService,
    required this.translationService,
    required this.emergencyService,
    required this.onLogout,
    this.userName,
  });

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  int _currentIndex = 0;

  void _navigateToTab(int index) {
    if (index >= 0 && index < 5) {
      setState(() => _currentIndex = index);
    }
  }

  @override
  Widget build(BuildContext context) {
    final screens = [
      DashboardScreen(
        healthService: widget.healthService,
        userService: widget.userService,
        reminderService: widget.reminderService,
        userName: widget.userName,
        onNavigateToTab: _navigateToTab,
      ),
      DocumentsScreen(
        documentService: widget.documentService,
        reminderService: widget.reminderService,
      ),
      AssistantScreen(assistantService: widget.assistantService),
      RemindersScreen(reminderService: widget.reminderService),
      MoreScreen(
        authService: widget.authService,
        userService: widget.userService,
        translationService: widget.translationService,
        emergencyService: widget.emergencyService,
        onLogout: widget.onLogout,
      ),
    ];

    return Scaffold(
      body: IndexedStack(
        index: _currentIndex,
        children: screens,
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _currentIndex,
        onDestinationSelected: (index) => setState(() => _currentIndex = index),
        backgroundColor: Colors.white,
        indicatorColor: AppTheme.primaryColor.withValues(alpha: 0.12),
        labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.home_outlined),
            selectedIcon: Icon(Icons.home),
            label: 'Home',
          ),
          NavigationDestination(
            icon: Icon(Icons.folder_outlined),
            selectedIcon: Icon(Icons.folder),
            label: 'Documents',
          ),
          NavigationDestination(
            icon: Icon(Icons.account_balance_outlined),
            selectedIcon: Icon(Icons.account_balance),
            label: 'Assistant',
          ),
          NavigationDestination(
            icon: Icon(Icons.notifications_outlined),
            selectedIcon: Icon(Icons.notifications),
            label: 'Reminders',
          ),
          NavigationDestination(
            icon: Icon(Icons.more_horiz_outlined),
            selectedIcon: Icon(Icons.more_horiz),
            label: 'More',
          ),
        ],
      ),
    );
  }
}
