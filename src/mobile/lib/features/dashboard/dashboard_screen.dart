import 'package:flutter/material.dart';

import 'widgets/destination_banner.dart';

import '../../core/services/reminder_service.dart';
import '../../core/services/destination_service.dart';
import '../../core/services/user_service.dart';
import '../../core/theme/app_theme.dart';
import '../../shared/services/health_service.dart';
import '../../core/error/result.dart';

// Dashboard-local design tokens
const _navy = Color(0xFF1A2236);
const _accentBlue = Color(0xFF1565C0);
const _sosRed = Color(0xFFD32F2F);
const _pageBg = Color(0xFFF0F4FB);
const _greetingGradientA = Color(0xFFC9E0FF);
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

enum DashboardFeature {
  translation,
  visaAssistant,
  family,
  emergency,
  corporate,
}

class DashboardScreen extends StatefulWidget {
  final HealthService healthService;
  final UserService userService;
  final ReminderService reminderService;
  final DestinationService? destinationService;
  final String? userName;
  final bool showHeader;
  final ValueChanged<int>? onNavigateToTab;
  final ValueChanged<DashboardFeature>? onOpenFeature;

  const DashboardScreen({
    super.key,
    required this.healthService,
    required this.userService,
    required this.reminderService,
    this.destinationService,
    this.userName,
    this.showHeader = true,
    this.onNavigateToTab,
    this.onOpenFeature,
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
  List<UnifiedReminder> _smartReminders = [];
  bool _remindersLoading = true;

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

    await Future.wait([_checkHealth(), _loadProfile(), _loadChecklist(), _loadSmartReminders()]);

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

  Future<void> _loadSmartReminders() async {
    try {
      final items = await widget.reminderService.getUnifiedReminders(days: 90);
      if (mounted) setState(() { _smartReminders = items; _remindersLoading = false; });
    } catch (_) {
      if (mounted) setState(() { _remindersLoading = false; });
    }
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
        child: Center(child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 1440),
          child: RefreshIndicator(
          onRefresh: _loadAll,
          child: ListView(
            padding: EdgeInsets.zero,
            children: [
              if (widget.showHeader) _buildHeader(),
              const SizedBox(height: 12),
              if (widget.destinationService == null) _buildGreeting(),
              if (widget.destinationService != null) ...[
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  child: DestinationBanner(
                    service: widget.destinationService!,
                    greeting: _timeGreeting,
                    userId: _profile?.id,
                    preferredCountry: _profile?.countryCode,
                    completedSteps: _checklist.where((item) => item.isCompleted).length,
                    totalSteps: _checklist.length,
                    checklistPreview: _checklist.where((item) => item.isCompleted).map((item) => item.title).toList(),
                    onViewChecklist: _showFullChecklist,
                  ),
                ),
                const SizedBox(height: 20),
              ],
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _buildJourneyOverview(),
                    const SizedBox(height: 20),
                    _buildUpcomingReminders(),

                    const SizedBox(height: 28),
                    _buildQuickActions(),
                    const SizedBox(height: 20),
                    _buildFeatureShortcuts(),
                    const SizedBox(height: 32),
                  ],
                ),
              ),
            ],
          ),
        ))),
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
          IconButton(
            tooltip: 'Open Reminder Center',
            onPressed: () => widget.onNavigateToTab?.call(3),
            icon: Badge(
              isLabelVisible: _smartReminders.isNotEmpty,
              smallSize: 8,
              child: const Icon(Icons.notifications_outlined, color: _navy),
            ),
          ),
          const SizedBox(width: 8),
          // SOS pill button
          GestureDetector(
            onTap: () {
              final openFeature = widget.onOpenFeature;
              if (openFeature != null) {
                openFeature(DashboardFeature.emergency);
              } else {
                widget.onNavigateToTab?.call(4);
              }
            },
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
    final greeting = name.isNotEmpty
        ? '$_timeGreeting, $name 👋'
        : '$_timeGreeting 👋';
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
      subtitle: isHealthy
          ? 'All systems operational'
          : 'Some services may be limited',
      bg: isHealthy ? _healthyBg : _degradedBg,
      dotColor: isHealthy ? AppTheme.successColor : AppTheme.warningColor,
      labelColor: isHealthy ? _healthyText : _degradedText,
    );
  }

  // ─── Employment Pass card ─────────────────────────────────────────────────

  Widget _buildPassCard() {
    final passLabel =
        _profile?.visaPassType?.replaceAllMapped(
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
                child: const Icon(
                  Icons.description,
                  color: Colors.white,
                  size: 24,
                ),
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
                      style: TextStyle(
                        fontSize: 12,
                        color: AppTheme.textSecondary,
                      ),
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

  Widget _buildJourneyOverview() {
    final width = MediaQuery.sizeOf(context).width;
    final compact = width < 650;
    final cards = [
      _homeSummaryCard('Document wallet', 'Keep important files close',
          Icons.folder_copy_outlined, const Color(0xFFE7EEFF), () => widget.onNavigateToTab?.call(1)),
      _homeSummaryCard('Friends & Family', 'Your trusted circle',
          Icons.people_alt_outlined, const Color(0xFFE3F5EE), () =>
              widget.onOpenFeature != null
                  ? widget.onOpenFeature!(DashboardFeature.family)
                  : widget.onNavigateToTab?.call(4)),
      _homeSummaryCard('AI Government Assistant', 'Get guidance for your journey',
          Icons.auto_awesome_outlined, const Color(0xFFF2E8FF), () => widget.onNavigateToTab?.call(2)),
    ];
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      const Text('Your ExpatOne services',
          style: TextStyle(fontSize: 19, fontWeight: FontWeight.bold, color: _navy)),
      const SizedBox(height: 12),
      if (compact) ...cards.map((card) => Padding(
        padding: const EdgeInsets.only(bottom: 10), child: card))
      else Row(children: [
        for (var i = 0; i < cards.length; i++) ...[
          if (i > 0) const SizedBox(width: 12),
          Expanded(child: cards[i]),
        ],
      ]),
    ]);
  }

  Widget _homeSummaryCard(String title, String description, IconData icon,
      Color tint, VoidCallback action) {
    return Material(color: Colors.white, borderRadius: BorderRadius.circular(18),
      child: InkWell(onTap: action, borderRadius: BorderRadius.circular(18),
        child: Container(padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(borderRadius: BorderRadius.circular(18),
            border: Border.all(color: const Color(0xFFE1E7F0))),
          child: Row(children: [
            Container(padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(color: tint, borderRadius: BorderRadius.circular(14)),
              child: Icon(icon, color: _accentBlue, size: 25)),
            const SizedBox(width: 12),
            Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(title, style: const TextStyle(fontWeight: FontWeight.bold, color: _navy)),
              const SizedBox(height: 3),
              Text(description, style: const TextStyle(fontSize: 12, color: AppTheme.textSecondary)),
            ])),
            const Icon(Icons.chevron_right, color: _accentBlue),
          ]))));
  }

  Widget _buildUpcomingReminders() {
    final now = DateTime.now().toUtc();
    final upcoming = _smartReminders.where((r) => !r.dueAtUtc.isBefore(now)).toList()
      ..sort((a, b) => a.dueAtUtc.compareTo(b.dueAtUtc));
    final urgent = upcoming.where((r) => r.dueAtUtc.isBefore(now.add(const Duration(days: 7)))).length;
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: const Color(0xFFE1E7F0))),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          const Icon(Icons.notifications_active_outlined, color: _accentBlue),
          const SizedBox(width: 10),
          const Expanded(child: Text('Upcoming reminders',
            style: TextStyle(fontSize: 17, fontWeight: FontWeight.bold, color: _navy))),
          TextButton(onPressed: () => widget.onNavigateToTab?.call(3),
            child: const Text('View all')),
        ]),
        if (_remindersLoading) const LinearProgressIndicator()
        else if (upcoming.isEmpty) const Padding(
          padding: EdgeInsets.symmetric(vertical: 12),
          child: Text('All caught up! No upcoming reminders in the next 90 days.'))
        else ...[
          Text('$urgent due in the next 7 days',
            style: const TextStyle(color: AppTheme.textSecondary, fontSize: 12)),
          const SizedBox(height: 8),
          for (final item in upcoming.take(3))
            ListTile(dense: true, contentPadding: EdgeInsets.zero,
              leading: const CircleAvatar(child: Icon(Icons.event_note_outlined)),
              title: Text(item.title, maxLines: 1, overflow: TextOverflow.ellipsis),
              subtitle: Text('${item.category} • ${item.dueAtUtc.toLocal().toString().split('.').first}'),
              onTap: () => widget.onNavigateToTab?.call(3)),
        ],
      ]),
    );
  }

  // ─── Checklist ────────────────────────────────────────────────────────────

  void _showFullChecklist() {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            const Text('Your relocation checklist',
              style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
            const SizedBox(height: 16),
            Flexible(child: ListView(shrinkWrap: true, children: [
              for (final item in _checklist) ListTile(
                leading: Icon(item.isCompleted ? Icons.check_circle : Icons.radio_button_unchecked,
                  color: item.isCompleted ? AppTheme.successColor : AppTheme.textSecondary),
                title: Text(item.title),
                onTap: () {
                  Navigator.of(sheetContext).pop();
                  if (item.action != null) {
                    final tab = _actionToTab(item.action!);
                    if (tab != null) widget.onNavigateToTab?.call(tab);
                  }
                },
              ),
            ])),
          ]),
        ),
      ),
    );
  }

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
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppTheme.textSecondary,
                  ),
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
                  color: item.isCompleted
                      ? AppTheme.successColor
                      : Colors.transparent,
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
                        color: item.isCompleted
                            ? AppTheme.textSecondary
                            : _navy,
                        decoration: item.isCompleted
                            ? TextDecoration.lineThrough
                            : null,
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
                const Icon(
                  Icons.chevron_right,
                  size: 18,
                  color: AppTheme.textSecondary,
                ),
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

  Widget _buildFeatureShortcuts() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Explore ExpatOne',
          style: TextStyle(
            color: _navy,
            fontSize: 18,
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 10,
          runSpacing: 10,
          children: [
            _featureTile(
              'Translation',
              Icons.translate_rounded,
              const Color(0xFFE3EEFF),
              const Color(0xFF1565C0),
              DashboardFeature.translation,
            ),
            _featureTile(
              'Visa assistance',
              Icons.assignment_rounded,
              const Color(0xFFEEE3FF),
              const Color(0xFF7B3FBF),
              DashboardFeature.visaAssistant,
            ),
            _featureTile(
              'Family',
              Icons.family_restroom_rounded,
              const Color(0xFFE3F5EE),
              const Color(0xFF2E7D32),
              DashboardFeature.family,
            ),
            _featureTile(
              'Emergency',
              Icons.emergency_rounded,
              const Color(0xFFFFF0EC),
              const Color(0xFFD84315),
              DashboardFeature.emergency,
            ),
            _featureTile(
              'Corporate',
              Icons.business_center_rounded,
              const Color(0xFFFFF3E0),
              const Color(0xFFE65100),
              DashboardFeature.corporate,
            ),
          ],
        ),
      ],
    );
  }

  Widget _featureTile(
    String label,
    IconData icon,
    Color background,
    Color foreground,
    DashboardFeature feature,
  ) {
    return SizedBox(
      width: 148,
      child: Material(
        color: background,
        borderRadius: BorderRadius.circular(16),
        child: InkWell(
          borderRadius: BorderRadius.circular(16),
          onTap: () {
            final openFeature = widget.onOpenFeature;
            if (openFeature != null) {
              openFeature(feature);
            } else if (feature == DashboardFeature.emergency) {
              widget.onNavigateToTab?.call(4);
            } else if (feature == DashboardFeature.translation ||
                feature == DashboardFeature.visaAssistant ||
                feature == DashboardFeature.family ||
                feature == DashboardFeature.corporate) {
              widget.onNavigateToTab?.call(4);
            }
          },
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 15),
            child: Row(
              children: [
                Icon(icon, color: foreground, size: 22),
                const SizedBox(width: 9),
                Expanded(
                  child: Text(
                    label,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: _navy,
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildQuickActions() {
    final actions = <Widget>[
      _QuickActionCard(icon: Icons.note_add_outlined, label: 'Add Document',
        bgColor: _qaDocBg, iconColor: _accentBlue,
        onTap: () => widget.onNavigateToTab?.call(1)),
      _QuickActionCard(icon: Icons.person_add_alt_outlined, label: 'Add Family',
        bgColor: _qaReminderBg, iconColor: _qaReminderIcon,
        onTap: () => widget.onOpenFeature?.call(DashboardFeature.family)),
      _QuickActionCard(icon: Icons.share_outlined, label: 'Share Document',
        bgColor: _qaDocBg, iconColor: _accentBlue,
        onTap: () => widget.onNavigateToTab?.call(1)),
      _QuickActionCard(icon: Icons.celebration_outlined, label: 'Add Occasion',
        bgColor: _qaAssistBg, iconColor: _qaAssistIcon,
        onTap: () => widget.onNavigateToTab?.call(3)),
      _QuickActionCard(icon: Icons.add_alert_outlined, label: 'New Reminder',
        bgColor: _qaReminderBg, iconColor: _qaReminderIcon,
        onTap: () => widget.onNavigateToTab?.call(3)),
      _QuickActionCard(icon: Icons.assignment_outlined, label: 'Visa Application',
        bgColor: _qaDocBg, iconColor: _accentBlue,
        onTap: () => widget.onOpenFeature?.call(DashboardFeature.visaAssistant)),
      _QuickActionCard(icon: Icons.auto_awesome_outlined, label: 'AI Assistant',
        bgColor: _qaAssistBg, iconColor: _qaAssistIcon,
        onTap: () => widget.onNavigateToTab?.call(2)),
      _QuickActionCard(icon: Icons.explore_outlined, label: 'Find Services',
        bgColor: _qaReminderBg, iconColor: _qaReminderIcon,
        onTap: () => widget.onNavigateToTab?.call(4)),
    ];
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      const Text('Quick actions',
        style: TextStyle(fontSize: 19, fontWeight: FontWeight.bold, color: _navy)),
      const SizedBox(height: 12),
      LayoutBuilder(builder: (context, constraints) {
        final columns = constraints.maxWidth < 420 ? 2 : 4;
        return Column(children: [
          for (var row = 0; row < 8; row += columns) ...[
            Row(children: [
              for (var index = row; index < row + columns; index++) ...[
                if (index > row) const SizedBox(width: 10),
                actions[index],
              ],
            ]),
            const SizedBox(height: 10),
          ],
        ]);
      }),
    ]);
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
              decoration: BoxDecoration(
                color: dotColor,
                shape: BoxShape.circle,
              ),
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
