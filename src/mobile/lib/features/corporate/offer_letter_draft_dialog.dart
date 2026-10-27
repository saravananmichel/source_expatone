import 'package:flutter/material.dart';
import 'offer_letter_template.dart';

/// HR-only preview. Does not issue, sign, persist or deliver a letter.
class OfferLetterDraftDialog extends StatefulWidget {
  final String employeeName;
  final String companyName;
  final String employmentCountry;
  final String? startDate;

  const OfferLetterDraftDialog({
    super.key,
    required this.employeeName,
    required this.companyName,
    required this.employmentCountry,
    this.startDate,
  });

  @override
  State<OfferLetterDraftDialog> createState() => _OfferLetterDraftDialogState();
}

class _OfferLetterDraftDialogState extends State<OfferLetterDraftDialog> {
  late final Map<String, TextEditingController> fields;
  String? preview;
  String? error;

  static const labels = <String, String>{
    'OfferDate': 'Offer date',
    'EmployeeName': 'Employee name',
    'CompanyName': 'Company name',
    'EmploymentCountry': 'Employment country',
    'JobTitle': 'Job title',
    'Department': 'Department',
    'EmploymentType': 'Employment type',
    'WorkLocation': 'Work location',
    'StartDate': 'Start date',
    'Currency': 'Currency (e.g. MYR)',
    'BasicSalary': 'Basic salary',
    'PayFrequency': 'Pay frequency',
    'Allowance': 'Fixed allowance (enter 0 if none)',
    'BonusTerms': 'Bonus terms (enter None if none)',
    'Benefits': 'Benefits (enter None if none)',
    'ProbationPeriod': 'Probation period',
    'NoticePeriod': 'Notice period',
    'SignatoryName': 'Signatory name',
    'SignatoryTitle': 'Signatory title',
  };

  @override
  void initState() {
    super.initState();
    final now = DateTime.now();
    final date = '${now.year}-${now.month.toString().padLeft(2, '0')}-${now.day.toString().padLeft(2, '0')}';
    final defaults = <String, String>{
      'OfferDate': date,
      'EmployeeName': widget.employeeName,
      'CompanyName': widget.companyName,
      'EmploymentCountry': widget.employmentCountry,
      'StartDate': widget.startDate?.split('T').first ?? '',
    };
    fields = {
      for (final key in OfferLetterTemplate.placeholders(OfferLetterTemplate.defaultTemplate))
        key: TextEditingController(text: defaults[key] ?? ''),
    };
  }

  @override
  void dispose() {
    for (final field in fields.values) {
      field.dispose();
    }
    super.dispose();
  }

  void generatePreview() {
    try {
      final values = fields.map((key, value) => MapEntry(key, value.text.trim()));
      final salary = double.tryParse(values['BasicSalary'] ?? '');
      final allowance = double.tryParse(values['Allowance'] ?? '');
      if (salary == null || salary < 0 || allowance == null || allowance < 0) {
        throw ArgumentError('Basic salary and allowance must be non-negative numbers.');
      }
      setState(() {
        preview = OfferLetterTemplate.render(OfferLetterTemplate.defaultTemplate, values);
        error = null;
      });
    } catch (e) {
      setState(() {
        error = e.toString();
        preview = null;
      });
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Draft employment offer — preview only'),
    content: SizedBox(
      width: 620,
      child: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('Complete all fields before preview. This draft is not issued, '
                'legally reviewed, signed, saved or sent to the employee.'),
            const SizedBox(height: 12),
            for (final entry in fields.entries)
              Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: TextField(
                  controller: entry.value,
                  maxLines: entry.key == 'Benefits' || entry.key == 'BonusTerms' ? 2 : 1,
                  decoration: InputDecoration(
                    labelText: labels[entry.key] ?? entry.key,
                    border: const OutlineInputBorder(),
                  ),
                  onChanged: (_) {
                    if (preview != null) setState(() => preview = null);
                  },
                ),
              ),
            if (error != null)
              Text(error!, style: const TextStyle(color: Colors.red)),
            if (preview != null) ...[
              const Divider(),
              const Text('Draft preview', style: TextStyle(fontWeight: FontWeight.bold)),
              const SizedBox(height: 8),
              SelectableText(preview!),
            ],
          ],
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('Close'),
      ),
      FilledButton.icon(
        onPressed: generatePreview,
        icon: const Icon(Icons.preview_outlined),
        label: const Text('Preview draft'),
      ),
    ],
  );
}
