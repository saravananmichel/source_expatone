import '../networking/api_client.dart';

class FamilyUser {
  final String id, email;
  final String? displayName;
  FamilyUser.fromJson(Map<String, dynamic> json)
      : id = json['id'] as String, email = json['email'] as String,
        displayName = json['displayName'] as String?;
}
class FamilyRequest {
  final String id, status, revision;
  final FamilyUser requester, recipient;
  final bool isIncoming;
  final List<String> documentIds;
  FamilyRequest.fromJson(Map<String, dynamic> json)
      : id = json['id'] as String, status = json['status'] as String,
        revision = json['revision'] as String,
        requester = FamilyUser.fromJson(json['requester'] as Map<String, dynamic>),
        recipient = FamilyUser.fromJson(json['recipient'] as Map<String, dynamic>),
        isIncoming = json['isIncoming'] as bool,
        documentIds = (json['documentIds'] as List).cast<String>();
}
class FamilyService {
  final ApiClient _api;
  FamilyService(this._api);
  Future<FamilyUser> search(String email) async => FamilyUser.fromJson(
      await _api.get('/family/search', queryParams: {'email': email.trim()}));
  Future<List<FamilyRequest>> requests() async =>
      (await _api.getList('/family/requests')).map(FamilyRequest.fromJson).toList();
  Future<void> send(String email) async {
    await _api.post('/family/requests', body: {'email': email.trim()});
  }
  Future<void> respond(FamilyRequest request, bool accept, List<String> ids) async {
    await _api.post('/family/requests/${request.id}/respond', body: {
      'revision': request.revision, 'accept': accept, 'documentIds': ids,
    });
  }
  Future<void> update(FamilyRequest request, List<String> ids) async {
    await _api.put('/family/requests/${request.id}/documents', body: {
      'revision': request.revision, 'documentIds': ids,
    });
  }
  Future<void> end(FamilyRequest request) async {
    await _api.post('/family/requests/${request.id}/end', body: {'revision': request.revision});
  }
}
