import 'package:flutter/material.dart';
import '../../core/services/reminder_service.dart';
import '../../core/services/document_service.dart';
import '../../core/theme/app_theme.dart';

class ManageRemindersScreen extends StatefulWidget {
  final DocumentItem document;
  final ReminderService reminderService;

  const ManageRemindersScreen({
    super.key,
    required this.document,
    required this.reminderService,
  });

  @override
  State<ManageRemindersScreen> createState() => _ManageRemindersScreenState();
}

class _ManageRemindersScreenState extends State<ManageRemindersScreen> {
  static const _offsets = [30, 14, 7, 1];

  Set<int> _selected = {};
  bool _isLoading = true;
  bool _isSaving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final reminders = await widget.reminderService.getDocumentReminders(widget.document.id);
      if (mounted) {
        setState(() {
          _selected = reminders
              .where((r) => r.isActive)
              .map((r) => r.daysBeforeExpiry)
              .toSet();
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() { _error = 'Unable to load reminders'; _isLoading = false; });
    }
  }

  Future<void> _save() async {
    setState(() { _isSaving = true; _error = null; });
    // Stage 1: generation POST — a failure here means reminders were NOT saved.
    try {
      await widget.reminderService.generateOnly(
        widget.document.id,
        _selected.toList()..sort((a, b) => b.compareTo(a)),
      );
    } catch (_) {
      if (mounted) setState(() { _error = 'Unable to save reminders'; _isSaving = false; });
      return;
    }
    // Stage 2: refresh GET — reminders ARE saved regardless of whether this succeeds.
    // A failure here must not be reported as a save failure.
    if (!mounted) return;
    try {
      await widget.reminderService.getDocumentReminders(widget.document.id);
    } catch (_) {
      // Refresh failed — navigate away with success and show a non-fatal message.
      // Capture messenger before pop so we can show the snackbar on the parent route.
      if (!mounted) return;
      final messenger = ScaffoldMessenger.of(context);
      Navigator.pop(context, true);
      messenger.showSnackBar(
        const SnackBar(
          content: Text("Reminders saved, but we couldn't refresh the latest status."),
        ),
      );
      return;
    }
    if (mounted) Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    final hasExpiry = widget.document.expiryDate != null;

    return Scaffold(
      appBar: AppBar(title: const Text('Manage Reminders')),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : !hasExpiry
              ? _buildNoExpiry()
              : _buildContent(),
    );
  }

  Widget _buildNoExpiry() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.event_busy, size: 64, color: AppTheme.textSecondary),
            const SizedBox(height: 16),
            Text('No expiry date', style: Theme.of(context).textTheme.headlineMedium),
            const SizedBox(height: 8),
            Text(
              'This document does not have an expiry date.\nReminders can only be set for documents with expiry dates.',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyLarge?.copyWith(color: AppTheme.textSecondary),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildContent() {
    final expiry = widget.document.expiryDate!;

    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(widget.document.name, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 4),
          Text(
            'Expires: ${_formatDate(expiry)}',
            style: Theme.of(context).textTheme.bodyLarge?.copyWith(color: AppTheme.textSecondary),
          ),
          if (_error != null) ...[
            const SizedBox(height: 16),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: AppTheme.errorColor.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Text(_error!, style: TextStyle(color: AppTheme.errorColor), textAlign: TextAlign.center),
            ),
          ],
          const SizedBox(height: 24),
          Text('Remind me:', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 12),
          ..._offsets.map((days) {
            final reminderDate = expiry.subtract(Duration(days: days));
            final isPast = reminderDate.isBefore(DateTime.now());

            return CheckboxListTile(
              value: _selected.contains(days),
              onChanged: isPast || _isSaving
                  ? null
                  : (val) {
                      setState(() {
                        if (val == true) {
                          _selected.add(days);
                        } else {
                          _selected.remove(days);
                        }
                      });
                    },
              title: Text('$days days before'),
              subtitle: Text(
                isPast ? 'Already passed' : _formatDate(reminderDate),
                style: TextStyle(
                  color: isPast ? AppTheme.errorColor : AppTheme.textSecondary,
                  fontSize: 13,
                ),
              ),
              controlAffinity: ListTileControlAffinity.leading,
            );
          }),
          const Spacer(),
          SizedBox(
            height: 50,
            child: ElevatedButton(
              onPressed: _isSaving ? null : _save,
              child: _isSaving
                  ? const SizedBox(width: 20, height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                  : const Text('Save Reminders', style: TextStyle(fontSize: 16)),
            ),
          ),
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
