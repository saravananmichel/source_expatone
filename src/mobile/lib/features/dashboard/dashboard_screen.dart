import 'package:flutter/material.dart';
import '../../core/theme/app_theme.dart';
import '../../shared/services/health_service.dart';
import '../../core/error/result.dart';

class DashboardScreen extends StatefulWidget {
  final HealthService healthService;

  const DashboardScreen({super.key, required this.healthService});

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  HealthStatus? _healthStatus;
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _checkHealth();
  }

  Future<void> _checkHealth() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    final result = await widget.healthService.checkHealth();

    if (!mounted) return;

    setState(() {
      _isLoading = false;
      switch (result) {
        case Success<HealthStatus>(:final data):
          _healthStatus = data;
        case Failure<HealthStatus>(:final message):
          _error = message;
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: _checkHealth,
          child: ListView(
            padding: const EdgeInsets.all(20),
            children: [
              const SizedBox(height: 8),
              _buildGreeting(),
              const SizedBox(height: 8),
              _buildSubtitle(),
              const SizedBox(height: 24),
              _buildSearchBar(),
              const SizedBox(height: 28),
              _buildConnectionStatus(),
              const SizedBox(height: 28),
              _buildUpcomingSection(),
              const SizedBox(height: 28),
              _buildQuickActions(),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildGreeting() {
    return Text(
      'Good morning',
      style: Theme.of(context).textTheme.headlineLarge,
    );
  }

  Widget _buildSubtitle() {
    return Text(
      'Your ExpatOne Assistant',
      style: Theme.of(context).textTheme.bodyLarge?.copyWith(
            color: AppTheme.textSecondary,
          ),
    );
  }

  Widget _buildSearchBar() {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: const Color(0xFFE8E8E8)),
      ),
      child: Row(
        children: [
          Icon(Icons.search, color: AppTheme.textSecondary.withValues(alpha: 0.5)),
          const SizedBox(width: 12),
          Text(
            'Ask anything about living in Malaysia...',
            style: TextStyle(
              color: AppTheme.textSecondary.withValues(alpha: 0.5),
              fontSize: 15,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildConnectionStatus() {
    if (_isLoading) {
      return const _StatusCard(
        icon: Icons.sync,
        label: 'Connecting to ExpatOne...',
        color: AppTheme.textSecondary,
        isLoading: true,
      );
    }

    if (_error != null) {
      return _StatusCard(
        icon: Icons.cloud_off,
        label: _error!,
        color: AppTheme.errorColor,
        onRetry: _checkHealth,
      );
    }

    final db = _healthStatus?.database ?? 'unknown';
    final isHealthy = _healthStatus?.isHealthy ?? false;

    return _StatusCard(
      icon: isHealthy ? Icons.cloud_done : Icons.warning_amber,
      label: isHealthy
          ? 'Backend: Connected  |  Database: ${db == 'connected' ? 'Connected' : 'Unavailable'}'
          : 'Backend: Degraded',
      color: isHealthy ? AppTheme.successColor : AppTheme.warningColor,
    );
  }

  Widget _buildUpcomingSection() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Upcoming', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 12),
        _buildUpcomingCard(
          icon: Icons.badge_outlined,
          title: 'Employment Pass',
          subtitle: 'Expires in ---',
        ),
        const SizedBox(height: 8),
        _buildUpcomingCard(
          icon: Icons.shield_outlined,
          title: 'Insurance',
          subtitle: 'Expires in ---',
        ),
        const SizedBox(height: 8),
        _buildUpcomingCard(
          icon: Icons.menu_book_outlined,
          title: 'Passport',
          subtitle: 'Expires in ---',
        ),
      ],
    );
  }

  Widget _buildUpcomingCard({
    required IconData icon,
    required String title,
    required String subtitle,
  }) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: AppTheme.primaryColor.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(10),
              ),
              child: Icon(icon, color: AppTheme.primaryColor, size: 22),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: const TextStyle(
                    fontWeight: FontWeight.w600,
                    fontSize: 15,
                  )),
                  const SizedBox(height: 2),
                  Text(subtitle, style: Theme.of(context).textTheme.bodyMedium),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildQuickActions() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Quick Actions', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 12),
        GridView.count(
          crossAxisCount: 3,
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          mainAxisSpacing: 10,
          crossAxisSpacing: 10,
          children: [
            _buildActionTile(Icons.account_balance, 'Government\nAssistant'),
            _buildActionTile(Icons.folder_outlined, 'Documents'),
            _buildActionTile(Icons.document_scanner_outlined, 'Scan\nDocument'),
            _buildActionTile(Icons.translate, 'Translate'),
            _buildActionTile(Icons.emergency, 'Emergency'),
            _buildActionTile(Icons.notifications_outlined, 'Reminders'),
          ],
        ),
      ],
    );
  }

  Widget _buildActionTile(IconData icon, String label) {
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: () {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Coming soon'),
              duration: Duration(seconds: 1),
            ),
          );
        },
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(icon, color: AppTheme.primaryColor, size: 28),
              const SizedBox(height: 8),
              Text(
                label,
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w500),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _StatusCard extends StatelessWidget {
  final IconData icon;
  final String label;
  final Color color;
  final bool isLoading;
  final VoidCallback? onRetry;

  const _StatusCard({
    required this.icon,
    required this.label,
    required this.color,
    this.isLoading = false,
    this.onRetry,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        children: [
          if (isLoading)
            SizedBox(
              width: 18,
              height: 18,
              child: CircularProgressIndicator(strokeWidth: 2, color: color),
            )
          else
            Icon(icon, size: 18, color: color),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              label,
              style: TextStyle(fontSize: 13, color: color, fontWeight: FontWeight.w500),
            ),
          ),
          if (onRetry != null)
            GestureDetector(
              onTap: onRetry,
              child: Icon(Icons.refresh, size: 18, color: color),
            ),
        ],
      ),
    );
  }
}
