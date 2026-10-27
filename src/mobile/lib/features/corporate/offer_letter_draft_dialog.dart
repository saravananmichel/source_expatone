import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:printing/printing.dart';
import 'dart:typed_data';
import 'offer_letter_template.dart';

/// HR-only offer preview and explicit issuance into the employee wallet.
class OfferLetterDraftDialog extends StatefulWidget {
  final Future<void> Function(String renderedText, List<String> benefitIds) onIssue;
  final Future<List<int>> Function(String renderedText)? onPreviewPdf;
  final Future<void> Function(String renderedText)? onSaveDraft;
  final Future<void> Function(String draftId, String renderedText)? onUpdateDraft;
  final List<Map<String, dynamic>> savedDrafts;
  final int? replacementVersion;
  final String employeeName;
  final String companyName;
  final String employmentCountry;
  final String? startDate;
  final String? companyTemplateBody;
  final List<Map<String, dynamic>> countries;
  final List<Map<String, dynamic>> currencies;
  final List<Map<String, dynamic>> benefits;
  final List<Map<String, dynamic>> signatories;

  const OfferLetterDraftDialog({
    super.key,
    required this.employeeName,
    this.replacementVersion,
    required this.onIssue,
    this.onPreviewPdf,
    this.onSaveDraft,
    this.onUpdateDraft,
    this.savedDrafts = const [],
    required this.companyName,
    required this.employmentCountry,
    this.startDate,
    this.companyTemplateBody,
    this.countries = const [],
    this.currencies = const [],
    this.benefits = const [],
    this.signatories = const [],
  });

  @override
  State<OfferLetterDraftDialog> createState() => _OfferLetterDraftDialogState();
}

class _OfferLetterDraftDialogState extends State<OfferLetterDraftDialog> {
  late final Map<String, TextEditingController> fields;
  String? preview;
  String? editingDraftId;
  bool editingSavedText = false;
  late final TextEditingController savedText;
  String? error;
  bool issuing = false;
  final Set<String> selectedBenefits = {};

  static const employmentTypes = ['Permanent', 'Fixed-term Contract',
    'Temporary', 'Part-time', 'Internship', 'Consultant'];
  static const payFrequencies = ['Weekly', 'Bi-Weekly', 'Monthly'];

  void updateField(String key, String value) {
    fields[key]?.text = value;
    if (preview != null) setState(() => preview = null);
  }

