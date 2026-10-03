import 'dart:async';
import '../constants/api_constants.dart';
import '../networking/api_client.dart';
import '../error/app_exception.dart';
import 'package:http/http.dart' as http;

class DocumentType {
  final String id;
  final String name;
  final String? description;
  final String? category;
  final bool hasExpiry;

  const DocumentType({
    required this.id,
    required this.name,
    this.description,
    this.category,
    required this.hasExpiry,
  });

  factory DocumentType.fromJson(Map<String, dynamic> json) => DocumentType(
        id: json['id'] as String,
        name: json['name'] as String,
        description: json['description'] as String?,
        category: json['category'] as String?,
        hasExpiry: json['hasExpiry'] as bool? ?? false,
      );
}

class DocumentItem {
  final String id;
  final String name;
  final String documentType;
  final String documentTypeId;
  final String? originalFileName;
  final String? contentType;
  final int fileSizeBytes;
  final String status;
  final DateTime? expiryDate;
  final DateTime createdAt;
  final bool isAnalyzed;
  final int versionCount;
  final int activeShareCount;

  const DocumentItem({
    required this.id,
    required this.name,
    required this.documentType,
    required this.documentTypeId,
    this.originalFileName,
    this.contentType,
    required this.fileSizeBytes,
    required this.status,
    this.expiryDate,
    required this.createdAt,
    this.isAnalyzed = false,
    this.versionCount = 0,
    this.activeShareCount = 0,
  });

  factory DocumentItem.fromJson(Map<String, dynamic> json) => DocumentItem(
        id: json['id'] as String,
        name: json['name'] as String,
        documentType: json['documentType'] as String,
        documentTypeId: json['documentTypeId'] as String,
        originalFileName: json['originalFileName'] as String?,
        contentType: json['contentType'] as String?,
        fileSizeBytes: (json['fileSizeBytes'] as num?)?.toInt() ?? 0,
        status: json['status'] as String? ?? 'Active',
        expiryDate: json['expiryDate'] != null
            ? DateTime.parse(json['expiryDate'] as String)
            : null,
        createdAt: DateTime.parse(json['createdAt'] as String),
        isAnalyzed: json['isAnalyzed'] as bool? ?? false,
        versionCount: (json['versionCount'] as num?)?.toInt() ?? 0,
        activeShareCount: (json['activeShareCount'] as num?)?.toInt() ?? 0,
      );

  String get fileSizeFormatted {
    if (fileSizeBytes < 1024) return '$fileSizeBytes B';
    if (fileSizeBytes < 1024 * 1024) return '${(fileSizeBytes / 1024).toStringAsFixed(1)} KB';
    return '${(fileSizeBytes / (1024 * 1024)).toStringAsFixed(1)} MB';
  }

  bool get isExpired => expiryDate != null && expiryDate!.isBefore(DateTime.now());
  bool get isExpiringSoon => expiryDate != null &&
      !isExpired &&
      expiryDate!.isBefore(DateTime.now().add(const Duration(days: 30)));
}

class DocumentVersion {
  final String id;
  final String documentId;
  final int versionNumber;
  final String? originalFileName;
  final String? contentType;
  final int fileSizeBytes;
  final bool isCurrent;
  final DateTime createdAt;

  const DocumentVersion({
    required this.id,
    required this.documentId,
    required this.versionNumber,
    this.originalFileName,
    this.contentType,
    required this.fileSizeBytes,
    required this.isCurrent,
    required this.createdAt,
  });

  factory DocumentVersion.fromJson(Map<String, dynamic> json) => DocumentVersion(
        id: json['id'] as String,
        documentId: json['documentId'] as String,
        versionNumber: (json['versionNumber'] as num).toInt(),
        originalFileName: json['originalFileName'] as String?,
        contentType: json['contentType'] as String?,
        fileSizeBytes: (json['fileSizeBytes'] as num?)?.toInt() ?? 0,
        isCurrent: json['isCurrent'] as bool? ?? false,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );

  String get fileSizeFormatted {
    if (fileSizeBytes < 1024) return '$fileSizeBytes B';
    if (fileSizeBytes < 1024 * 1024) return '${(fileSizeBytes / 1024).toStringAsFixed(1)} KB';
    return '${(fileSizeBytes / (1024 * 1024)).toStringAsFixed(1)} MB';
  }
}

