import 'package:flutter/material.dart';
import '../../core/services/corporate_service.dart';

/// Company profile and offer template editor for authorized HR staff.
class CompanyOfferSettingsScreen extends StatefulWidget {
  final String companyId;
  final String companyName;
  final bool canRename;
  final CorporateService service;

  const CompanyOfferSettingsScreen({
    super.key, required this.companyId, required this.service,
    required this.companyName, this.canRename = false,
  });

  @override
  State<CompanyOfferSettingsScreen> createState() => _CompanyOfferSettingsScreenState();
}

class _CompanyOfferSettingsScreenState extends State<CompanyOfferSettingsScreen> {
  final fields = <String, TextEditingController>{
    for (final key in [
      'addressLine1', 'addressLine2', 'city', 'state', 'postalCode',
      'countryCode', 'contactNumber', 'contactEmail', 'website',
    ]) key: TextEditingController(),
  };
  final templateBody = TextEditingController();
  late final companyName = TextEditingController(text: widget.companyName);
  bool loading = true;
  bool saving = false;
  String? error;
  int templateVersion = 0;

  @override
  void initState() {
    super.initState();
    load();
  }

  @override
  void dispose() {
    for (final field in fields.values) {
      field.dispose();
    }
    templateBody.dispose();
    companyName.dispose();
    super.dispose();
  }

  Future<void> load() async {
    try {
      final profile = await widget.service.companyProfile(widget.companyId);
      final template = await widget.service.companyOfferTemplate(widget.companyId);
      if (!mounted) return;
      for (final entry in fields.entries) {
        entry.value.text = (profile[entry.key] ?? '').toString();
      }
      final definition = template['template'];
      if (definition is Map) {
        templateBody.text = (definition['body'] ?? '').toString();
      }
      setState(() => templateVersion = (template['version'] as num?)?.toInt() ?? 0);
    } catch (e) {
      if (mounted) setState(() => error = 'Unable to load company settings: $e');
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> save() async {
    if (saving) return;
    setState(() { saving = true; error = null; });
    try {
      if (widget.canRename && companyName.text.trim() != widget.companyName.trim()) {
        await widget.service.renameCompany(widget.companyId, companyName.text.trim());
      }
      await widget.service.saveCompanyProfile(widget.companyId, {
        for (final entry in fields.entries) entry.key: entry.value.text.trim(),
      });
      if (templateBody.text.trim().isNotEmpty) {
        await widget.service.saveCompanyOfferTemplate(widget.companyId, {
          'body': templateBody.text.trim(),
        });
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Company settings saved')));
        await load();
      }
    } catch (e) {
      if (mounted) setState(() => error = 'Unable to save company settings: $e');
    } finally {
      if (mounted) setState(() => saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    const labels = {
      'addressLine1': 'Registered address line 1',
      'addressLine2': 'Address line 2',
      'city': 'City',
      'state': 'State / province',
      'postalCode': 'Postal code',
      'countryCode': 'Country code (ISO 2-letter)',
      'contactNumber': 'Company contact number',
      'contactEmail': 'Company email',
      'website': 'Company website',
    };
    return Scaffold(
      appBar: AppBar(title: const Text('Company Offer Settings')),
      body: loading ? const Center(child: CircularProgressIndicator())
          : ListView(padding: const EdgeInsets.all(20), children: [
              if (widget.canRename) ...[
                TextField(controller: companyName,
                  decoration: const InputDecoration(labelText: 'Registered company name',
                    border: OutlineInputBorder())),
                const SizedBox(height: 14),
              ],
              const Text('Company contact information',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
              const SizedBox(height: 12),
              for (final entry in fields.entries)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: TextField(
                    controller: entry.value,
                    decoration: InputDecoration(
                      labelText: labels[entry.key],
                      border: const OutlineInputBorder()),
                  ),
                ),
              const SizedBox(height: 12),
              Text('Offer template (version $templateVersion)',
                  style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
              const Text('Use approved placeholders such as {{EmployeeName}}, '
                  '{{JobTitle}}, {{StartDate}} and {{Currency}}. '
                  'Templates require HR and legal review before issuing.'),
              const SizedBox(height: 12),
              TextField(
                controller: templateBody,
                maxLines: 12,
                decoration: const InputDecoration(
                  labelText: 'Offer letter body',
                  border: OutlineInputBorder(),
                ),
              ),
              if (error != null) Padding(
                padding: const EdgeInsets.symmetric(vertical: 12),
                child: Text(error!, style: TextStyle(
                  color: Theme.of(context).colorScheme.error))),
              const SizedBox(height: 18),
              FilledButton.icon(
                onPressed: saving ? null : save,
                icon: const Icon(Icons.save_outlined),
                label: Text(saving ? 'Saving...' : 'Save company settings'),
              ),
              const SizedBox(height: 24),
            ]),
    );
  }
}
