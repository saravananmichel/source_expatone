import 'package:flutter/material.dart';
import '../../core/services/reminder_service.dart';
import '../../core/services/user_service.dart';
import '../../core/theme/app_theme.dart';
import '../../shared/services/health_service.dart';
import '../../core/error/result.dart';

// Dashboard-local design tokens
const _navy = Color(0xFF1A2236);
const _accentBlue = Color(0xFF1565C0);
const _sosRed = Color(0xFFD32F2F);
const _pageBg = Color(0xFFF0F4FB);
const _greetingGradientA = Color(0xFFD6E4FF);
const _greetingGradientB = Color(0xFFEEF4FF);
const _healthyBg = Color(0xFFE8F5EC);
const _healthyText = Color(0xFF2E7D32);
const _degradedBg = Color(0xFFFFF3E0);
const _degradedText = Color(0xFFE65100);
const _passBg = Color(0xFFE3EEFF);
const _qaDocBg = Color(0xFFE3EEFF);
const _qaAssistBg = Color(0xFFEEE3FF);
const _qaReminderBg = Color(0xFFE3F5EE);
const _qaEmergencyBg = Color(0xFFFFF0EC);
const _qaAssistIcon = Color(0xFF7B3FBF);
const _qaReminderIcon = Color(0xFF2E7D32);
const _qaEmergencyIcon = Color(0xFFD84315);

class DashboardScreen extends StatefulWidget {
  final HealthService healthService;
  final UserService userService;
  final ReminderService reminderService;
  final String? userName;
  final ValueChanged<int>? onNavigateToTab;

  const DashboardScreen({
    super.key,
    required this.healthService,
    required this.userService,
    required this.reminderService,
    this.userName,
    this.onNavigateToTab,
  });

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  HealthStatus? _healthStatus;
  bool _isLoading = true;
  String? _healthError;
  UserProfile? _profile;
  List<ChecklistItem> _checklist = [];

  @override
  void initState() {
    super.initState();
    _loadAll();
  }

  Future<void> _loadAll() async {
    setState(() {
      _isLoading = true;
      _healthError = null;
    });

    await Future.wait([
      _checkHealth(),
      _loadProfile(),
      _loadChecklist(),
    ]);

    if (mounted) setState(() => _isLoading = false);
  }

  Future<void> _checkHealth() async {
    final result = await widget.healthService.checkHealth();
    if (!mounted) return;
    switch (result) {
      case Success<HealthStatus>(:final data):
        _healthStatus = data;
      case Failure<HealthStatus>(:final message):
        _healthError = message;
    }
  }

  Future<void> _loadProfile() async {
    try {
      _profile = await widget.userService.getMe();
    } catch (_) {}
  }

  Future<void> _loadChecklist() async {
    try {
      _checklist = await widget.userService.getOnboardingChecklist();
    } catch (_) {}
  }

  String get _firstName {
    final name = widget.userName ?? _profile?.displayName;
    if (name == null || name.isEmpty) return '';
    return name.split(' ').first;
  }

