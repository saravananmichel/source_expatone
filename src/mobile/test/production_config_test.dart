import 'package:flutter_test/flutter_test.dart';
import 'package:expatone_app/core/constants/api_constants.dart';
import 'package:expatone_app/core/config/app_config.dart';

void main() {
  group('Production configuration guard', () {
    // These tests verify that the production URLs are not local/emulator addresses
    // and that the URL validation logic rejects insecure configurations.

    test('AppConfig.production URL starts with https', () {
      expect(
        AppConfig.production.apiBaseUrl.startsWith('https://'),
        isTrue,
        reason: 'Production API URL must use HTTPS',
      );
    });

    test('AppConfig.production URL does not contain 10.0.2.2', () {
      expect(
        AppConfig.production.apiBaseUrl.contains('10.0.2.2'),
        isFalse,
        reason: 'Production URL must not point to Android emulator',
      );
    });

    test('AppConfig.production URL does not contain localhost', () {
      expect(
        AppConfig.production.apiBaseUrl.toLowerCase().contains('localhost'),
        isFalse,
        reason: 'Production URL must not point to localhost',
      );
    });

    test('AppConfig.staging URL starts with https', () {
      expect(
        AppConfig.staging.apiBaseUrl.startsWith('https://'),
        isTrue,
        reason: 'Staging API URL must use HTTPS',
      );
    });

    test('ApiConstants._isProductionUrl rejects 10.0.2.2', () {
      expect(
        ApiConstants.isProductionUrlForTest('http://10.0.2.2:5000/api'),
        isFalse,
      );
    });

    test('ApiConstants._isProductionUrl rejects localhost', () {
      expect(
        ApiConstants.isProductionUrlForTest('http://localhost:5000/api'),
        isFalse,
      );
    });

    test('ApiConstants._isProductionUrl rejects http production-looking URL', () {
      expect(
        ApiConstants.isProductionUrlForTest('http://api.expatone.com/api'),
        isFalse,
        reason: 'Even a real domain must use HTTPS',
      );
    });

    test('ApiConstants._isProductionUrl rejects 127.0.0.1', () {
      expect(
        ApiConstants.isProductionUrlForTest('https://127.0.0.1/api'),
        isFalse,
      );
    });

    test('ApiConstants._isProductionUrl accepts valid HTTPS production URL', () {
      expect(
        ApiConstants.isProductionUrlForTest('https://api.expatone.com/api'),
        isTrue,
      );
    });

    test('ApiConstants.baseUrl in test environment is the emulator default', () {
      // In the test environment dart.vm.product is false, so the assertion
      // does not fire. The URL may be the dev default.
      // This just confirms the getter works without throwing.
      expect(ApiConstants.baseUrl, isA<String>());
      expect(ApiConstants.baseUrl, isNotEmpty);
    });
  });
}
