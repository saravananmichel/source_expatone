import 'package:flutter_test/flutter_test.dart';
import 'package:expatone_app/shared/services/health_service.dart';

void main() {
  group('HealthStatus.isHealthy', () {
    test('"ok" is considered healthy', () {
      final status = HealthStatus.fromJson({'status': 'ok'});
      expect(status.isHealthy, isTrue);
    });

    test('"healthy" is considered healthy', () {
      final status = HealthStatus.fromJson({'status': 'healthy'});
      expect(status.isHealthy, isTrue);
    });

    test('any other status is not healthy', () {
      for (final s in ['degraded', 'error', 'unknown', '']) {
        final status = HealthStatus.fromJson({'status': s});
        expect(status.isHealthy, isFalse, reason: 'status "$s" should not be healthy');
      }
    });
  });
}
