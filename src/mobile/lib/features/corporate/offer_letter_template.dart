/// Pure, deterministic placeholder expansion for approved HR letter templates.
/// The caller must validate permissions and approve the final letter before
/// producing or storing a PDF. Never use this for access-control decisions.
class OfferLetterTemplate {
  static final RegExp _placeholder =
      RegExp(r'\{\{\s*([A-Za-z][A-Za-z0-9_]*)\s*\}\}');

  /// Returns all placeholders referenced by [template] in appearance order.
  static List<String> placeholders(String template) {
    final names = <String>{};
    for (final match in _placeholder.allMatches(template)) {
      names.add(match.group(1)!);
    }
    return names.toList(growable: false);
  }

  /// Renders only when every placeholder has a nonempty supplied value.
  /// This prevents accidentally issuing letters with unfilled salary fields.
  static String render(String template, Map<String, String> values) {
    final missing = placeholders(template)
        .where((name) => (values[name] ?? '').trim().isEmpty)
        .toList();
    if (missing.isNotEmpty) {
      throw ArgumentError('Missing offer letter fields: ${missing.join(', ')}');
    }
    return template.replaceAllMapped(
      _placeholder,
      (match) => values[match.group(1)!]!,
    );
  }

  /// Preview-only draft. Local legal review is required before issuance.
  /// Currency amounts and optional benefits must be explicitly confirmed by HR.
  static const defaultTemplate = '''
DRAFT EMPLOYMENT OFFER — SUBJECT TO LEGAL REVIEW

Offer date: {{OfferDate}}
Employee: {{EmployeeName}}
Company: {{CompanyName}}
Employment country: {{EmploymentCountry}}

Dear {{EmployeeName}},

We are pleased to offer you the role of {{JobTitle}} in the
{{Department}} department of {{CompanyName}}.

Employment type: {{EmploymentType}}
Work location: {{WorkLocation}}
Proposed start date: {{StartDate}}

COMPENSATION
Basic salary: {{Currency}} {{BasicSalary}} per {{PayFrequency}}
Allowances: {{Currency}} {{Allowance}} per {{PayFrequency}}
Bonus terms: {{BonusTerms}}
Benefits: {{Benefits}}

EMPLOYMENT CONDITIONS
Probation period: {{ProbationPeriod}}
Notice period: {{NoticePeriod}}

This is a draft offer for HR review, not a signed employment agreement.
Employment conditions, statutory entitlements and any immigration
requirements are subject to applicable law and final agreement.

Sincerely,
{{SignatoryName}}
{{SignatoryTitle}}
{{CompanyName}}
''';
}