  Future<void> chooseDate(String key) async {
    final initial = DateTime.tryParse(fields[key]?.text ?? '') ?? DateTime.now();
    final date = await showDatePicker(
      context: context, initialDate: initial,
      firstDate: DateTime(1900), lastDate: DateTime(2200),
    );
    if (date == null || !mounted) return;
    updateField(key, '${date.year.toString().padLeft(4, '0')}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')}');
    setState(() {});
  }

  Widget offerInput(String key, TextEditingController controller) {
    if (key == 'OfferDate' || key == 'StartDate') {
      return TextField(
        controller: controller, readOnly: true,
        decoration: InputDecoration(labelText: labels[key],
          border: const OutlineInputBorder(), suffixIcon: const Icon(Icons.calendar_month)),
        onTap: () => chooseDate(key),
      );
    }
    List<Map<String, dynamic>>? catalog;
    if (key == 'EmploymentCountry' || key == 'WorkLocation') catalog = widget.countries;
    if (key == 'Currency') catalog = widget.currencies;
    if (key == 'SignatoryName') catalog = widget.signatories;
    if (catalog != null && catalog.isNotEmpty) {
      final options = catalog;
      final values = options.map((row) => key == 'SignatoryName'
          ? (row['name'] ?? '').toString()
          : (row['code'] ?? '').toString()).where((v) => v.isNotEmpty).toSet().toList();
      final current = values.contains(controller.text) ? controller.text : null;
      return DropdownButtonFormField<String>(
        initialValue: current,
        decoration: InputDecoration(labelText: labels[key], border: const OutlineInputBorder()),
        items: options.where((row) => values.contains(key == 'SignatoryName'
            ? (row['name'] ?? '').toString() : (row['code'] ?? '').toString()))
          .map((row) {
            final value = key == 'SignatoryName' ? (row['name'] ?? '').toString()
                : (row['code'] ?? '').toString();
            final label = key == 'SignatoryName' ? value
                : '${row['name'] ?? value} ($value)';
            return DropdownMenuItem(value: value, child: Text(label, overflow: TextOverflow.ellipsis));
          }).toList(),
        onChanged: (value) {
          if (value == null) return;
          updateField(key, value);
          if (key == 'SignatoryName') {
            final row = options.firstWhere((item) => item['name'] == value);
            updateField('SignatoryTitle', (row['title'] ?? '').toString());
          }
          setState(() {});
        },
      );
    }
    if (key == 'EmploymentType' || key == 'PayFrequency') {
      final choices = key == 'EmploymentType' ? employmentTypes : payFrequencies;
      return DropdownButtonFormField<String>(
        initialValue: choices.contains(controller.text) ? controller.text : null,
        decoration: InputDecoration(labelText: labels[key], border: const OutlineInputBorder()),
        items: choices.map((v) => DropdownMenuItem(value: v, child: Text(v))).toList(),
        onChanged: (value) { if (value != null) { updateField(key, value); setState(() {}); } },
      );
    }
    if (key == 'Benefits' && widget.benefits.isNotEmpty) {
      return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        const Text('Select company benefits'),
        ...widget.benefits.map((benefit) {
          final id = (benefit['id'] ?? '').toString();
          final name = (benefit['name'] ?? '').toString();
          return CheckboxListTile(
            dense: true, title: Text(name),
            subtitle: Text((benefit['description'] ?? '').toString()),
            value: selectedBenefits.contains(id),
            onChanged: (checked) {
              setState(() {
                if (checked == true) { selectedBenefits.add(id); }
                else { selectedBenefits.remove(id); }
                fields['Benefits']?.text = widget.benefits
                  .where((b) => selectedBenefits.contains((b['id'] ?? '').toString()))
                  .map((b) => '${b['name']}: ${b['description'] ?? ''}').join('; ');
                preview = null;
              });
            },
          );
        }),
      ]);
    }
    return TextField(
      controller: controller,
      readOnly: key == 'SignatoryTitle' && widget.signatories.isNotEmpty,
      maxLines: key == 'Benefits' || key == 'BonusTerms' ? 2 : 1,
      decoration: InputDecoration(labelText: labels[key] ?? key,
        border: const OutlineInputBorder()),
      onChanged: (_) { if (preview != null) setState(() => preview = null); },
    );
  }


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
    savedText = TextEditingController();
    fields = {
      for (final key in OfferLetterTemplate.placeholders(widget.companyTemplateBody?.trim().isNotEmpty == true
          ? widget.companyTemplateBody! : OfferLetterTemplate.defaultTemplate))
        key: TextEditingController(text: defaults[key] ?? ''),
    };
  }

  @override
  void dispose() {
    for (final field in fields.values) {
      field.dispose();
    }
    savedText.dispose();
    super.dispose();
  }

  void generatePreview() {
    if (editingSavedText) {
      if (savedText.text.trim().isEmpty) {
        setState(() { error = 'Draft text cannot be empty.'; preview = null; });
      } else {
        setState(() { preview = savedText.text.trim(); error = null; });
      }
      return;
    }
    try {
      final values = fields.map((key, value) => MapEntry(key, value.text.trim()));
      if (fields.containsKey('Benefits') && (values['Benefits'] ?? '').isEmpty) {
        values['Benefits'] = 'None';
      }
      final salary = double.tryParse(values['BasicSalary'] ?? '');
      final allowance = double.tryParse(values['Allowance'] ?? '');
      if (salary == null || salary < 0 || allowance == null || allowance < 0) {
        throw ArgumentError('Basic salary and allowance must be non-negative numbers.');
      }
      setState(() {
        preview = OfferLetterTemplate.render(
          widget.companyTemplateBody?.trim().isNotEmpty == true
              ? widget.companyTemplateBody! : OfferLetterTemplate.defaultTemplate,
          values,
        );
        error = null;
      });
    } catch (e) {
      setState(() {
        error = e.toString();
        preview = null;
      });
    }
  }

  Future<void> saveDraft() async {
    if (issuing || widget.onSaveDraft == null) return;
    if (preview == null) generatePreview();
    final current = preview;
    if (current == null) return;
    setState(() { issuing = true; error = null; });
    try {
      if (editingDraftId != null && widget.onUpdateDraft != null) {
        await widget.onUpdateDraft!(editingDraftId!, current);
      } else {
        await widget.onSaveDraft!(current);
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Offer draft saved to server.')));
      }
    } catch (e) {
      if (mounted) setState(() => error = 'Could not save draft: $e');
    } finally {
      if (mounted) setState(() => issuing = false);
    }
  }

  Future<void> showPdfPreview() async {
    if (issuing || widget.onPreviewPdf == null) return;
    if (preview == null) generatePreview();
    final text = preview;
    if (text == null) return;
    setState(() { issuing = true; error = null; });
    try {
      final bytes = await widget.onPreviewPdf!(text);
      if (!mounted) return;
      await Navigator.of(context).push(MaterialPageRoute<void>(
        builder: (context) => Scaffold(
          appBar: AppBar(title: const Text('Offer PDF preview — not issued')),
          body: PdfPreview(
            build: (_) async => Uint8List.fromList(bytes),
            canChangeOrientation: false,
            canChangePageFormat: false,
          ),
        ),
      ));
    } catch (e) {
      if (mounted) setState(() => error = 'Could not preview PDF: $e');
    } finally {
      if (mounted) setState(() => issuing = false);
    }
  }

  Future<void> issue() async {
    final current = preview;
    if (current == null || issuing) return;
    final approved = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Confirm offer issuance'),
        content: const Text('The reviewed offer PDF will be issued to the employee wallet. This action cannot be treated as an employee signature. Continue?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Review again')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Issue offer')),
        ],
      ),
    );
    if (approved != true || !mounted) return;
    setState(() { issuing = true; error = null; });
    try {
      await widget.onIssue(current, selectedBenefits.toList());
      if (mounted) Navigator.pop(context);
    } catch (e) {
      if (mounted) setState(() => error = 'Could not issue offer: $e');
    } finally {
      if (mounted) setState(() => issuing = false);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(widget.replacementVersion == null
        ? 'Employment offer — review and issue'
        : 'Replacement offer v${widget.replacementVersion} — review and issue'),
    content: SizedBox(
      width: 620,
      child: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('Complete all fields and review the preview carefully. Issuing stores a PDF in the employee Document Wallet. This generic draft is not legal advice or an electronic signature.'),
            const SizedBox(height: 12),
            if (widget.savedDrafts.isNotEmpty) ...[
              DropdownButtonFormField<String>(
                decoration: const InputDecoration(labelText: 'Resume a saved draft',
                  border: OutlineInputBorder()),
                items: widget.savedDrafts.map((row) => DropdownMenuItem<String>(
                  value: row['id'].toString(),
                  child: Text('Saved ${row['updatedAt'] ?? row['createdAt'] ?? row['id']}'),
                )).toList(),
                onChanged: (id) {
                  if (id == null) return;
                  final draft = widget.savedDrafts.firstWhere((row) => row['id'].toString() == id);
                  try {
                    final json = jsonDecode(draft['templateDataJson'].toString());
                    final text = json is Map ? (json['text'] ?? '').toString() : '';
                    setState(() {
                      editingDraftId = id;
                      editingSavedText = true;
                      savedText.text = text;
                      preview = null;
                      error = null;
                    });
                  } catch (_) {
                    setState(() => error = 'Saved draft could not be loaded.');
                  }
                },
              ),
              const SizedBox(height: 12),
            ],
            if (editingSavedText) ...[
              const Text('Editing saved offer text. Review all terms before issuing.'),
              TextField(controller: savedText, minLines: 10, maxLines: 18,
                decoration: const InputDecoration(border: OutlineInputBorder(),
                  labelText: 'Saved offer text'),
                onChanged: (_) => setState(() => preview = null)),
              TextButton(onPressed: () => setState(() {
                editingSavedText = false; editingDraftId = null; preview = null;
              }), child: const Text('Start a new offer')),
            ] else for (final entry in fields.entries)
              Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: offerInput(entry.key, entry.value),
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
        onPressed: issuing ? null : () => Navigator.pop(context),
        child: const Text('Close'),
      ),
      if (widget.onSaveDraft != null)
        OutlinedButton.icon(
          onPressed: issuing ? null : saveDraft,
          icon: const Icon(Icons.save_outlined),
          label: const Text('Save draft'),
        ),
      FilledButton.icon(
        onPressed: issuing ? null : generatePreview,
        icon: const Icon(Icons.preview_outlined),
        label: const Text('Preview draft'),
      ),
      if (preview != null && widget.onPreviewPdf != null)
        OutlinedButton.icon(
          onPressed: issuing ? null : showPdfPreview,
          icon: const Icon(Icons.picture_as_pdf_outlined),
          label: const Text('Preview PDF'),
        ),
      if (preview != null)
        FilledButton.icon(
          onPressed: issuing ? null : issue,
          icon: const Icon(Icons.send_outlined),
          label: Text(issuing ? 'Issuing…' : 'Issue to employee wallet'),
        ),
    ],
  );
}
