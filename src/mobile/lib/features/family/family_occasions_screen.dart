import 'package:flutter/material.dart';
import '../../shared/widgets/profile_avatar.dart';
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
  Widget build(BuildContext context) {
    final birthdays = _items.where((e) => e['kind'] == 'Birthday').length;
    final anniversaries = _items.where((e) => e['kind'] == 'Anniversary').length;
    return Scaffold(
      appBar: AppBar(title: const Text('Family occasions'),
        actions: [IconButton(onPressed: _load, icon: const Icon(Icons.refresh), tooltip: 'Refresh')]),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _create(), icon: const Icon(Icons.add), label: const Text('Add occasion')),
      body: _loading ? const Center(child: CircularProgressIndicator()) :
        _error != null ? Center(child: TextButton(onPressed: _load, child: Text(_error!))) :
        RefreshIndicator(onRefresh: _load, child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(18),
          children: [
            Container(padding: const EdgeInsets.all(22),
              decoration: BoxDecoration(
                gradient: const LinearGradient(colors: [Color(0xFF273E85), Color(0xFF5368B5)]),
                borderRadius: BorderRadius.circular(20)),
              child: const Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Icon(Icons.celebration_outlined, color: Colors.white, size: 32),
                SizedBox(height: 12),
                Text('Celebrate every special moment',
                  style: TextStyle(color: Colors.white, fontSize: 22, fontWeight: FontWeight.bold)),
                SizedBox(height: 6),
                Text('Birthdays, anniversaries and milestones — all in one place.',
                  style: TextStyle(color: Colors.white70)),
              ])),
            const SizedBox(height: 16),
            Wrap(spacing: 10, runSpacing: 10, children: [
              Chip(avatar: const Icon(Icons.cake_outlined, size: 18), label: Text('Birthdays: '+birthdays.toString())),
              Chip(avatar: const Icon(Icons.favorite_outline, size: 18), label: Text('Anniversaries: '+anniversaries.toString())),
              Chip(avatar: const Icon(Icons.event_outlined, size: 18), label: Text('All occasions: '+_items.length.toString())),
            ]),
            const SizedBox(height: 20),
            Text('Your occasions', style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.bold)),
            const SizedBox(height: 12),
            if (_items.isEmpty) Card(child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 48),
              child: Column(children: [
                Icon(Icons.cake_outlined, size: 52, color: Theme.of(context).colorScheme.primary),
                const SizedBox(height: 12),
                const Text('Your celebration calendar starts here',
                  textAlign: TextAlign.center, style: TextStyle(fontSize: 17, fontWeight: FontWeight.w600)),
                const SizedBox(height: 6),
                const Text('Add a birthday or anniversary to receive reminders.',
                  textAlign: TextAlign.center),
                const SizedBox(height: 16),
                FilledButton.icon(onPressed: () => _create(), icon: const Icon(Icons.add),
                  label: const Text('Add first occasion')),
              ])))
            else ..._items.map((item) {
              return Card(
              margin: const EdgeInsets.only(bottom: 10),
              child: ListTile(
                contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                leading: ProfileAvatar(
                  imageUrl: item['profilePhotoUrl'] as String?,
                  displayName: item['title'] as String?,
                  radius: 22,
                ),
                title: Text(item['title'] as String? ?? '',
                  style: const TextStyle(fontWeight: FontWeight.w600)),
                subtitle: Text([
                  item['kind'] as String? ?? 'Occasion',
                  if (item['occasionDate'] is String)
                    (item['occasionDate'] as String).split('T').first,
                  if (item['repeatsAnnually'] == true) 'Repeats yearly',
                ].join(' • ')),
                onTap: () => _create(item),
                trailing: PopupMenuButton<String>(
                  onSelected: (action) async {
                    if (action == 'edit') { await _create(item); return; }
                    final confirmed = await showDialog<bool>(context: context,
                      builder: (context) => AlertDialog(title: const Text('Delete occasion?'),
                        actions: [
                          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
                          TextButton(onPressed: () => Navigator.pop(context, true), child: const Text('Delete')),
                        ]));
                    if (confirmed != true) return;
                    try { await widget.api.delete('/family-occasions/${item['id']}'); await _load(); }
                    catch (_) { if (mounted) ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('Unable to delete occasion'))); }
                  },
                  itemBuilder: (_) => const [
                    PopupMenuItem(value: 'edit', child: Text('Edit')),
                    PopupMenuItem(value: 'delete', child: Text('Delete')),
                  ]),
              ),
              );
            }),
            const SizedBox(height: 80),
          ])),
    );
  }
}
