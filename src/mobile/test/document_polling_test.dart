import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/document_service.dart';

void main() {
  test('analysis polling uses a real query parameter and preserves route', () async {
    final urls = <Uri>[];
    final service = DocumentService(ApiClient(httpClient: MockClient((request) async {
      urls.add(request.url);
      if (request.method == 'POST') {
        return http.Response(jsonEncode({'analysisId': 'run-123'}), 202);
      }
      expect(request.url.path, '/api/documents/doc-123/analysis/status');
      expect(request.url.queryParameters, {'analysisId': 'run-123'});
      expect(request.url.toString(), isNot(contains('%3F')));
      return http.Response(jsonEncode({'status': 'REQUIRES_REVIEW', 'analysis': {'summary': 'Ready'}}), 200);
    })));
    expect(await service.analyzeDocument('doc-123'), {'summary': 'Ready'});
    expect(urls.length, 2);
  });
  test('document answers retain source citations and fail closed without grounding', () {
    final answer = DocumentAnswer.fromJson({'documentId': 'doc', 'answer': 'Salary',
      'evidence': [{'id': 'e1', 'page': 2, 'sourceText': 'Salary: RM12,000'}]});
    expect(answer.grounded, isFalse);
    expect(answer.evidence.single.page, 2);
    expect(answer.evidence.single.sourceText, 'Salary: RM12,000');
  });
}
