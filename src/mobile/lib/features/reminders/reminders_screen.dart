import 'package:flutter/material.dart';
import '../../core/services/reminder_service.dart';
import '../../core/theme/app_theme.dart';

class RemindersScreen extends StatefulWidget {
  final ReminderService reminderService;

  const RemindersScreen({super.key, required this.reminderService});

  @override
  State<RemindersScreen> createState() => _RemindersScreenState();
}

class _RemindersScreenState extends State<RemindersScreen> {
  List<ReminderItem>? _reminders;
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadReminders();
  }

  Future<void> _loadReminders() async {
    setState(() { _isLoading = true; _error = null; });
    try {
      final reminders = await widget.reminderService.getReminders();
      if (mounted) setState(() { _reminders = reminders; _isLoading = false; });
    } catch (e) {
      if (mounted) setState(() { _error = 'Unable to load reminders'; _isLoading = false; });
    }
  }

  Future<void> _dismissReminder(ReminderItem reminder) async {
    try {
      await widget.reminderService.dismissReminder(reminder.id);
      _loadReminders();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Unable to dismiss reminder')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Reminder Center')),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? _buildError()
              : _reminders == null || _reminders!.isEmpty
                  ? _buildEmpty()
                  : _buildList(),
    );
  }

  Widget _buildError() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.error_outline, size: 48, color: AppTheme.errorColor),
          const SizedBox(height: 16),
          Text(_error!, style: TextStyle(color: AppTheme.errorColor)),
          const SizedBox(height: 12),
          ElevatedButton(onPressed: _loadReminders, child: const Text('Retry')),
        ],
      ),
    );
  }

  Widget _buildEmpty() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.notifications_none, size: 72, color: AppTheme.textSecondary.withValues(alpha: 0.5)),
            const SizedBox(height: 24),
            Text('No active reminders', style: Theme.of(context).textTheme.headlineMedium),
            const SizedBox(height: 8),
            Text(
              'Set up reminders on your documents\nto get notified before they expire.',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyLarge?.copyWith(color: AppTheme.textSecondary),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildList() {
    return RefreshIndicator(
      onRefresh: _loadReminders,
      child: ListView.builder(
        padding: const EdgeInsets.all(16),
        itemCount: _reminders!.length,
        itemBuilder: (context, index) {
          final r = _reminders![index];
          return _ReminderCard(
            reminder: r,
            onDismiss: () => _dismissReminder(r),
          );
        },
      ),
    );
  }
}

class _ReminderCard extends StatelessWidget {
  final ReminderItem reminder;
  final VoidCallback onDismiss;

  const _ReminderCard({required this.reminder, required this.onDismiss});

  Color _urgencyColor() {
    if (reminder.daysBeforeExpiry <= 1) return AppTheme.errorColor;
    if (reminder.daysBeforeExpiry <= 7) return AppTheme.warningColor;
    return AppTheme.primaryColor;
  }

  @override
  Widget build(BuildContext context) {
    final color = _urgencyColor();

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Icon(Icons.notifications_active, color: color, size: 20),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        reminder.documentName,
                        style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15),
                      ),
                      Text(
                        reminder.documentType,
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                    ],
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: color.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: Text(
                    '${reminder.daysBeforeExpiry}d',
                    style: TextStyle(color: color, fontWeight: FontWeight.w600, fontSize: 12),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            if (reminder.documentExpiryDate != null)
              _infoLine('Expires', _formatDate(reminder.documentExpiryDate!)),
            _infoLine('Reminder', _formatDate(reminder.reminderDate)),
            _infoLine('Offset', '${reminder.daysBeforeExpiry} days before expiry'),
            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                TextButton(
                  onPressed: onDismiss,
                  child: const Text('Dismiss'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _infoLine(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        children: [
          Text('$label: ', style: TextStyle(color: AppTheme.textSecondary, fontSize: 13)),
          Text(value, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500)),
        ],
      ),
    );
  }

  String _formatDate(DateTime date) {
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
        'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    return '${date.day} ${months[date.month - 1]} ${date.year}';
  }
}
