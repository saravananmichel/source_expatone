import 'package:flutter/material.dart';
import '../../core/networking/api_client.dart';

class FamilyOccasionsScreen extends StatefulWidget {
  final ApiClient api;
  const FamilyOccasionsScreen({super.key, required this.api});
  @override
  State<FamilyOccasionsScreen> createState() => _FamilyOccasionsScreenState();
}

class _FamilyOccasionsScreenState extends State<FamilyOccasionsScreen> {
  List<Map<String, dynamic>> _items = [];
  bool _loading = true;
  String? _error;
  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    if (mounted) setState(() { _loading = true; _error = null; });
    try {
      final items = await widget.api.getList('/family-occasions');
      if (mounted) setState(() { _items = items; _loading = false; _error = null; });
    } catch (_) {
      if (mounted) setState(() { _loading = false; _error = 'Unable to load occasions. Please retry.'; });
    }
  }

  Future<void> _create([Map<String, dynamic>? existing]) async {
    final title = TextEditingController();
    String kind = existing?['kind'] as String? ?? 'Birthday';
    int offset = existing?['daysBefore'] as int? ?? 7;
    DateTime date = existing == null ? DateTime.now() : DateTime.parse(existing['occasionDate'] as String);
    bool annual = existing?['repeatsAnnually'] as bool? ?? true;
    bool reminders = existing?['remindersEnabled'] as bool? ?? true;
    title.text = existing?['title'] as String? ?? '';
    final saved = await showDialog<bool>(context: context, builder: (context) =>
      StatefulBuilder(builder: (context, update) => AlertDialog(
        title: Text(existing == null ? 'Add family occasion' : 'Edit family occasion'),
        content: SingleChildScrollView(child: Column(mainAxisSize: MainAxisSize.min, children: [
          TextField(controller: title, decoration: const InputDecoration(labelText: 'Occasion title')),
          DropdownButton<String>(value: kind, isExpanded: true,
            items: ['Birthday', 'Anniversary', 'Relocation', 'Custom']
                .map((value) => DropdownMenuItem(value: value, child: Text(value))).toList(),
            onChanged: (value) => update(() => kind = value ?? kind)),
          ListTile(title: Text('Date: ${date.day}/${date.month}/${date.year}'),
            trailing: const Icon(Icons.calendar_month),
            onTap: () async {
              final picked = await showDatePicker(context: context,
                initialDate: date, firstDate: DateTime(1900), lastDate: DateTime(2100));
              if (picked != null) update(() => date = picked);
            }),
          SwitchListTile(title: const Text('Repeat annually'), value: annual,
            onChanged: (value) => update(() => annual = value)),
          SwitchListTile(title: const Text('Remind me'), value: reminders,
            onChanged: (value) => update(() => reminders = value)),
          DropdownButton<int>(value: offset, isExpanded: true,
            items: [1, 3, 7, 30].map((days) =>
                DropdownMenuItem(value: days, child: Text('$days days before'))).toList(),
            onChanged: (value) => update(() => offset = value ?? offset)),
        ])),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
          FilledButton(onPressed: () async {
            if (title.text.trim().isEmpty) return;
            try {
              final body = <String, dynamic>{
                'title': title.text.trim(), 'kind': kind,
                'occasionDate': DateTime.utc(date.year, date.month, date.day).toIso8601String(),
                'repeatsAnnually': annual, 'remindersEnabled': reminders, 'daysBefore': offset,
              };
              if (existing == null) {
                await widget.api.post('/family-occasions', body: body);
              } else {
                await widget.api.put('/family-occasions/${existing['id']}', body: body);
              }
              if (context.mounted) Navigator.pop(context, true);
            } catch (_) {
              if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('Unable to save occasion')));
            }
          }, child: const Text('Save')),
        ],
      )));
    title.dispose();
    if (saved == true) _load();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Family occasions')),
    floatingActionButton: FloatingActionButton(onPressed: _create, child: const Icon(Icons.add)),
    body: _loading ? const Center(child: CircularProgressIndicator()) :
      _error != null ? Center(child: TextButton(onPressed: _load, child: Text(_error!))) :
      RefreshIndicator(onRefresh: _load, child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        children: [if (_items.isEmpty) const ListTile(title: Text('No occasions yet'),
          subtitle: Text('Tap + to add a birthday or special occasion.')),
        ..._items.map((item) => ListTile(
        leading: const Icon(Icons.event),
        title: Text(item['title'] as String? ?? ''),
        subtitle: Text(item['kind'] as String? ?? ''),
        onTap: () => _create(item),
        trailing: IconButton(icon: const Icon(Icons.delete_outline),
          onPressed: () async {
            final confirmed = await showDialog<bool>(context: context, builder: (context) =>
              AlertDialog(title: const Text('Delete occasion?'), actions: [
                TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
                TextButton(onPressed: () => Navigator.pop(context, true), child: const Text('Delete')),
              ]));
            if (confirmed != true) return;
            try { await widget.api.delete('/family-occasions/${item['id']}'); await _load(); }
            catch (_) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('Unable to delete occasion'))); }
          }),
      ))])),
  );
}
