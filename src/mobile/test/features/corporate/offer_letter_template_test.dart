import 'package:flutter_test/flutter_test.dart';
import 'package:expatone_app/features/corporate/offer_letter_template.dart';

void main() {
  test('extracts distinct placeholders in appearance order', () {
    expect(
      OfferLetterTemplate.placeholders('{{EmployeeName}} {{BasicSalary}} {{EmployeeName}}'),
      ['EmployeeName', 'BasicSalary'],
    );
  });

  test('fills offer letter placeholders', () {
    expect(
      OfferLetterTemplate.render(
        'Dear {{EmployeeName}}, salary {{Currency}} {{BasicSalary}}',
        {'EmployeeName': 'Asha', 'Currency': 'MYR', 'BasicSalary': '8500'},
      ),
      'Dear Asha, salary MYR 8500',
    );
  });

  test('refuses incomplete salary details', () {
    expect(
      () => OfferLetterTemplate.render(
        '{{EmployeeName}}: {{BasicSalary}}',
        {'EmployeeName': 'Asha'},
      ),
      throwsArgumentError,
    );
  });
}
