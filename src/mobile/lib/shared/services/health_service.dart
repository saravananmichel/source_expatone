import '../../core/networking/api_client.dart';
import '../../core/error/result.dart';

class HealthStatus {
  final String status;
  final String database;
  final String service;
  final String version;

  const HealthStatus({
    required this.status,
    required this.database,
    required this.service,
    required this.version,
  });

  factory HealthStatus.fromJson(Map<String, dynamic> json) {
    return HealthStatus(
      status: json['status'] as String? ?? 'unknown',
      database: json['database'] as String? ?? 'unknown',
      service: json['service'] as String? ?? 'unknown',
      version: json['version'] as String? ?? 'unknown',
    );
  }

  bool get isHealthy => status == 'healthy';
}

class HealthService {
  final ApiClient _apiClient;

  HealthService(this._apiClient);

  Future<Result<HealthStatus>> checkHealth() async {
    try {
      final response = await _apiClient.get('/health');
      return Result.success(HealthStatus.fromJson(response));
    } catch (e) {
      return Result.failure('Unable to connect to ExpatOne. Please check your connection and try again.');
    }
  }
}