  String get _timeGreeting {
    final hour = DateTime.now().hour;
    if (hour < 12) return 'Good morning';
    if (hour < 17) return 'Good afternoon';
    return 'Good evening';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: _pageBg,
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: _loadAll,
          child: ListView(
            padding: EdgeInsets.zero,
            children: [
              _buildHeader(),
              _buildGreeting(),
              const SizedBox(height: 20),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _buildHealthCard(),
                    const SizedBox(height: 12),
                    _buildPassCard(),
                    if (_checklist.isNotEmpty) ...[
                      const SizedBox(height: 28),
                      _buildChecklistSection(),
                    ],
                    const SizedBox(height: 28),
                    _buildQuickActions(),
                    const SizedBox(height: 32),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  // ─── Header ───────────────────────────────────────────────────────────────

  Widget _buildHeader() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 20, 16, 0),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          // Branding — flexible so it never forces overflow on narrow screens
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                RichText(
                  text: const TextSpan(
                    children: [
                      TextSpan(
                        text: 'Expat',
                        style: TextStyle(
                          fontSize: 24,
                          fontWeight: FontWeight.bold,
                          color: _navy,
                          letterSpacing: -0.3,
                        ),
                      ),
                      TextSpan(
                        text: 'One',
                        style: TextStyle(
                          fontSize: 24,
                          fontWeight: FontWeight.bold,
                          color: _accentBlue,
                          letterSpacing: -0.3,
                        ),
                      ),
                    ],
                  ),
                ),
                const Text(
                  'Your Life Abroad, Simplified',
                  style: TextStyle(
                    fontSize: 11,
                    color: AppTheme.textSecondary,
                    letterSpacing: 0.1,
                  ),
                ),
              ],
            ),
          ),
          // Notification bell with dot
          SizedBox(
            width: 40,
            height: 40,
            child: Stack(
              children: [
                Align(
                  alignment: Alignment.center,
                  child: Icon(Icons.notifications_outlined, color: _navy, size: 24),
                ),
                Positioned(
                  right: 5,
                  top: 5,
                  child: Container(
                    width: 8,
                    height: 8,
                    decoration: BoxDecoration(
                      color: Colors.orange,
                      shape: BoxShape.circle,
                      border: Border.all(color: _pageBg, width: 1.5),
                    ),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          // SOS pill button
          GestureDetector(
            onTap: () => widget.onNavigateToTab?.call(4),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
              decoration: BoxDecoration(
                color: _sosRed,
                borderRadius: BorderRadius.circular(24),
              ),
              child: const Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.phone, color: Colors.white, size: 14),
                  SizedBox(width: 4),
                  Text(
                    'SOS',
                    style: TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                      letterSpacing: 0.5,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  // ─── Greeting ─────────────────────────────────────────────────────────────

  Widget _buildGreeting() {
    final name = _firstName;
    final greeting = name.isNotEmpty ? '$_timeGreeting, $name 👋' : '$_timeGreeting 👋';
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 16, 16, 0),
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 24),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [_greetingGradientA, _greetingGradientB],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            greeting,
            style: const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.bold,
              color: _navy,
              height: 1.2,
            ),
          ),
          const SizedBox(height: 6),
          const Text(
            "Here's what's happening with your expat journey today.",
            style: TextStyle(
              fontSize: 13,
              color: AppTheme.textSecondary,
              height: 1.4,
            ),
          ),
        ],
      ),
    );
  }

  // ─── Health card ──────────────────────────────────────────────────────────

  Widget _buildHealthCard() {
    if (_isLoading) {
      return _DashboardHealthCard(
        isLoading: true,
        label: 'Backend: Checking...',
        subtitle: 'Connecting to services',
        bg: const Color(0xFFF5F5F5),
        dotColor: AppTheme.textSecondary,
        labelColor: AppTheme.textSecondary,
      );
    }
    if (_healthError != null) {
      return _DashboardHealthCard(
        isLoading: false,
        label: 'Backend: Unavailable',
        subtitle: _healthError!,
        bg: _degradedBg,
        dotColor: AppTheme.warningColor,
        labelColor: _degradedText,
        onRetry: _loadAll,
      );
    }
    final isHealthy = _healthStatus?.isHealthy ?? false;
    return _DashboardHealthCard(
      isLoading: false,
      label: isHealthy ? 'Backend: Healthy' : 'Backend: Degraded',
      subtitle: isHealthy ? 'All systems operational' : 'Some services may be limited',
      bg: isHealthy ? _healthyBg : _degradedBg,
      dotColor: isHealthy ? AppTheme.successColor : AppTheme.warningColor,
      labelColor: isHealthy ? _healthyText : _degradedText,
    );
  }

  // ─── Employment Pass card ─────────────────────────────────────────────────

  Widget _buildPassCard() {
    final passLabel = _profile?.visaPassType?.replaceAllMapped(
          RegExp(r'([a-z])([A-Z])'),
          (m) => '${m.group(1)} ${m.group(2)}',
        ) ??
        'Employment Pass';

    return Container(
      decoration: BoxDecoration(
        color: _passBg,
        borderRadius: BorderRadius.circular(16),
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(16),
        onTap: () => widget.onNavigateToTab?.call(1),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(11),
                decoration: BoxDecoration(
                  color: _accentBlue,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: const Icon(Icons.description, color: Colors.white, size: 24),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      passLabel,
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 15,
                        color: _navy,
                      ),
                    ),
                    const SizedBox(height: 2),
                    const Text(
                      'Manage your visa and work documents',
                      style: TextStyle(fontSize: 12, color: AppTheme.textSecondary),
                    ),
                  ],
                ),
              ),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: const [
                  Text(
                    'View',
                    style: TextStyle(
                      color: _accentBlue,
                      fontWeight: FontWeight.w600,
                      fontSize: 14,
                    ),
                  ),
                  SizedBox(width: 2),
                  Icon(Icons.chevron_right, color: _accentBlue, size: 20),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  // ─── Checklist ────────────────────────────────────────────────────────────

  Widget _buildChecklistSection() {
    final completed = _checklist.where((c) => c.isCompleted).length;
    final total = _checklist.length;
    final progress = total > 0 ? completed / total : 0.0;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.center,
          children: [
            const Expanded(
              child: Text(
                'Your Checklist',
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.bold,
                  color: _navy,
                ),
              ),
            ),
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  '$completed/$total completed',
                  style: const TextStyle(fontSize: 12, color: AppTheme.textSecondary),
                ),
                const SizedBox(height: 5),
                SizedBox(
                  width: 80,
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(4),
                    child: LinearProgressIndicator(
                      value: progress,
                      backgroundColor: const Color(0xFFDDDDDD),
                      color: AppTheme.successColor,
                      minHeight: 6,
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
        const SizedBox(height: 14),
        ..._checklist.map(
          (item) => Padding(
            padding: const EdgeInsets.only(bottom: 10),
            child: _buildChecklistCard(item),
          ),
        ),
      ],
    );
  }

  Widget _buildChecklistCard(ChecklistItem item) {
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFEAEAEA)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.03),
            blurRadius: 6,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: () {
          if (item.action != null) {
            final tab = _actionToTab(item.action!);
            if (tab != null) widget.onNavigateToTab?.call(tab);
          }
        },
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Container(
                width: 26,
                height: 26,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: item.isCompleted ? AppTheme.successColor : Colors.transparent,
                  border: item.isCompleted
                      ? null
                      : Border.all(color: const Color(0xFFBBBBBB), width: 1.5),
                ),
                child: item.isCompleted
                    ? const Icon(Icons.check, color: Colors.white, size: 15)
                    : null,
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      item.title,
                      style: TextStyle(
                        fontWeight: FontWeight.w600,
                        fontSize: 14,
                        color: item.isCompleted ? AppTheme.textSecondary : _navy,
                        decoration: item.isCompleted ? TextDecoration.lineThrough : null,
                        decorationColor: AppTheme.textSecondary,
                        decorationThickness: 1.5,
                      ),
                    ),
                    const SizedBox(height: 3),
                    Text(
                      item.description,
                      style: const TextStyle(
                        fontSize: 12,
                        color: AppTheme.textSecondary,
                        height: 1.35,
                      ),
                    ),
                  ],
                ),
              ),
              if (item.action != null) ...[
                const SizedBox(width: 6),
                const Icon(Icons.chevron_right, size: 18, color: AppTheme.textSecondary),
              ],
            ],
          ),
        ),
      ),
    );
  }

  int? _actionToTab(String action) {
    return switch (action) {
      'documents' => 1,
      'assistant' => 2,
      'reminders' => 3,
      'profile' => 4,
      _ => null,
    };
  }

  // ─── Quick actions ────────────────────────────────────────────────────────

  Widget _buildQuickActions() {
    return Row(
      children: [
        _QuickActionCard(
          icon: Icons.note_add_outlined,
          label: 'Add\nDocument',
          bgColor: _qaDocBg,
          iconColor: _accentBlue,
          onTap: () => widget.onNavigateToTab?.call(1),
        ),
        const SizedBox(width: 10),
        _QuickActionCard(
          icon: Icons.auto_awesome_outlined,
          label: 'Ask\nAssistant',
          bgColor: _qaAssistBg,
          iconColor: _qaAssistIcon,
          onTap: () => widget.onNavigateToTab?.call(2),
        ),
        const SizedBox(width: 10),
        _QuickActionCard(
          icon: Icons.calendar_month_outlined,
          label: 'View\nReminders',
          bgColor: _qaReminderBg,
          iconColor: _qaReminderIcon,
          onTap: () => widget.onNavigateToTab?.call(3),
        ),
        const SizedBox(width: 10),
        _QuickActionCard(
          icon: Icons.location_on_outlined,
          label: 'Emergency\nHelp',
          bgColor: _qaEmergencyBg,
          iconColor: _qaEmergencyIcon,
          onTap: () => widget.onNavigateToTab?.call(4),
        ),
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Supporting widgets
// ─────────────────────────────────────────────────────────────────────────────

class _DashboardHealthCard extends StatelessWidget {
  final bool isLoading;
  final String label;
  final String subtitle;
  final Color bg;
  final Color dotColor;
  final Color labelColor;
  final VoidCallback? onRetry;

  const _DashboardHealthCard({
    required this.isLoading,
    required this.label,
    required this.subtitle,
    required this.bg,
    required this.dotColor,
    required this.labelColor,
    this.onRetry,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          if (isLoading)
            SizedBox(
              width: 14,
              height: 14,
              child: CircularProgressIndicator(strokeWidth: 2, color: dotColor),
            )
          else
            Container(
              width: 14,
              height: 14,
              decoration: BoxDecoration(color: dotColor, shape: BoxShape.circle),
            ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 14,
                    color: labelColor,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  subtitle,
                  style: TextStyle(
                    fontSize: 12,
                    color: labelColor.withValues(alpha: 0.75),
                  ),
                ),
              ],
            ),
          ),
          if (onRetry != null)
            GestureDetector(
              onTap: onRetry,
              child: Icon(Icons.refresh, size: 18, color: labelColor),
            )
          else
            Icon(Icons.chevron_right, size: 18, color: labelColor),
        ],
      ),
    );
  }
}

class _QuickActionCard extends StatelessWidget {
  final IconData icon;
  final String label;
  final Color bgColor;
  final Color iconColor;
  final VoidCallback onTap;

  const _QuickActionCard({
    required this.icon,
    required this.label,
    required this.bgColor,
    required this.iconColor,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Expanded(
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 16, horizontal: 6),
          decoration: BoxDecoration(
            color: bgColor,
            borderRadius: BorderRadius.circular(14),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(icon, color: iconColor, size: 28),
              const SizedBox(height: 8),
              Text(
                label,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w500,
                  color: _navy,
                  height: 1.3,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
