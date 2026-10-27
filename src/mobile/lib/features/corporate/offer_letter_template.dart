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

  static const defaultTemplate = '''
EMPLOYMENT OFFER LETTER

Date: {{OfferDate}}

Dear {{EmployeeName}},

We are pleased to offer you the position of {{JobTitle}} with
{{CompanyName}} at {{WorkLocation}}, commencing {{StartDate}}.

Your basic salary will be {{Currency}} {{BasicSalary}} per {{PayFrequency}}.
Your fixed allowance will be {{Currency}} {{Allowance}} per {{PayFrequency}}.

Your probation period will be {{ProbationPeriod}}.
Your notice period will be {{NoticePeriod}}.

This letter is subject to the final employment agreement and applicable law.

Sincerely,
{{SignatoryName}}
{{SignatoryTitle}}
{{CompanyName}}
''';
}
