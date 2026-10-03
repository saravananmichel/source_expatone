import 'dart:convert';
import 'dart:math';

import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/auth_service.dart';
import 'package:expatone_app/core/services/document_service.dart';
import 'package:expatone_app/features/documents/document_analysis_screen.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  testWidgets(
    'real authenticated upload, durable local analysis, reload and source UI',
    (tester) async {
      await Firebase.initializeApp();
      final auth = FirebaseAuthService();
      final random = Random.secure();
      final suffix = List.generate(
        16,
        (_) => random.nextInt(16).toRadixString(16),
      ).join();
      await auth.registerWithEmail(
        'quality-device-$suffix@example.com',
        '$suffix-Q7!a9-Test',
        'Synthetic Quality Test',
      );
      final api = ApiClient()..setAuthService(auth);
      final docs = DocumentService(api);
      final types = await docs.getDocumentTypes();
      final bytes = base64Decode(
        'JVBERi0xLjQKMSAwIG9iago8PCAvVHlwZSAvQ2F0YWxvZyAvUGFnZXMgMiAwIFIgPj4KZW5kb2JqCjIgMCBvYmoKPDwgL1R5cGUgL1BhZ2VzIC9LaWRzIFszIDAgUl0gL0NvdW50IDEgPj4KZW5kb2JqCjMgMCBvYmoKPDwgL1R5cGUgL1BhZ2UgL1BhcmVudCAyIDAgUiAvTWVkaWFCb3ggWzAgMCA2MDAgODAwXSAvUmVzb3VyY2VzIDw8IC9Gb250IDw8IC9GMSA8PCAvVHlwZSAvRm9udCAvU3VidHlwZSAvVHlwZTEgL0Jhc2VGb250IC9IZWx2ZXRpY2EgPj4gPj4gPj4gL0NvbnRlbnRzIDQgMCBSID4+CmVuZG9iago0IDAgb2JqCjw8IC9MZW5ndGggMjIzID4+CnN0cmVhbQpCVCAvRjEgMTQgVGYgNDAgNzUwIFRkIChQQVNTUE9SVCAtIFNZTlRIRVRJQyBURVNUKSBUaiAwIC0yNCBUZCAoTmFtZTogQXZlcnkgRXhhbXBsZSkgVGogMCAtMjQgVGQgKFBhc3Nwb3J0IG51bWJlcjogVEVTVC1ERVZJQ0UtMDAxKSBUaiAwIC0yNCBUZCAoRXhwaXJ5IGRhdGU6IDIwMzQtMDEtMDEpIFRqIDAgLTI0IFRkIChOYXRpb25hbGl0eTogRXhhbXBsZWxhbmQpIFRqIDAgLTI0IFRkIEVUCmVuZHN0cmVhbQplbmRvYmoKeHJlZgowIDUKMDAwMDAwMDAwMCA2NTUzNSBmIAowMDAwMDAwMDA5IDAwMDAwIG4gCjAwMDAwMDAwNTggMDAwMDAgbiAKMDAwMDAwMDExNSAwMDAwMCBuIAowMDAwMDAwMjkwIDAwMDAwIG4gCnRyYWlsZXIgPDwgL1NpemUgNSAvUm9vdCAxIDAgUiA+PgpzdGFydHhyZWYKNTY0CiUlRU9G',
      );
      final upload = await docs.requestUploadUrl(
        documentTypeId: types.first.id,
        documentName: 'Synthetic document quality device test',
        fileName: 'quality-device.pdf',
        contentType: 'application/pdf',
        fileSizeBytes: bytes.length,
      );
      await docs.uploadFileToS3(upload.uploadUrl, bytes, 'application/pdf');
      await docs.completeUpload(upload.documentId);
      // Poll the real durable job; no provider or storage test doubles.
      Map<String, dynamic>? analysis;
      for (var i = 0; i < 540; i++) {
        final job = await api.get(
          '/documents/${upload.documentId}/analysis/status',
        );
        expect(job['status'], isNot('FAILED'));
        if (job['status'] == 'COMPLETED' ||
            job['status'] == 'REQUIRES_REVIEW') {
          analysis = Map<String, dynamic>.from(job['analysis'] as Map);
          break;
        }
        await Future<void>.delayed(const Duration(seconds: 2));
      }
      expect(analysis, isNotNull);
      expect(analysis!['provider'], 'Local');
      expect(analysis['evidence'], isNotEmpty);
      expect(
        analysis['semanticDocument']['documentGraph']['nodes'],
        isNotEmpty,
      );
      expect(
        analysis['statements'].every((s) => s['supportStatus'] != null),
        isTrue,
      );
      expect((await docs.getAnalysisHistory(upload.documentId)), hasLength(1));
      final document = await docs.getDocument(upload.documentId);
      await tester.pumpWidget(
        MaterialApp(
          home: DocumentAnalysisScreen(
            document: document,
            documentService: docs,
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(
        find.text('View source').first,
        250,
        scrollable: find
            .descendant(
              of: find.byType(ListView).first,
              matching: find.byType(Scrollable),
            )
            .first,
      );
      await tester.tap(find.text('View source').first);
      await tester.pumpAndSettle();
      expect(find.text('Source evidence'), findsOneWidget);
      expect(find.text('Open page 1'), findsAtLeastNWidgets(1));
      await auth.signOut();
    },
    timeout: const Timeout(Duration(minutes: 20)),
  );
}
