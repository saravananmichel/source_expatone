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

  IconData _categoryIcon(String category) => switch (category) {
    'visa' => Icons.flight_takeoff_outlined,
    'document' => Icons.description_outlined,
    'connection' => Icons.people_outline,
    'occasion' => Icons.cake_outlined,
    'corporate' => Icons.business_center_outlined,
    'sharing' => Icons.folder_shared_outlined,
    _ => Icons.notifications_outlined,
  };

  @override
  Widget build(BuildContext context) {
    final filtered = _items.where((item) =>
      ('${item.title} ${item.description ?? ''} ${item.category}')
        .toLowerCase().contains(_search.toLowerCase())).toList();
    final now = DateTime.now().toUtc();
    final upcoming = filtered.where((e) => !e.dueAtUtc.isBefore(now)).length;
    final urgent = filtered.where((e) => e.dueAtUtc.isBefore(now.add(const Duration(days: 7)))).length;
    const categories = <(String, String)>[
      ('all', 'All'), ('visa', 'Visa'), ('document', 'Documents'),
      ('connection', 'Friends & Family'), ('occasion', 'Occasions'),
      ('corporate', 'Corporate'), ('sharing', 'Sharing'),
    ];
    return RefreshIndicator(onRefresh: _load, child: ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(18),
      children: [
        Container(padding: const EdgeInsets.all(22),
          decoration: BoxDecoration(
            gradient: const LinearGradient(colors: [Color(0xFF193D98), Color(0xFF2474D0)]),
            borderRadius: BorderRadius.circular(20)),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const Icon(Icons.notifications_active_outlined, color: Colors.white, size: 30),
            const SizedBox(height: 10),
            const Text('Smart Reminders', style: TextStyle(
              color: Colors.white, fontSize: 24, fontWeight: FontWeight.bold)),
            const SizedBox(height: 6),
            const Text('All your important dates, tasks and actions in one place.',
              style: TextStyle(color: Colors.white70)),
            const SizedBox(height: 14),
            Wrap(spacing: 8, runSpacing: 8, children: [
              Chip(label: Text('${filtered.length} total')),
              Chip(label: Text('$upcoming upcoming')),
              Chip(label: Text('$urgent within 7 days')),
            ]),
          ])),
        const SizedBox(height: 18),
        TextField(decoration: InputDecoration(
          hintText: 'Search reminders, people or documents',
          prefixIcon: const Icon(Icons.search),
          filled: true, fillColor: Theme.of(context).colorScheme.surface,
          border: OutlineInputBorder(borderRadius: BorderRadius.circular(14))),
          onChanged: (value) => setState(() => _search = value)),
        const SizedBox(height: 12),
        SingleChildScrollView(scrollDirection: Axis.horizontal, child: Row(children: [
          for (final entry in categories) Padding(
            padding: const EdgeInsets.only(right: 8),
            child: ChoiceChip(label: Text(entry.$2), selected: _category == entry.$1,
              onSelected: (_) { setState(() => _category = entry.$1); _load(); })),
        ])),
        const SizedBox(height: 10),
        Row(children: [
          const Text('Time range  '),
          DropdownButton<int>(value: _days, items: const [
            DropdownMenuItem(value: 30, child: Text('30 days')),
            DropdownMenuItem(value: 90, child: Text('90 days')),
            DropdownMenuItem(value: 365, child: Text('365 days')),
          ], onChanged: (value) {
            if (value != null) { setState(() => _days = value); _load(); }
          }),
          const Spacer(),
          IconButton(tooltip: 'Refresh', onPressed: _load, icon: const Icon(Icons.refresh)),
        ]),
        const SizedBox(height: 12),
        if (_loading) const Center(child: Padding(
          padding: EdgeInsets.all(32), child: CircularProgressIndicator()))
        else if (_error != null) Card(child: ListTile(
          leading: const Icon(Icons.error_outline),
          title: Text(_error!),
          trailing: TextButton(onPressed: _load, child: const Text('Retry'))))
        else if (filtered.isEmpty) Card(child: Padding(
          padding: const EdgeInsets.all(36), child: Column(children: [
            const Icon(Icons.event_available_outlined, size: 48, color: Color(0xFF2775D0)),
            const SizedBox(height: 12),
            const Text('All caught up', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
            const SizedBox(height: 6),
            const Text('No reminders match these filters.', textAlign: TextAlign.center),
          ])))
        else LayoutBuilder(builder: (context, constraints) {
          final wide = constraints.maxWidth >= 780;
          if (!wide) return Column(children: [
            for (final item in filtered) Card(margin: const EdgeInsets.only(bottom: 10),
              child: ListTile(contentPadding: const EdgeInsets.all(14),
                leading: CircleAvatar(child: Icon(_categoryIcon(item.category))),
                title: Text(item.title, style: const TextStyle(fontWeight: FontWeight.bold)),
                subtitle: Text('${item.category} • ${item.dueAtUtc.toLocal().toString().split('.').first}'
                  '${item.description == null ? '' : '\n${item.description}'}'),
                isThreeLine: item.description != null)),
          ]);
          return Card(clipBehavior: Clip.antiAlias, child: DataTable(
            headingRowColor: WidgetStateProperty.all(const Color(0xFFEAF2FF)),
            columns: const [
              DataColumn(label: Text('Type')), DataColumn(label: Text('Title')),
              DataColumn(label: Text('Related to')), DataColumn(label: Text('Due date')),
              DataColumn(label: Text('Status')),
            ],
            rows: [
              for (final item in filtered) DataRow(cells: [
                DataCell(Row(mainAxisSize: MainAxisSize.min, children: [
                  Icon(_categoryIcon(item.category), size: 18),
                  const SizedBox(width: 7), Text(item.category)])),
                DataCell(SizedBox(width: 210, child: Text(item.title,
                  overflow: TextOverflow.ellipsis))),
                DataCell(SizedBox(width: 220, child: Text(item.description ?? '—',
                  overflow: TextOverflow.ellipsis))),
                DataCell(Text(item.dueAtUtc.toLocal().toString().split(' ').first)),
                DataCell(Text(item.dueAtUtc.isBefore(now) ? 'Past due' : 'Upcoming')),
              ]),
            ],
          ));
        }),
      ],
    ));
  }
}
