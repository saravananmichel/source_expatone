import 'package:flutter/material.dart';

import '../../features/translator/translation_screen.dart';
import '../../features/family/family_screen.dart';
import '../../features/emergency/emergency_screen.dart';
import '../../features/corporate/corporate_screen.dart';
import '../../features/applications/application_assistant_screen.dart';
import '../../core/services/corporate_service.dart';
import '../../core/services/destination_service.dart';
import '../../core/networking/api_client.dart';
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
  int _documentRefresh = 0;
  int _reminderRefresh = 0;

  void _navigateToTab(int index) {
    if (index >= 0 && index < 5) {
      setState(() {
        _currentIndex = index;
        if (index == 1) _documentRefresh++;
        if (index == 3) _reminderRefresh++;
      });
    }
  }

  void _openDashboardFeature(DashboardFeature feature) {
    late final Widget screen;

    switch (feature) {
      case DashboardFeature.translation:
        screen = TranslationScreen(
          translationService: widget.translationService,
        );
        break;
      case DashboardFeature.visaAssistant:
        screen = ApplicationAssistantScreen(
          documentService: widget.documentService,
        );
        break;
      case DashboardFeature.family:
        screen = FamilyScreen(documentService: widget.documentService);
        break;
      case DashboardFeature.emergency:
        screen = EmergencyScreen(emergencyService: widget.emergencyService);
        break;
      case DashboardFeature.corporate:
        final api = ApiClient()..setAuthService(widget.authService);
        screen = CorporateScreen(service: CorporateService(api));
        break;
    }

    Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => screen));
  }

  @override
  Widget build(BuildContext context) {
    final screens = [
      DashboardScreen(
        healthService: widget.healthService,
        userService: widget.userService,
        reminderService: widget.reminderService,
        destinationService: DestinationService(
          ApiClient()..setAuthService(widget.authService),
        ),
        userName: widget.userName,
        onNavigateToTab: _navigateToTab,
        onOpenFeature: _openDashboardFeature,
      ),
      DocumentsScreen(
        key: ValueKey(_documentRefresh),
        documentService: widget.documentService,
        reminderService: widget.reminderService,
      ),
      AssistantScreen(assistantService: widget.assistantService),
      RemindersScreen(
        key: ValueKey('reminders-$_reminderRefresh'),
        reminderService: widget.reminderService,
      ),
      MoreScreen(
        documentService: widget.documentService,
        authService: widget.authService,
        userService: widget.userService,
        translationService: widget.translationService,
        emergencyService: widget.emergencyService,
        onLogout: widget.onLogout,
      ),
    ];

    return Scaffold(
      body: IndexedStack(index: _currentIndex, children: screens),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _currentIndex,
        onDestinationSelected: _navigateToTab,
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
