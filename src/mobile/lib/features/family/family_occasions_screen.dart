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
  @override
  void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    try {
      final items = await widget.api.getList('/family-occasions');
      if (mounted) setState(() { _items = items; _loading = false; });
    } catch (_) {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _create() async {
    final title = TextEditingController();
    String kind = 'Birthday';
    int offset = 7;
    DateTime date = DateTime.now();
    final saved = await showDialog<bool>(context: context, builder: (context) =>
      StatefulBuilder(builder: (context, update) => AlertDialog(
        title: const Text('Add family occasion'),
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
              await widget.api.post('/family-occasions', body: {
                'title': title.text.trim(), 'kind': kind,
                'occasionDate': DateTime.utc(date.year, date.month, date.day).toIso8601String(),
                'repeatsAnnually': true, 'remindersEnabled': true, 'daysBefore': offset,
              });
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
      ListView(children: _items.map((item) => ListTile(
        leading: const Icon(Icons.event),
        title: Text(item['title'] as String? ?? ''),
        subtitle: Text(item['kind'] as String? ?? ''),
      )).toList()),
  );
}