class DocumentShare {
  final String id;
  final String documentId;
  final String? documentName;
  final List<DocumentAnswerEvidence> evidence;
  final String sharedWithUserId;
  final String? sharedWithEmail;
  final String permission;
  final DateTime createdAt;
  final DateTime? revokedAt;

  const DocumentShare({
    required this.id,
    required this.documentId,
    this.documentName,
    this.evidence = const [],
    required this.sharedWithUserId,
    this.sharedWithEmail,
    required this.permission,
    required this.createdAt,
    this.revokedAt,
  });

  factory DocumentShare.fromJson(Map<String, dynamic> json) => DocumentShare(
        id: json['id'] as String,
        documentId: json['documentId'] as String,
        documentName: json['documentName'] as String?,
        evidence: (json['evidence'] as List? ?? []).map((e) => DocumentAnswerEvidence.fromJson(Map<String, dynamic>.from(e as Map))).toList(),
        sharedWithUserId: json['sharedWithUserId'] as String,
        sharedWithEmail: json['sharedWithEmail'] as String?,
        permission: json['permission'] as String? ?? 'Read',
        createdAt: DateTime.parse(json['createdAt'] as String),
        revokedAt: json['revokedAt'] != null
            ? DateTime.parse(json['revokedAt'] as String)
            : null,
      );

  bool get isActive => revokedAt == null;
}

class SharedDocument {
  final String shareId;
  final String documentId;
  final String documentName;
  final String documentType;
  final String? ownerEmail;
  final String permission;
  final DateTime? expiryDate;
  final DateTime sharedAt;

  const SharedDocument({
    required this.shareId,
    required this.documentId,
    required this.documentName,
    required this.documentType,
    this.ownerEmail,
    required this.permission,
    this.expiryDate,
    required this.sharedAt,
  });

  factory SharedDocument.fromJson(Map<String, dynamic> json) => SharedDocument(
        shareId: json['shareId'] as String,
        documentId: json['documentId'] as String,
        documentName: json['documentName'] as String,
        documentType: json['documentType'] as String,
        ownerEmail: json['ownerEmail'] as String?,
        permission: json['permission'] as String? ?? 'Read',
        expiryDate: json['expiryDate'] != null
            ? DateTime.parse(json['expiryDate'] as String)
            : null,
        sharedAt: DateTime.parse(json['sharedAt'] as String),
      );
}

class DocumentAuditLog {
  final String id;
  final String documentId;
  final String action;
  final String? targetVersionId;
  final String? targetShareId;
  final String? metadata;
  final DateTime createdAt;

  const DocumentAuditLog({
    required this.id,
    required this.documentId,
    required this.action,
    this.targetVersionId,
    this.targetShareId,
    this.metadata,
    required this.createdAt,
  });

  factory DocumentAuditLog.fromJson(Map<String, dynamic> json) => DocumentAuditLog(
        id: json['id'] as String,
        documentId: json['documentId'] as String,
        action: json['action'] as String,
        targetVersionId: json['targetVersionId'] as String?,
        targetShareId: json['targetShareId'] as String?,
        metadata: json['metadata'] as String?,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );

  String get actionLabel {
    switch (action) {
      case 'document_uploaded':
        return 'Document uploaded';
      case 'document_accessed':
        return 'Document viewed';
      case 'document_deleted':
        return 'Document deleted';
      case 'version_uploaded':
        return 'New version uploaded';
      case 'version_accessed':
        return 'Version viewed';
      case 'version_deleted':
        return 'Version deleted';
      case 'share_created':
        return 'Document shared';
      case 'share_revoked':
        return 'Share revoked';
      case 'shared_document_accessed':
        return 'Shared document viewed';
      default:
        return action;
    }
  }
}

class VersionUploadResponse {
  final String versionId;
  final String uploadUrl;

  const VersionUploadResponse({
    required this.versionId,
    required this.uploadUrl,
  });

