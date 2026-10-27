import 'package:flutter/material.dart';
import '../../core/services/corporate_service.dart';
import 'offer_letter_draft_dialog.dart';

/// Loads company-scoped offer data before displaying the draft dialog.
/// Call this from the corporate offer action instead of constructing the
/// dialog with empty dropdown catalogs.
Future<void> showCompanyOfferDialog({
  required BuildContext context,
  required CorporateService service,
  required String companyId,
  required String employeeId,
  required String employeeName,
  required String companyName,
  required String employmentCountry,
  required Future<void> Function(String renderedText) onIssue,
  String? startDate,
  int? replacementVersion,
}) async {
  final results = await Future.wait<dynamic>([
    service.offerCountries(),
    service.offerCurrencies(),
    service.companyBenefits(companyId),
    service.offerSignatories(companyId),
    service.companyOfferTemplate(companyId),
  ]);
  if (!context.mounted) return;
  final template = results[4] as Map<String, dynamic>;
  final definition = template['template'];
  final body = definition is Map ? definition['body']?.toString() : null;
  await showDialog<void>(
    context: context,
    barrierDismissible: false,
    builder: (context) => OfferLetterDraftDialog(
      employeeName: employeeName,
      companyName: companyName,
      employmentCountry: employmentCountry,
      startDate: startDate,
      replacementVersion: replacementVersion,
      onIssue: onIssue,
      onSaveDraft: (text) async {
        await service.saveOfferDraft(companyId, employeeId, text);
      },
      countries: results[0] as List<Map<String, dynamic>>,
      currencies: results[1] as List<Map<String, dynamic>>,
      benefits: results[2] as List<Map<String, dynamic>>,
      signatories: results[3] as List<Map<String, dynamic>>,
      companyTemplateBody: body,
    ),
  );
}
