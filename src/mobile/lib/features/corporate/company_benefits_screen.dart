import 'package:flutter/material.dart';
import '../../core/services/corporate_service.dart';

/// HR-managed benefits catalog. Only company-scoped API endpoints are used.
class CompanyBenefitsScreen extends StatefulWidget {
  final String companyId;
  final CorporateService service;

  const CompanyBenefitsScreen({
    super.key,
    required this.companyId,
    required this.service,
  });

  @override
  State<CompanyBenefitsScreen> createState() => _CompanyBenefitsScreenState();
}

class _CompanyBenefitsScreenState extends State<CompanyBenefitsScreen> {
  List<Map<String, dynamic>> benefits = [];
  bool loading = true;
  bool includeInactive = false;
  String? error;

  static const categories = [
    'Health & Medical', 'Paid Leave', 'Retirement & Provident Fund',
    'Bonuses & Incentives', 'Work Flexibility', 'Allowances',
    'Wellness & Additional Perks', 'Other',
  ];

  @override
  void initState() {
    super.initState();
    refresh();
  }

  Future<void> refresh() async {
    setState(() { loading = true; error = null; });
    try {
      final data = await widget.service.companyBenefits(
        widget.companyId, includeInactive: includeInactive);
      if (mounted) setState(() => benefits = data);
    } catch (e) {
      if (mounted) setState(() => error = 'Unable to load benefits: $e');
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> edit([Map<String, dynamic>? existing]) async {
    final name = TextEditingController(text: (existing?['name'] ?? '').toString());
    final description = TextEditingController(
        text: (existing?['description'] ?? '').toString());
    String category = (existing?['category'] ?? categories.first).toString();
    if (!categories.contains(category)) category = categories.last;
    bool active = existing?['isActive'] != false;
    try {
      final saved = await showDialog<bool>(
        context: context,
        builder: (dialogContext) => StatefulBuilder(
          builder: (context, update) => AlertDialog(
            title: Text(existing == null ? 'Add company benefit' : 'Edit benefit'),
            content: SizedBox(
              width: 480,
              child: SingleChildScrollView(
                child: Column(mainAxisSize: MainAxisSize.min, children: [
                  DropdownButtonFormField<String>(
                    initialValue: category,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'Category'),
                    items: categories.map((c) => DropdownMenuItem(
                      value: c, child: Text(c))).toList(),
                    onChanged: (value) {
                      if (value != null) update(() => category = value);
                    },
                  ),
                  TextField(controller: name, maxLength: 160,
                    decoration: const InputDecoration(labelText: 'Benefit name')),
                  TextField(controller: description, maxLength: 4000,
                    minLines: 2, maxLines: 4,
                    decoration: const InputDecoration(labelText: 'Description / entitlement')),
                  SwitchListTile(
                    title: const Text('Active'),
                    value: active,
                    onChanged: (value) => update(() => active = value),
                  ),
                ]),
              ),
            ),
            actions: [
              TextButton(onPressed: () => Navigator.pop(context, false),
                child: const Text('Cancel')),
              FilledButton(
                onPressed: () async {
                  if (name.text.trim().isEmpty) return;
                  try {
                    final payload = <String, dynamic>{
                      'category': category,
                      'name': name.text.trim(),
                      'description': description.text.trim(),
                      'isActive': active,
                    };
                    if (existing == null) {
                      await widget.service.createCompanyBenefit(widget.companyId, payload);
                    } else {
                      await widget.service.updateCompanyBenefit(
                        widget.companyId, existing['id'].toString(), payload);
                    }
                    if (dialogContext.mounted) Navigator.pop(dialogContext, true);
                  } catch (e) {
                    if (dialogContext.mounted) {
                      ScaffoldMessenger.of(dialogContext).showSnackBar(
                        SnackBar(content: Text('Save failed: $e')));
                    }
                  }
                },
                child: const Text('Save'),
              ),
            ],
          ),
        ),
      );
      if (saved == true && mounted) await refresh();
    } finally {
      name.dispose();
      description.dispose();
    }
  }

  Future<void> deactivateBenefit(Map<String, dynamic> benefit) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Deactivate benefit?'),
        content: Text('Remove "${benefit['name']}" from future offers? '
            'Existing issued offers will retain their historical details.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.pop(context, true),
            child: const Text('Deactivate')),
        ],
      ),
    );
    if (confirmed != true) return;
    try {
      await widget.service.deactivateCompanyBenefit(
          widget.companyId, benefit['id'].toString());
      if (mounted) await refresh();
    } catch (e) {
      if (mounted) setState(() => error = 'Unable to deactivate benefit: $e');
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Company Benefits')),
    floatingActionButton: FloatingActionButton.extended(
      onPressed: () => edit(),
      icon: const Icon(Icons.add),
      label: const Text('Add benefit'),
    ),
    body: Column(children: [
      SwitchListTile(
        title: const Text('Include inactive benefits'),
        value: includeInactive,
        onChanged: (value) {
          setState(() => includeInactive = value);
          refresh();
        },
      ),
      if (error != null) Padding(
        padding: const EdgeInsets.all(12),
        child: Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
      Expanded(child: loading
        ? const Center(child: CircularProgressIndicator())
        : benefits.isEmpty
          ? const Center(child: Text('No benefits configured for this company.'))
          : ListView.builder(
              itemCount: benefits.length,
              itemBuilder: (context, index) {
                final benefit = benefits[index];
                final active = benefit['isActive'] == true;
                return ListTile(
                  title: Text((benefit['name'] ?? '').toString()),
                  subtitle: Text('${benefit['category'] ?? ''}\n${benefit['description'] ?? ''}'),
                  isThreeLine: true,
                  trailing: Wrap(children: [
                    if (!active) const Chip(label: Text('Inactive')),
                    IconButton(icon: const Icon(Icons.edit_outlined),
                      tooltip: 'Edit', onPressed: () => edit(benefit)),
                    if (active) IconButton(icon: const Icon(Icons.delete_outline),
                      tooltip: 'Deactivate', onPressed: () => deactivateBenefit(benefit)),
                  ]),
                );
              },
            )),
    ]),
  );
}
