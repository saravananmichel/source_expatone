import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/document_service.dart';
import 'package:expatone_app/features/documents/document_analysis_screen.dart';

class EvidenceService extends DocumentService {
  EvidenceService() : super(ApiClient());
  @override
  Future<Map<String, dynamic>> getDocumentAnalysis(String documentId) async => {
    'documentCategory': 'Employment Contract',
    'summary': 'The agreement sets out compensation and notice.',
    'requiresReview': true,
    'statements': [
      {
        'id': 's1',
        'kind': 'fact',
        'label': 'Salary',
        'text': 'The salary is RM12,000 per month.',
        'evidenceIds': ['e1'],
      },
    ],
    'evidence': [
      {
        'id': 'e1',
        'page': 2,
        'section': 'Compensation',
        'sourceText': 'Salary: RM12,000 per month.',
      },
    ],
  };
  @override
  Future<List<Map<String, dynamic>>> getAnalysisHistory(
    String documentId,
  ) async => [
    {
      'status': 'REQUIRES_REVIEW',
      'createdAt': '2026-10-02',
      'analysis': await getDocumentAnalysis(documentId),
    },
  ];
}

class UncertainEvidenceService extends EvidenceService {
  @override
  Future<Map<String, dynamic>> getDocumentAnalysis(String documentId) async {
    final result = await super.getDocumentAnalysis(documentId);
    result['statements'] = [
      {
        'id': 's1',
        'kind': 'action',
        'label': 'Unsupported advice',
        'text': 'Invented renewal requirement',
        'originalValue': 'Salary: RM12,000 per month.',
        'supportStatus': 'UNSUPPORTED',
        'evidenceIds': ['e1'],
      },
    ];
    result['qualityDiagnostics'] = {'unsupportedClaims': 1, 'pageCount': 2};
    return result;
  }
}

void main() {
  final document = DocumentItem(
    id: 'synthetic',
    name: 'Contract',
    documentType: 'Employment Contract',
    documentTypeId: 'type',
    fileSizeBytes: 100,
    status: 'Active',
    createdAt: DateTime(2026),
    isAnalyzed: true,
  );
  testWidgets('shows fact labels and original page evidence', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: DocumentAnalysisScreen(
          document: document,
          documentService: EvidenceService(),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('fact · Salary'), findsOneWidget);
    await tester.tap(find.text('View source'));
    await tester.pumpAndSettle();
    expect(find.text('Page 2 · Compensation'), findsOneWidget);
    expect(find.text('Salary: RM12,000 per month.'), findsOneWidget);
    expect(find.text('Open page 2'), findsOneWidget);
  });
  testWidgets('lists preserved analysis history', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: DocumentAnalysisScreen(
          document: document,
          documentService: EvidenceService(),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Analysis history'));
    await tester.pumpAndSettle();
    expect(find.text('Analysis v1 · REQUIRES_REVIEW'), findsOneWidget);
  });
  testWidgets(
    'unverified prose is hidden while original evidence stays accessible',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: DocumentAnalysisScreen(
            document: document,
            documentService: UncertainEvidenceService(),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Details to verify'), findsOneWidget);
      expect(find.text('Interpretation needs review'), findsOneWidget);
      expect(find.text('Invented renewal requirement'), findsNothing);
      expect(
        find.text('Extracted value: Salary: RM12,000 per month.'),
        findsOneWidget,
      );
      await tester.tap(find.text('View source'));
      await tester.pumpAndSettle();
      expect(find.text('Source evidence'), findsOneWidget);
    },
  );
}
