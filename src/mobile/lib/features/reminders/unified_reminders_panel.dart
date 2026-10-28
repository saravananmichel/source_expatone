import 'package:flutter/material.dart';
import '../../core/services/reminder_service.dart';

class UnifiedRemindersPanel extends StatefulWidget {
  final ReminderService service;
  const UnifiedRemindersPanel({super.key, required this.service});

  @override
  State<UnifiedRemindersPanel> createState() => _UnifiedRemindersPanelState();
}

class _UnifiedRemindersPanelState extends State<UnifiedRemindersPanel> {
  String _category = 'all';
  bool _loading = true;
  String? _error;
  List<UnifiedReminder> _items = [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final items = await widget.service.getUnifiedReminders(
        category: _category, days: 90);
      if (mounted) setState(() { _items = items; _loading = false; });
    } catch (_) {
      if (mounted) setState(() {
        _error = 'Unable to load smart reminders.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Column(children: [
      Padding(
        padding: const EdgeInsets.all(12),
        child: DropdownButtonFormField<String>(
          value: _category,
          decoration: const InputDecoration(labelText: 'Reminder category'),
          items: const [
            DropdownMenuItem(value: 'all', child: Text('All')),
            DropdownMenuItem(value: 'document', child: Text('Documents')),
            DropdownMenuItem(value: 'connection', child: Text('Connections')),
            DropdownMenuItem(value: 'corporate', child: Text('Corporate tasks')),
            DropdownMenuItem(value: 'sharing', child: Text('Sharing expiry')),
            DropdownMenuItem(value: 'visa', child: Text('Visa drafts')),
            DropdownMenuItem(value: 'occasion', child: Text('Family occasions')),
          ],
          onChanged: (value) {
            if (value == null) return;
            setState(() => _category = value);
            _load();
          },
        ),
      ),
      Expanded(child: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: TextButton(onPressed: _load, child: Text(_error!)))
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView.builder(
                    physics: const AlwaysScrollableScrollPhysics(),
                    itemCount: _items.length,
                    itemBuilder: (context, index) {
                      final item = _items[index];
                      return ListTile(
                        leading: const Icon(Icons.notifications_active_outlined),
                        title: Text(item.title),
                        subtitle: Text([
                          item.category,
                          if (item.description != null) item.description!,
                          item.dueAtUtc.toLocal().toString().split('.').first,
                        ].join(' • ')),
                      );
                    },
                  ),
                )),
    ]);
  }
}
