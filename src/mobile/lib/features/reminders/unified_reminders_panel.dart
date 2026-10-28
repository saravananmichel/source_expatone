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

  @override
  void dispose() {
    _requestVersion++;
    super.dispose();
  }

  Future<void> _load() async {
    final version = ++_requestVersion;
    if (!mounted) return;
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
    final overdueCount = _items.where((e) => e.dueAtUtc.isBefore(DateTime.now().toUtc())).length;
    final upcomingCount = _items.where((e) => !e.dueAtUtc.isBefore(DateTime.now().toUtc())).length;
    Widget metric(String label, int count, IconData icon, Color color) => Container(
      width: 172, padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(color: color.withValues(alpha: .09),
        borderRadius: BorderRadius.circular(18)),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Icon(icon, color: color), const SizedBox(height: 10),
        Text('$'+'$count', style: Theme.of(context).textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.bold)),
        Text(label),
      ]),
    );
    return RefreshIndicator(onRefresh: _load, child: ListView(
      padding: const EdgeInsets.all(18),
      physics: const AlwaysScrollableScrollPhysics(),
      children: [
        Text('Stay ahead of what matters', style: Theme.of(context).textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.bold)),
        const SizedBox(height: 5),
        Text('Your documents, family, visa and work reminders in one place.',
          style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant)),
        const SizedBox(height: 20),
        Wrap(spacing: 12, runSpacing: 12, children: [
          metric('All reminders', _items.length, Icons.notifications_active_outlined, Colors.indigo),
          metric('Upcoming', upcomingCount, Icons.event_available_outlined, Colors.teal),
          metric('Past due', overdueCount, Icons.warning_amber_rounded, Colors.deepOrange),
        ]),
        const SizedBox(height: 24),
        TextField(decoration: InputDecoration(
          prefixIcon: const Icon(Icons.search), hintText: 'Search your reminders',
          border: OutlineInputBorder(borderRadius: BorderRadius.circular(14))),
          onChanged: (value) => setState(() => _search = value)),
        const SizedBox(height: 12),
        Wrap(spacing: 10, runSpacing: 8, crossAxisAlignment: WrapCrossAlignment.center, children: [
          DropdownButton<String>(value: _category, items: const [
            DropdownMenuItem(value: 'all', child: Text('All categories')),
            DropdownMenuItem(value: 'document', child: Text('Documents')),
            DropdownMenuItem(value: 'connection', child: Text('Connections')),
            DropdownMenuItem(value: 'corporate', child: Text('Corporate tasks')),
            DropdownMenuItem(value: 'sharing', child: Text('Sharing expiry')),
            DropdownMenuItem(value: 'visa', child: Text('Visa drafts')),
            DropdownMenuItem(value: 'occasion', child: Text('Family occasions')),
          ], onChanged: (value) { if (value != null) { setState(() => _category = value); _load(); } }),
          const SizedBox(width: 10),
          DropdownButton<int>(value: _days, items: const [
            DropdownMenuItem(value: 30, child: Text('Next 30 days')),
            DropdownMenuItem(value: 90, child: Text('Next 90 days')),
            DropdownMenuItem(value: 365, child: Text('Next 365 days')),
          ], onChanged: (value) { if (value != null) { setState(() => _days = value); _load(); } }),
        ]),
        const SizedBox(height: 18),
        Text('Your reminders', style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.bold)),
        const SizedBox(height: 12),
        if (_loading) const Padding(padding: EdgeInsets.all(32), child: Center(child: CircularProgressIndicator()))
        else if (_error != null) Center(child: TextButton.icon(
          onPressed: _load, icon: const Icon(Icons.refresh), label: Text(_error!)))
        else if (filtered.isEmpty) Card(child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 42, horizontal: 20),
          child: Column(children: [
            Icon(Icons.event_available_outlined, size: 46, color: Theme.of(context).colorScheme.primary),
            const SizedBox(height: 12),
            const Text('Nothing needs your attention', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w600)),
            const SizedBox(height: 5),
            const Text('Try another category or time range.', textAlign: TextAlign.center),
          ])))
        else ...filtered.map((item) {
          final overdue = item.dueAtUtc.isBefore(DateTime.now().toUtc());
          return Card(margin: const EdgeInsets.only(bottom: 10), child: ListTile(
            contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            leading: CircleAvatar(child: Icon(overdue ? Icons.priority_high : Icons.notifications_outlined)),
            title: Text(item.title, style: const TextStyle(fontWeight: FontWeight.w600)),
            subtitle: Padding(padding: const EdgeInsets.only(top: 6),
              child: Text([item.category, if (item.description != null) item.description!,
                item.dueAtUtc.toLocal().toString().split('.').first].join(' • '))),
            trailing: overdue ? const Icon(Icons.warning_amber, color: Colors.deepOrange) : null,
          ));
        }),
      ],
    ));
  }
}
