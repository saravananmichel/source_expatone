import '../networking/api_client.dart';

class FamilyUser {
  final String id, email;
  final String? displayName;
  final String? profilePhotoUrl;
  FamilyUser.fromJson(Map<String, dynamic> json)
      : id = json['id'] as String, email = json['email'] as String,
        displayName = json['displayName'] as String?,
        profilePhotoUrl = json['profilePhotoUrl'] as String?;
}
class FamilyRequest {
  final String id, status, revision, relationship;
  final FamilyUser requester, recipient;
  final bool isIncoming, allowExpiryReminders, allowVisaApplicationUse;
  final DateTime? accessExpiresAt;
  final bool expiredAtLoad;
  bool get isExpired => expiredAtLoad || (status == 'Accepted' && accessExpiresAt != null && !accessExpiresAt!.isAfter(DateTime.now()));
  final List<String> documentIds;
  FamilyRequest.fromJson(Map<String, dynamic> json)
      : id = json['id'] as String, status = json['status'] as String,
        revision = json['revision'] as String,
        relationship = json['relationship'] as String? ?? 'Other',
        requester = FamilyUser.fromJson(json['requester'] as Map<String, dynamic>),
        recipient = FamilyUser.fromJson(json['recipient'] as Map<String, dynamic>),
        isIncoming = json['isIncoming'] as bool,
        accessExpiresAt = json['accessExpiresAt'] == null ? null : DateTime.parse(json['accessExpiresAt'] as String),
        expiredAtLoad = json['isExpired'] as bool? ?? false,
        allowExpiryReminders = json['allowExpiryReminders'] as bool? ?? false,
        allowVisaApplicationUse = json['allowVisaApplicationUse'] as bool? ?? false,
        documentIds = (json['documentIds'] as List).cast<String>();
}
class FamilyService {
  final ApiClient _api;
  FamilyService(this._api);
  Future<FamilyUser> search(String email) async => FamilyUser.fromJson(
      await _api.get('/family/search', queryParams: {'email': email.trim()}));
  Future<List<FamilyRequest>> requests() async =>
      (await _api.getList('/family/requests')).map(FamilyRequest.fromJson).toList();
  Future<void> send(String email, {String relationship = 'Other'}) async {
    await _api.post('/family/requests', body: {'email': email.trim(), 'relationship': relationship});
  }
  Future<void> respond(FamilyRequest request, bool accept, List<String> ids, {bool? allowExpiryReminders, bool? allowVisaApplicationUse, DateTime? accessExpiresAt, bool updateAccessExpiry = false}) async {
    await _api.post('/family/requests/${request.id}/respond', body: {
      'revision': request.revision, 'accept': accept, 'documentIds': ids,
      if (accessExpiresAt != null) 'accessExpiresAt': accessExpiresAt.toUtc().toIso8601String(),
      'allowExpiryReminders': ?allowExpiryReminders,
      'allowVisaApplicationUse': ?allowVisaApplicationUse,
    });
  }
  Future<void> update(FamilyRequest request, List<String> ids, {bool? allowExpiryReminders, DateTime? accessExpiresAt, bool updateAccessExpiry = false}) async {
    await _api.put('/family/requests/${request.id}/documents', body: {
      'revision': request.revision, 'documentIds': ids,
      if (updateAccessExpiry) ...{
        'updateAccessExpiry': true,
        'accessExpiresAt': accessExpiresAt?.toUtc().toIso8601String(),
      },
      'allowExpiryReminders': ?allowExpiryReminders,
    });
  }
  Future<List<FamilyDashboardDocument>> dashboard() async =>
      (await _api.getList('/family/dashboard')).map(FamilyDashboardDocument.fromJson).toList();
  Future<List<FamilyAlert>> alerts() async =>
      (await _api.getList('/family/alerts')).map(FamilyAlert.fromJson).toList();
  Future<void> confirmExpiry(FamilyDashboardDocument document) =>
      _api.postVoid('/family/documents/${document.id}/confirm-expiry',
        body: {'fingerprint': document.fingerprint});
  Future<void> dismissAlert(String id) => _api.postVoid('/family/alerts/$id/dismiss');
  Future<void> end(FamilyRequest request) async {
    await _api.post('/family/requests/${request.id}/end', body: {'revision': request.revision});
  }
}

class FamilyDashboardDocument {
  final String id, name, documentType, expiryStatus, fingerprint;
  final FamilyUser owner;
  final bool isOwned, expiryConfirmed, remindersAllowed;
  final DateTime? expiryDate;
  final int? daysRemaining;
  FamilyDashboardDocument.fromJson(Map<String, dynamic> json)
      : id = json['id'] as String, name = json['name'] as String,
        documentType = json['documentType'] as String,
        expiryStatus = json['expiryStatus'] as String,
        fingerprint = json['fingerprint'] as String,
        owner = FamilyUser.fromJson(json['owner'] as Map<String, dynamic>),
        isOwned = json['isOwned'] as bool, expiryConfirmed = json['expiryConfirmed'] as bool,
        remindersAllowed = json['remindersAllowed'] as bool,
        expiryDate = json['expiryDate'] == null ? null : DateTime.parse(json['expiryDate'] as String),
        daysRemaining = (json['daysRemaining'] as num?)?.toInt();

  bool matches(String filter) => switch (filter) {
    'Expired' => daysRemaining != null && daysRemaining! < 0,
    'Next 30 days' => daysRemaining != null && daysRemaining! >= 0 && daysRemaining! <= 30,
    'Next 90 days' => daysRemaining != null && daysRemaining! >= 0 && daysRemaining! <= 90,
    'Valid' => expiryStatus == 'Valid',
    'No expiry date' => expiryDate == null,
    _ => true,
  };
}
class FamilyAlert {
  final String id, kind, title, message;
  final String? documentId, requestId;
  final DateTime? expiryDate;
  FamilyAlert.fromJson(Map<String, dynamic> json)
      : id = json['id'] as String, kind = json['kind'] as String,
        title = json['title'] as String, message = json['message'] as String,
        documentId = json['documentId'] as String?, requestId = json['requestId'] as String?,
        expiryDate = json['expiryDate'] == null ? null : DateTime.parse(json['expiryDate'] as String);
}
