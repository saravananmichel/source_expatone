import '../networking/api_client.dart';

class VisaService {
  final ApiClient api;
  VisaService(this.api);
  Future<Map<String, dynamic>> catalog() => api.get('/visa-applications/catalog');
  Future<List<Map<String, dynamic>>> list() => api.getList('/visa-applications');
  Future<Map<String, dynamic>> get(String id) => api.get('/visa-applications/$id');
  Future<Map<String, dynamic>> create(String template, List<Map<String, dynamic>> docs) => api.post('/visa-applications', body: {'templateId': template, 'documents': docs});
  Future<Map<String, dynamic>> save(String id, String revision, Map<String, String> values, Map<String, dynamic> checks) => api.put('/visa-applications/$id', body: {'revision': revision, 'values': values, 'checks': checks});
  Future<Map<String, dynamic>> documents(String id, String revision, List<Map<String, dynamic>> docs) => api.put('/visa-applications/$id/documents', body: {'revision': revision, 'documents': docs});
  Future<Map<String, dynamic>> action(String id, String revision, String action) => api.post('/visa-applications/$id/$action', body: {'revision': revision}, timeout: const Duration(seconds: 45));
  Future<void> delete(String id, String revision) async { await api.delete('/visa-applications/$id?revision=$revision'); }
}