  factory VersionUploadResponse.fromJson(Map<String, dynamic> json) => VersionUploadResponse(
        versionId: json['versionId'] as String,
        uploadUrl: json['uploadUrl'] as String,
      );
}

class UploadUrlResponse {
  final String documentId;
  final String uploadUrl;

  const UploadUrlResponse({
    required this.documentId,
    required this.uploadUrl,
  });

  factory UploadUrlResponse.fromJson(Map<String, dynamic> json) => UploadUrlResponse(
        documentId: json['documentId'] as String,
        uploadUrl: json['uploadUrl'] as String,
      );
}

class DocumentService {
  final ApiClient _apiClient;
  final _analysisProgress = StreamController<Map<String, String>>.broadcast();
  Stream<Map<String, String>> get analysisProgress => _analysisProgress.stream;

  DocumentService(this._apiClient);

  Future<List<DocumentType>> getDocumentTypes() async {
    final response = await _apiClient.getList('/document-types');
    return response.map((e) => DocumentType.fromJson(e)).toList();
  }

  Future<List<DocumentItem>> getDocuments() async {
    final response = await _apiClient.getList('/documents');
    return response.map((e) => DocumentItem.fromJson(e)).toList();
  }

  Future<DocumentItem> getDocument(String id) async {
    final response = await _apiClient.get('/documents/$id');
    return DocumentItem.fromJson(response);
  }

  Future<UploadUrlResponse> requestUploadUrl({
    required String documentTypeId,
    required String documentName,
    required String fileName,
    required String contentType,
    required int fileSizeBytes,
    DateTime? expiryDate,
  }) async {
    final body = <String, dynamic>{
      'documentTypeId': documentTypeId,
      'documentName': documentName,
      'fileName': fileName,
      'contentType': contentType,
      'fileSizeBytes': fileSizeBytes,
    };
    if (expiryDate != null) {
      body['expiryDate'] = expiryDate.toIso8601String();
    }
    final response = await _apiClient.post('/documents/upload-url', body: body);
    return UploadUrlResponse.fromJson(response);
  }

  Future<void> uploadFileToS3(String uploadUrl, List<int> fileBytes, String contentType) async {
    final response = await http.put(
      Uri.parse(uploadUrl),
      headers: {'Content-Type': contentType},
      body: fileBytes,
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw const NetworkException('Failed to upload file');
    }
  }

  Future<DocumentItem> completeUpload(String documentId) async {
    final response = await _apiClient.post('/documents/$documentId/complete');
    return DocumentItem.fromJson(response);
  }

  Future<String> getAccessUrl(String documentId) async {
    final response = await _apiClient.get('/documents/$documentId/access-url');
    return response['url'] as String;
  }

  Future<void> deleteDocument(String documentId) async {
    await _apiClient.delete('/documents/$documentId');
  }

  Future<Map<String, dynamic>> analyzeDocument(String documentId, {bool forceReanalyze = false}) async {
    final response = await _apiClient.post(
      '/documents/$documentId/analyze',
      body: {'forceReanalyze': forceReanalyze},
      timeout: ApiConstants.aiTimeout,
    );
    if (response['analysisId'] == null) return response;
    final analysisId = response['analysisId'] as String;
    for (var attempt = 0; attempt < 180; attempt++) {
      final job = await _apiClient.get('/documents/$documentId/analysis/status', queryParams: {'analysisId': analysisId});
      final status = job['status'];
      _analysisProgress.add({'documentId': documentId, 'status': status as String? ?? 'PROCESSING',
        'stage': job['stage'] as String? ?? 'Understanding document'});
      if (status == 'COMPLETED' || status == 'REQUIRES_REVIEW') {
        return Map<String, dynamic>.from(job['analysis'] as Map);
      }
      if (status == 'FAILED') {
        throw const NetworkException("We couldn't fully analyze this document. You can retry.");
      }
      await Future<void>.delayed(const Duration(seconds: 2));
    }
    throw const NetworkException('Analysis is still processing. Reopen this screen to check progress.');
  }

  Future<List<Map<String, dynamic>>> getAnalysisHistory(String documentId) =>
      _apiClient.getList('/documents/$documentId/analysis/history');

