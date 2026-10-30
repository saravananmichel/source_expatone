import 'package:flutter/material.dart';
import '../../features/profile/profile_screen.dart';
import 'profile_avatar.dart';
import 'language_region_screen.dart';
import 'settings_screen.dart';

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
  int _dashboardRefresh = 0;
  int _documentRefresh = 0;
  int _reminderRefresh = 0;
  String? _avatarUrl;

  @override
  void initState() {
    super.initState();
    _loadAvatar();
  }

  Future<void> _loadAvatar() async {
    try {
      final url = await widget.userService.getMyProfilePhotoUrl();
      if (mounted) setState(() => _avatarUrl = url);
    } catch (_) {}
  }

  Future<void> _signOut() async {
    await widget.authService.signOut();
    if (!mounted) return;
    widget.onLogout();
    Navigator.of(context).popUntil((route) => route.isFirst);
  }

  void _openSettings() {
    Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const SettingsScreen()));
  }

  Future<void> _openLanguageRegion() async {
    final saved = await Navigator.of(context).push<bool>(MaterialPageRoute<bool>(
      builder: (_) => LanguageRegionScreen(
        userService: widget.userService,
        destinationService: DestinationService(ApiClient()..setAuthService(widget.authService)),
      ),
    ));
    if (saved == true && mounted) {
      // Reload profile and destination banner with the newly selected region.
      setState(() => _dashboardRefresh++);
    }
  }

  void _openProfile() {
    Navigator.of(context).push(MaterialPageRoute<void>(
      builder: (_) => ProfileScreen(
        authService: widget.authService,
        userService: widget.userService,
        onLogout: widget.onLogout,
      ),
    )).then((_) => _loadAvatar());
  }


  void _navigateToTab(int index) {
    if (index >= 0 && index < 5) {
      setState(() {
        _currentIndex = index;
        if (index == 1) _documentRefresh++;
        if (index == 3) _reminderRefresh++;
      });
    }
  }

  void _openNestedFeature(Widget screen) {
    Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => Scaffold(
      appBar: AppBar(
        title: const Text('ExpatOne'),
        actions: [
          IconButton(tooltip: 'Reminders', icon: const Icon(Icons.notifications_outlined),
            onPressed: () { Navigator.of(context).popUntil((route) => route.isFirst); _navigateToTab(3); }),
          PopupMenuButton<String>(tooltip: 'Account menu',
            onSelected: (value) {
              Navigator.of(context).popUntil((route) => route.isFirst);
              switch (value) {
                case 'profile': _openProfile();
                case 'settings': _openSettings();
                case 'language': _openLanguageRegion();
                case 'logout': _signOut();
              }
            },
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'profile', child: Text('My Profile')),
              PopupMenuItem(value: 'settings', child: Text('Settings & preferences')),
              PopupMenuItem(value: 'language', child: Text('Language & region')),
              PopupMenuDivider(),
              PopupMenuItem(value: 'logout', child: Text('Logout')),
            ],
            icon: const Icon(Icons.account_circle_outlined),
          ),
        ],
      ),
      body: screen,
      bottomNavigationBar: NavigationBar(
        selectedIndex: _currentIndex,
        onDestinationSelected: (index) {
          Navigator.of(context).popUntil((route) => route.isFirst);
          _navigateToTab(index);
        },
        destinations: const [
          NavigationDestination(icon: Icon(Icons.home_outlined), label: 'Home'),
          NavigationDestination(icon: Icon(Icons.folder_outlined), label: 'Documents'),
          NavigationDestination(icon: Icon(Icons.account_balance_outlined), label: 'Assistant'),
          NavigationDestination(icon: Icon(Icons.notifications_outlined), label: 'Reminders'),
          NavigationDestination(icon: Icon(Icons.more_horiz), label: 'More'),
        ],
      ),
    )));
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
        screen = FamilyScreen(documentService: widget.documentService, onOpenNested: _openNestedFeature);
        break;
      case DashboardFeature.emergency:
        screen = EmergencyScreen(emergencyService: widget.emergencyService);
        break;
      case DashboardFeature.corporate:
        final api = ApiClient()..setAuthService(widget.authService);
        screen = CorporateScreen(service: CorporateService(api));
        break;
    }

    Navigator.of(context).push(MaterialPageRoute<void>(
      builder: (_) => Scaffold(
        body: Column(children: [
          Material(
            color: Colors.white,
            child: SafeArea(bottom: false, child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
              child: Row(children: [
                const Icon(Icons.public, color: AppTheme.primaryColor),
                const SizedBox(width: 8),
                const Expanded(child: Text('ExpatOne',
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18))),
                IconButton(
                  tooltip: 'Reminders',
                  icon: const Icon(Icons.notifications_outlined),
                  onPressed: () {
                    Navigator.of(context).pop();
                    _navigateToTab(3);
                  },
                ),
                PopupMenuButton<String>(
                  tooltip: 'Account menu',
                  onSelected: (value) {
                    Navigator.of(context).pop();
                    switch (value) {
                      case 'profile':
                        _openProfile();
                      case 'settings':
                        _openSettings();
                      case 'language':
                        _openLanguageRegion();
                      case 'logout':
                        _signOut();
                    }
                  },
                  itemBuilder: (_) => const [
                    PopupMenuItem(value: 'profile', child: Text('My Profile')),
                    PopupMenuItem(value: 'settings', child: Text('Settings & preferences')),
                    PopupMenuItem(value: 'language', child: Text('Language & region')),
                    PopupMenuDivider(),
                    PopupMenuItem(value: 'logout', child: Text('Logout')),
                  ],
                  child: ProfileAvatar(imageUrl: _avatarUrl,
                    displayName: widget.userName, radius: 17),
                ),
              ]),
            )),
          ),
          Expanded(child: screen),
        ]),
        bottomNavigationBar: NavigationBar(
          selectedIndex: _currentIndex,
          onDestinationSelected: (index) {
            Navigator.of(context).pop();
            _navigateToTab(index);
          },
          destinations: const [
            NavigationDestination(icon: Icon(Icons.home_outlined), label: 'Home'),
            NavigationDestination(icon: Icon(Icons.folder_outlined), label: 'Documents'),
            NavigationDestination(icon: Icon(Icons.account_balance_outlined), label: 'Assistant'),
            NavigationDestination(icon: Icon(Icons.notifications_outlined), label: 'Reminders'),
            NavigationDestination(icon: Icon(Icons.more_horiz), label: 'More'),
          ],
        ),
      ),
    ));
  }

  @override
  Widget build(BuildContext context) {
    final screens = [
      DashboardScreen(
        key: ValueKey('dashboard-$_dashboardRefresh'),
        healthService: widget.healthService,
        userService: widget.userService,
        reminderService: widget.reminderService,
        destinationService: DestinationService(
          ApiClient()..setAuthService(widget.authService),
        ),
        userName: widget.userName,
        showHeader: false,
        onNavigateToTab: _navigateToTab,
        onOpenFeature: _openDashboardFeature,
      ),
      DocumentsScreen(
        key: ValueKey(_documentRefresh),
        documentService: widget.documentService,
        reminderService: widget.reminderService,
        onOpenNested: _openNestedFeature,
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
        onOpenFeature: _openDashboardFeature,
      ),
    ];

    return Scaffold(
      appBar: AppBar(
        automaticallyImplyLeading: false,
        backgroundColor: Colors.white,
        title: const Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.public, color: AppTheme.primaryColor),
            SizedBox(width: 8),
            Text('ExpatOne', style: TextStyle(fontWeight: FontWeight.bold)),
          ],
        ),
        actions: [
          IconButton(
            tooltip: 'Reminders',
            onPressed: () => _navigateToTab(3),
            icon: const Icon(Icons.notifications_outlined),
          ),
          PopupMenuButton<String>(
            tooltip: 'Account menu',
            onSelected: (value) {
              switch (value) {
                case 'profile':
                  _openProfile();
                case 'settings':
                  _openSettings();
                case 'language':
                  _openLanguageRegion();
                case 'logout':
                  _signOut();
              }
            },
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'profile', child: Text('My Profile')),
              PopupMenuItem(value: 'settings', child: Text('Settings & preferences')),
              PopupMenuItem(value: 'language', child: Text('Language & region')),
              PopupMenuDivider(),
              PopupMenuItem(value: 'logout', child: Text('Logout')),
            ],
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  ProfileAvatar(imageUrl: _avatarUrl, displayName: widget.userName, radius: 17),
                  const SizedBox(width: 6),
                  const Icon(Icons.keyboard_arrow_down),
                ],
              ),
            ),
          ),
        ],
      ),
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
