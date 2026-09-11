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
      );

  String get fileSizeFormatted {
    if (fileSizeBytes < 1024) return '$fileSizeBytes B';
    if (fileSizeBytes < 1024 * 1024) return '${(fileSizeBytes / 1024).toStringAsFixed(1)} KB';
    return '${(fileSizeBytes / (1024 * 1024)).toStringAsFixed(1)} MB';
  }
}

class UploadUrlResponse {
  final String documentId;
  final String uploadUrl;
  final String objectKey;

  const UploadUrlResponse({
    required this.documentId,
    required this.uploadUrl,
    required this.objectKey,
  });

  factory UploadUrlResponse.fromJson(Map<String, dynamic> json) => UploadUrlResponse(
        documentId: json['documentId'] as String,
        uploadUrl: json['uploadUrl'] as String,
        objectKey: json['objectKey'] as String,
      );
}

class DocumentService {
  final ApiClient _apiClient;

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
}
