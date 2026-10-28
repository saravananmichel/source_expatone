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
  String _search = '';
  int _days = 90;
  bool _loading = true;
  String? _error;
  List<UnifiedReminder> _items = [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  int _requestVersion = 0;

  Future<void> _load() async {
    final version = ++_requestVersion;
    setState(() { _loading = true; _error = null; });
    try {
      final items = await widget.service.getUnifiedReminders(
        category: _category, days: _days);
      if (mounted && version == _requestVersion) setState(() { _items = items; _loading = false; });
    } catch (_) {
      if (mounted && version == _requestVersion) setState(() {
        _error = 'Unable to load smart reminders.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final filtered = _items.where((item) =>
      ('${item.title} ${item.description ?? ''} ${item.category}')
        .toLowerCase().contains(_search.toLowerCase())).toList();
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
      Padding(padding: const EdgeInsets.symmetric(horizontal: 12), child: TextField(
        decoration: const InputDecoration(prefixIcon: Icon(Icons.search),
          hintText: 'Search reminders'),
        onChanged: (value) => setState(() => _search = value))),
      Padding(padding: const EdgeInsets.symmetric(horizontal: 12), child: Row(children: [
        const Text('Next'), const SizedBox(width: 12),
        DropdownButton<int>(value: _days,
          items: const [DropdownMenuItem(value: 30, child: Text('30 days')),
            DropdownMenuItem(value: 90, child: Text('90 days')),
            DropdownMenuItem(value: 365, child: Text('365 days'))],
          onChanged: (value) { if (value != null) { setState(() => _days = value); _load(); } }),
      ])),
      Expanded(child: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: TextButton(onPressed: _load, child: Text(_error!)))
              : RefreshIndicator(
                  onRefresh: _load,
                  child: ListView.builder(
                    physics: const AlwaysScrollableScrollPhysics(),
                    itemCount: filtered.isEmpty ? 1 : filtered.length,
                    itemBuilder: (context, index) {
                      if (filtered.isEmpty) return const ListTile(
                        title: Text('No matching reminders'),
                        subtitle: Text('Try another category, search or date range.'));
                      final item = filtered[index];
                      final overdue = item.dueAtUtc.isBefore(DateTime.now().toUtc()) &&
                          (item.category == 'corporate' || item.category == 'document');
                      return ListTile(
                        leading: Icon(overdue ? Icons.warning_amber_outlined : Icons.notifications_active_outlined,
                          color: overdue ? Theme.of(context).colorScheme.error : null),
                        title: Text(item.title),
                        subtitle: Text([
                          item.category,
                          if (overdue) 'Overdue',
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