  Future<Map<String, dynamic>> getDocumentAnalysis(String documentId) async {
    final response = await _apiClient.get('/documents/$documentId/analysis');
    return response;
  }

  Future<DocumentAnswer> askDocument(String documentId, String question) async {
    final response = await _apiClient.post(
      '/documents/$documentId/ask',
      body: {'question': question},
      timeout: ApiConstants.aiTimeout,
    );
    return DocumentAnswer.fromJson(response);
  }

  // --- Versioning ---

  Future<VersionUploadResponse> requestVersionUploadUrl({
    required String documentId,
    required String fileName,
    required String contentType,
    required int fileSizeBytes,
  }) async {
    final response = await _apiClient.post(
      '/documents/$documentId/versions/upload-url',
      body: {
        'fileName': fileName,
        'contentType': contentType,
        'fileSizeBytes': fileSizeBytes,
      },
    );
    return VersionUploadResponse.fromJson(response);
  }

  Future<DocumentVersion> completeVersionUpload(String documentId, String versionId) async {
    final response = await _apiClient.post('/documents/$documentId/versions/$versionId/complete');
    return DocumentVersion.fromJson(response);
  }

  Future<List<DocumentVersion>> getVersions(String documentId) async {
    final response = await _apiClient.getList('/documents/$documentId/versions');
    return response.map((e) => DocumentVersion.fromJson(e)).toList();
  }

  Future<String> getVersionAccessUrl(String documentId, String versionId) async {
    final response = await _apiClient.get('/documents/$documentId/versions/$versionId/access-url');
    return response['url'] as String;
  }

  Future<void> deleteVersion(String documentId, String versionId) async {
    await _apiClient.delete('/documents/$documentId/versions/$versionId');
  }

  // --- Sharing ---

  Future<DocumentShare> shareDocument(String documentId, String email) async {
    final response = await _apiClient.post(
      '/documents/$documentId/shares',
      body: {'sharedWithEmail': email},
    );
    return DocumentShare.fromJson(response);
  }

  Future<List<DocumentShare>> getShares(String documentId) async {
    final response = await _apiClient.getList('/documents/$documentId/shares');
    return response.map((e) => DocumentShare.fromJson(e)).toList();
  }

  Future<void> revokeShare(String documentId, String shareId) async {
    await _apiClient.delete('/documents/$documentId/shares/$shareId');
  }

  Future<List<SharedDocument>> getSharedWithMe() async {
    final response = await _apiClient.getList('/documents/shared-with-me');
    return response.map((e) => SharedDocument.fromJson(e)).toList();
  }

  Future<String> getSharedDocumentAccessUrl(String documentId) async {
    final response = await _apiClient.get('/documents/$documentId/shared-access-url');
    return response['url'] as String;
  }

  // --- Audit logs ---

  Future<List<DocumentAuditLog>> getAuditLogs(String documentId) async {
    final response = await _apiClient.getList('/documents/$documentId/audit-logs');
    return response.map((e) => DocumentAuditLog.fromJson(e)).toList();
  }
}

class DocumentAnswerEvidence {
  final String id;
  final int page;
  final String sourceText;
  const DocumentAnswerEvidence({required this.id, required this.page, required this.sourceText});
  factory DocumentAnswerEvidence.fromJson(Map<String, dynamic> json) => DocumentAnswerEvidence(
    id: json['id'] as String, page: json['page'] as int, sourceText: json['sourceText'] as String);
}

class DocumentAnswer {
  final String documentId;
  final String answer;
  final bool grounded;
  final String? documentName;
  final List<DocumentAnswerEvidence> evidence;

  const DocumentAnswer({
    required this.documentId,
    required this.answer,
    required this.grounded,
    this.documentName,
    this.evidence = const [],
  });

  factory DocumentAnswer.fromJson(Map<String, dynamic> json) => DocumentAnswer(
        documentId: json['documentId'] as String,
        answer: json['answer'] as String,
        grounded: json['grounded'] as bool? ?? false,
        documentName: json['documentName'] as String?,
        evidence: (json['evidence'] as List? ?? []).map((e) => DocumentAnswerEvidence.fromJson(Map<String, dynamic>.from(e as Map))).toList(),
      );
}
