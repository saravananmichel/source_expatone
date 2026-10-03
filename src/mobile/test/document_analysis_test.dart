import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/document_service.dart';
import 'package:expatone_app/features/documents/document_analysis_screen.dart';
import 'package:expatone_app/features/documents/document_qa_screen.dart';

class MockAnalysisDocumentService extends DocumentService {
  Map<String, dynamic>? mockAnalysis;
  bool shouldFail = false;
  bool analyzeDocumentCalled = false;
  DocumentAnswer? mockAnswer;
  bool askDocumentCalled = false;

  MockAnalysisDocumentService() : super(ApiClient());

  @override
  Future<Map<String, dynamic>> analyzeDocument(String documentId, {bool forceReanalyze = false}) async {
    analyzeDocumentCalled = true;
    if (shouldFail) throw Exception('Failed');
    return mockAnalysis ?? {};
  }

  @override
  Future<Map<String, dynamic>> getDocumentAnalysis(String documentId) async {
    if (shouldFail) throw Exception('Failed');
    return mockAnalysis ?? {};
  }

  @override
  Future<DocumentAnswer> askDocument(String documentId, String question) async {
    askDocumentCalled = true;
    if (shouldFail) throw Exception('Failed');
    return mockAnswer ?? const DocumentAnswer(
      documentId: '1',
      answer: 'The expiry date is January 14, 2028.',
      grounded: true,
      documentName: 'Test Document',
    );
  }
}

