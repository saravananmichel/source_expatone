import '../networking/api_client.dart';

class CorporateService {
  final ApiClient api;
  CorporateService(this.api);
  Future<List<Map<String,dynamic>>> companies() => api.getList('/api/corporate/companies');
  Future<List<Map<String,dynamic>>> invitations() => api.getList('/api/corporate/invitations');
  Future<Map<String,dynamic>> createCompany(String name) => api.post('/api/corporate/companies',body:{'name':name});
  Future<Map<String,dynamic>> invite(String companyId,String email) => api.post('/api/corporate/companies/$companyId/invite',body:{'email':email});
  Future<Map<String,dynamic>> accept(String id) => api.post('/api/corporate/invitations/$id/accept');
  Future<Map<String,dynamic>> decline(String id) => api.post('/api/corporate/invitations/$id/decline');
  Future<Map<String,dynamic>> dashboard(String id) => api.get('/api/corporate/companies/$id/dashboard');
  Future<List<Map<String,dynamic>>> employees(String id) => api.getList('/api/corporate/companies/$id/employees');
  Future<List<Map<String,dynamic>>> cases(String id) => api.getList('/api/corporate/companies/$id/cases');
  Future<Map<String,dynamic>> createCase(String id,String employeeId,String title,DateTime? due) =>
    api.post('/api/corporate/companies/$id/cases',body:{'employeeUserId':employeeId,'title':title,'dueAt':due?.toUtc().toIso8601String()});
  Future<Map<String,dynamic>> updateCase(String id,String caseId,String status) =>
    api.put('/api/corporate/companies/$id/cases/$caseId',body:{'status':status});
  Future<Map<String,dynamic>> linkVisa(String id,String caseId,String visaId) =>
    api.post('/api/corporate/companies/$id/cases/$caseId/link-my-visa',body:{'visaApplicationId':visaId});
  Future<void> unlinkVisa(String id,String caseId) => api.postVoid('/api/corporate/companies/$id/cases/$caseId/unlink-my-visa');
}