void main() {
  late MockAnalysisDocumentService docService;

  final fullAnalysis = {
    'documentCategory': 'Passport',
    'summary': 'A Malaysian passport issued to the holder for international travel.',
    'title': 'International Passport',
    'personName': 'John Doe',
    'issuingAuthority': 'Immigration Department of Malaysia',
    'documentNumber': 'A12345678',
    'issueDate': '2024-01-01',
    'expiryDate': '01 Jan 2034',
    'effectiveDate': '2024-01-01',
    'documentStatus': 'Valid',
    'plainLanguageExplanation': 'This is your international travel passport issued by Malaysia.',
    'confidence': 'high',
    'importantDates': [
      {'label': 'Issue Date', 'date': '01 Jan 2024', 'isExtracted': true},
      {'label': 'Expiry Date', 'date': '01 Jan 2034', 'isExtracted': true},
    ],
    'deadlines': ['Renew 6 months before expiry'],
    'requiredActions': ['Keep passport valid while residing in Malaysia'],
    'keyInformation': [
      {'label': 'Passport Number', 'value': 'A12345678', 'isExtracted': true},
      {'label': 'Document Type', 'value': 'Travel Document', 'isExtracted': false},
    ],
    'warnings': ['Do not damage or deface this document'],
    'terminology': [
      {'term': 'Immigration Endorsement', 'explanation': 'A stamp or sticker placed by immigration authorities'},
    ],
    'analyzedAt': '2024-01-15T10:00:00Z',
  };

  final analyzedDoc = DocumentItem(
    id: '1',
    name: 'My Passport',
    documentType: 'Passport',
    documentTypeId: '1',
    fileSizeBytes: 500000,
    status: 'Active',
    createdAt: DateTime(2024, 1, 1),
    isAnalyzed: true,
  );

  final unanalyzedDoc = DocumentItem(
    id: '2',
    name: 'New Visa',
    documentType: 'Visa',
    documentTypeId: '2',
    fileSizeBytes: 250000,
    status: 'Active',
    createdAt: DateTime(2024, 2, 1),
    isAnalyzed: false,
  );

  setUp(() {
    docService = MockAnalysisDocumentService();
  });

  group('DocumentAnalysisScreen', () {
    testWidgets('shows loading state for analyzed document', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Loading analysis...'), findsOneWidget);
    });

    testWidgets('shows analyzing state for unanalyzed document', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: unanalyzedDoc,
          documentService: docService,
        ),
      ));

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.text('Analyzing document...'), findsOneWidget);
    });

    testWidgets('renders all sections with full analysis', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Passport'), findsOneWidget);
      expect(find.textContaining('Malaysian passport'), findsOneWidget);
      expect(find.text('Document Details'), findsOneWidget);
      expect(find.text('Key Information'), findsOneWidget);

      await tester.scrollUntilVisible(find.text('Important Dates'), 200);
      expect(find.text('Important Dates'), findsOneWidget);

      await tester.scrollUntilVisible(find.text('Required Actions'), 200);
      expect(find.text('Required Actions'), findsOneWidget);

      await tester.scrollUntilVisible(find.text('Deadlines'), 200);
      expect(find.text('Deadlines'), findsOneWidget);

      await tester.scrollUntilVisible(find.text('Warnings'), 200);
      expect(find.text('Warnings'), findsOneWidget);

      await tester.scrollUntilVisible(find.text('Terminology'), 200);
      expect(find.text('Terminology'), findsOneWidget);
    });

    testWidgets('shows key information values', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Passport Number'), findsOneWidget);
      expect(find.text('A12345678'), findsAtLeast(1));
    });

    testWidgets('shows AI badge for inferred information', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('AI'), findsWidgets);
    });

    testWidgets('shows error state on failure', (tester) async {
      docService.shouldFail = true;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Unable to load analysis. Please try again.'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('shows error state for failed analysis', (tester) async {
      docService.shouldFail = true;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: unanalyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Unable to analyze document. Please try again.'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('handles empty sections gracefully', (tester) async {
      docService.mockAnalysis = {
        'documentCategory': 'Letter',
        'summary': 'A generic letter.',
        'importantDates': [],
        'expiryDate': null,
        'deadlines': [],
        'requiredActions': [],
        'keyInformation': [],
        'warnings': [],
        'terminology': [],
      };

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Letter'), findsOneWidget);
      expect(find.text('A generic letter.'), findsOneWidget);
      expect(find.text('Key Information'), findsNothing);
      expect(find.text('Important Dates'), findsNothing);
      expect(find.text('Warnings'), findsNothing);
    });

    testWidgets('shows disclaimer banner', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      final disclaimerFinder = find.textContaining('generated by AI');
      await tester.scrollUntilVisible(disclaimerFinder, 200);
      expect(disclaimerFinder, findsOneWidget);
    });

    testWidgets('triggers analyze for unanalyzed documents', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: unanalyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      expect(docService.analyzeDocumentCalled, isTrue);
    });

    testWidgets('shows expiry date in dates section', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('01 Jan 2034'), findsWidgets);
    });

    testWidgets('shows terminology explanations', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.text('Immigration Endorsement'), 200);
      expect(find.text('Immigration Endorsement'), findsOneWidget);
      expect(find.textContaining('stamp or sticker'), findsOneWidget);
    });
  });

  group('Structured fields display', () {
    testWidgets('shows document details section with new fields', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Document Details'), findsOneWidget);
      expect(find.text('International Passport'), findsOneWidget);
      expect(find.text('John Doe'), findsOneWidget);
      expect(find.text('Immigration Department of Malaysia'), findsOneWidget);
    });

    testWidgets('shows confidence badge', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.textContaining('Extraction confidence'), 200);
      expect(find.textContaining('Extraction confidence: High'), findsOneWidget);
    });

    testWidgets('shows plain language explanation section', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.text('What This Means'), 200);
      expect(find.text('What This Means'), findsOneWidget);
      expect(find.textContaining('international travel passport'), findsOneWidget);
    });

    testWidgets('shows ask button', (tester) async {
      docService.mockAnalysis = fullAnalysis;

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(find.text('Ask About This Document'), 200);
      expect(find.text('Ask About This Document'), findsOneWidget);
    });

    testWidgets('hides document details when no fields present', (tester) async {
      docService.mockAnalysis = {
        'documentCategory': 'Other',
        'summary': 'Minimal doc',
        'importantDates': [],
        'keyInformation': [],
        'warnings': [],
        'terminology': [],
      };

      await tester.pumpWidget(MaterialApp(
        home: DocumentAnalysisScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Document Details'), findsNothing);
    });
  });

  group('DocumentQAScreen', () {
    testWidgets('shows empty state with example questions', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: DocumentQAScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));

      expect(find.text('Ask a question about this document'), findsOneWidget);
      expect(find.text('Try asking:'), findsOneWidget);
      expect(find.text('What are the key dates in this document?'), findsOneWidget);
      expect(find.text('Who issued this document?'), findsOneWidget);
    });

    testWidgets('tapping example question fills text field', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: DocumentQAScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));

      await tester.tap(find.text('What are the key dates in this document?'));
      await tester.pumpAndSettle();

      expect(
        find.widgetWithText(TextField, 'What are the key dates in this document?'),
        findsOneWidget,
      );
    });

    testWidgets('shows answer after asking question', (tester) async {
      docService.mockAnswer = const DocumentAnswer(
        documentId: '1',
        answer: 'The expiry date is January 14, 2028.',
        grounded: true,
        documentName: 'My Passport',
      );

      await tester.pumpWidget(MaterialApp(
        home: DocumentQAScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));

      await tester.enterText(find.byType(TextField), 'When does it expire?');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(find.text('When does it expire?'), findsOneWidget);
      expect(find.text('The expiry date is January 14, 2028.'), findsOneWidget);
      expect(docService.askDocumentCalled, isTrue);
    });

    testWidgets('shows ungrounded warning', (tester) async {
      docService.mockAnswer = const DocumentAnswer(
        documentId: '1',
        answer: 'I cannot find this information in the document.',
        grounded: false,
        documentName: 'My Passport',
      );

      await tester.pumpWidget(MaterialApp(
        home: DocumentQAScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));

      await tester.enterText(find.byType(TextField), 'Something?');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(find.textContaining('could not be fully grounded'), findsOneWidget);
    });

    testWidgets('shows error on failure', (tester) async {
      docService.shouldFail = true;

      await tester.pumpWidget(MaterialApp(
        home: DocumentQAScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));

      await tester.enterText(find.byType(TextField), 'Test question');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(find.textContaining('Unable to get an answer'), findsOneWidget);
    });

    testWidgets('shows question bubble after asking', (tester) async {
      docService.mockAnswer = const DocumentAnswer(
        documentId: '1',
        answer: 'Answer',
        grounded: true,
      );

      await tester.pumpWidget(MaterialApp(
        home: DocumentQAScreen(
          document: analyzedDoc,
          documentService: docService,
        ),
      ));

      await tester.enterText(find.byType(TextField), 'My question');
      await tester.tap(find.byIcon(Icons.send));
      await tester.pumpAndSettle();

      expect(find.text('My question'), findsOneWidget);
      expect(find.text('Answer'), findsOneWidget);
    });
  });

  group('DocumentAnswer model', () {
    test('parses from JSON', () {
      final json = {
        'documentId': 'abc-123',
        'answer': 'The answer is 42.',
        'grounded': true,
        'documentName': 'Test Doc',
      };

      final answer = DocumentAnswer.fromJson(json);
      expect(answer.documentId, 'abc-123');
      expect(answer.answer, 'The answer is 42.');
      expect(answer.grounded, isTrue);
      expect(answer.documentName, 'Test Doc');
    });

    test('defaults grounded to false when missing', () {
      final json = {
        'documentId': 'abc-123',
        'answer': 'Answer',
      };

      final answer = DocumentAnswer.fromJson(json);
      expect(answer.grounded, isFalse);
      expect(answer.documentName, isNull);
    });
  });

  group('DocumentItem model', () {
    test('parses isAnalyzed from JSON', () {
      final json = {
        'id': '1',
        'name': 'Test',
        'documentType': 'Passport',
        'documentTypeId': '1',
        'fileSizeBytes': 1000,
        'status': 'Active',
        'createdAt': '2024-01-01T00:00:00Z',
        'isAnalyzed': true,
      };

      final item = DocumentItem.fromJson(json);
      expect(item.isAnalyzed, isTrue);
    });

    test('defaults isAnalyzed to false', () {
      final json = {
        'id': '1',
        'name': 'Test',
        'documentType': 'Passport',
        'documentTypeId': '1',
        'fileSizeBytes': 1000,
        'status': 'Active',
        'createdAt': '2024-01-01T00:00:00Z',
      };

      final item = DocumentItem.fromJson(json);
      expect(item.isAnalyzed, isFalse);
    });
  });
}
