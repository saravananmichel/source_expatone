import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/document_service.dart';
import 'package:expatone_app/features/documents/documents_screen.dart';
import 'package:expatone_app/features/documents/document_detail_screen.dart';

class MockDocumentService extends DocumentService {
  List<DocumentItem> mockDocuments = [];
  List<DocumentType> mockTypes = [
    const DocumentType(id: '1', name: 'Passport', category: 'Identity', hasExpiry: true),
    const DocumentType(id: '2', name: 'Visa', category: 'Immigration', hasExpiry: true),
    const DocumentType(id: '9', name: 'Other', category: 'General', hasExpiry: false),
  ];
  bool shouldFail = false;
  bool deleteDocumentCalled = false;
  bool deleteVersionCalled = false;
  bool deleteVersionShouldFail = false;
  List<DocumentVersion> mockVersions = [];
  List<DocumentVersion> versionsAfterDelete = [];

  MockDocumentService() : super(ApiClient());

  @override
  Future<List<DocumentItem>> getDocuments() async {
    if (shouldFail) throw Exception('Failed');
    return mockDocuments;
  }

  @override
  Future<List<DocumentType>> getDocumentTypes() async {
    if (shouldFail) throw Exception('Failed');
    return mockTypes;
  }

  @override
  Future<String> getAccessUrl(String documentId) async {
    if (shouldFail) throw Exception('Failed');
    return 'https://example.com/doc';
  }

  @override
  Future<void> deleteDocument(String documentId) async {
    deleteDocumentCalled = true;
    if (shouldFail) throw Exception('Failed');
  }

  @override
  Future<List<DocumentVersion>> getVersions(String documentId) async {
    if (deleteVersionCalled) return versionsAfterDelete;
    return mockVersions;
  }

  @override
  Future<void> deleteVersion(String documentId, String versionId) async {
    deleteVersionCalled = true;
    if (deleteVersionShouldFail) throw Exception('Delete failed');
  }

  @override
  Future<List<DocumentShare>> getShares(String documentId) async => [];

  @override
  Future<List<DocumentAuditLog>> getAuditLogs(String documentId) async => [];
}

void main() {
  late MockDocumentService docService;

  setUp(() {
    docService = MockDocumentService();
  });

  group('DocumentsScreen', () {
    testWidgets('shows empty state when no documents', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: DocumentsScreen(documentService: docService),
      ));
      await tester.pumpAndSettle();

      expect(find.text('No documents yet'), findsOneWidget);
      expect(find.text('Add your first document'), findsOneWidget);
    });

    testWidgets('shows document list', (tester) async {
      docService.mockDocuments = [
        DocumentItem(
          id: '1',
          name: 'My Passport',
          documentType: 'Passport',
          documentTypeId: '1',
          fileSizeBytes: 500000,
          status: 'Active',
          createdAt: DateTime(2024, 1, 1),
          expiryDate: DateTime(2030, 5, 12),
        ),
        DocumentItem(
          id: '2',
          name: 'Work Visa',
          documentType: 'Visa',
          documentTypeId: '2',
          fileSizeBytes: 250000,
          status: 'Active',
          createdAt: DateTime(2024, 2, 1),
        ),
      ];

      await tester.pumpWidget(MaterialApp(
        home: DocumentsScreen(documentService: docService),
      ));
      await tester.pumpAndSettle();

      expect(find.text('My Passport'), findsOneWidget);
      expect(find.text('Work Visa'), findsOneWidget);
      expect(find.text('Passport'), findsOneWidget);
      expect(find.text('Expires: 12 May 2030'), findsOneWidget);
    });

    testWidgets('shows error state', (tester) async {
      docService.shouldFail = true;

      await tester.pumpWidget(MaterialApp(
        home: DocumentsScreen(documentService: docService),
      ));
      await tester.pumpAndSettle();

      expect(find.text('Unable to load documents'), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
    });

    testWidgets('shows loading state', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: DocumentsScreen(documentService: docService),
      ));

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
    });

    testWidgets('has add document button', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: DocumentsScreen(documentService: docService),
      ));
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.add), findsWidgets);
    });
  });

  group('DocumentDetailScreen', () {
    final testDoc = DocumentItem(
      id: '1',
      name: 'My Passport',
      documentType: 'Passport',
      documentTypeId: '1',
      originalFileName: 'passport.pdf',
      contentType: 'application/pdf',
      fileSizeBytes: 2458123,
      status: 'Active',
      expiryDate: DateTime(2030, 5, 12),
      createdAt: DateTime(2024, 1, 15, 10, 30),
    );

    testWidgets('shows document details', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: DocumentDetailScreen(
          document: testDoc,
          documentService: docService,
        ),
      ));

      expect(find.text('My Passport'), findsOneWidget);
      expect(find.text('Passport'), findsOneWidget);
      expect(find.text('passport.pdf'), findsOneWidget);
      expect(find.text('application/pdf'), findsOneWidget);
      expect(find.text('2.3 MB'), findsOneWidget);
      expect(find.text('12 May 2030'), findsOneWidget);
      expect(find.text('Active'), findsOneWidget);
    });

    testWidgets('shows view button', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: DocumentDetailScreen(
          document: testDoc,
          documentService: docService,
        ),
      ));

      expect(find.text('View Document'), findsOneWidget);
    });

    testWidgets('shows delete confirmation dialog', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: DocumentDetailScreen(
          document: testDoc,
          documentService: docService,
        ),
      ));

      await tester.tap(find.byIcon(Icons.delete_outlined));
      await tester.pumpAndSettle();

      expect(find.text('Delete document?'), findsOneWidget);
      expect(find.text('This document will be permanently removed.'), findsOneWidget);
      expect(find.text('Cancel'), findsOneWidget);
      expect(find.text('Delete'), findsOneWidget);
    });

    testWidgets('cancel delete dismisses dialog', (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: DocumentDetailScreen(
          document: testDoc,
          documentService: docService,
        ),
      ));

      await tester.tap(find.byIcon(Icons.delete_outlined));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();

      expect(find.text('Delete document?'), findsNothing);
    });
  });

  group('Version delete', () {
    final testDoc = DocumentItem(
      id: 'doc-1',
      name: 'My Passport',
      documentType: 'Passport',
      documentTypeId: '1',
      originalFileName: 'passport.pdf',
      contentType: 'application/pdf',
      fileSizeBytes: 638,
      status: 'Active',
      createdAt: DateTime(2026, 9, 18),
    );

    final olderVersion = DocumentVersion(
      id: 'ver-1',
      documentId: 'doc-1',
      versionNumber: 1,
      originalFileName: 'v1.pdf',
      fileSizeBytes: 638,
      isCurrent: false,
      createdAt: DateTime(2026, 9, 18, 6, 39),
    );

    final currentVersion = DocumentVersion(
      id: 'ver-2',
      documentId: 'doc-1',
      versionNumber: 2,
      originalFileName: 'v2.pdf',
      fileSizeBytes: 634,
      isCurrent: true,
      createdAt: DateTime(2026, 9, 18, 6, 50),
    );

    testWidgets('delete icon visible for older version when multiple versions exist',
        (tester) async {
      docService.mockVersions = [currentVersion, olderVersion];

      await tester.pumpWidget(MaterialApp(
        home: DocumentDetailScreen(
          document: testDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      // Scroll until the version delete icon comes into view
      await tester.scrollUntilVisible(
        find.byIcon(Icons.delete_outline, skipOffstage: false).first,
        200,
      );
      await tester.pumpAndSettle();

      expect(find.byIcon(Icons.delete_outline), findsWidgets);
    });

    testWidgets('delete icon not shown when only one version exists', (tester) async {
      // Sole current version — deletion not allowed per _canDeleteVersion logic
      docService.mockVersions = [currentVersion];

      await tester.pumpWidget(MaterialApp(
        home: DocumentDetailScreen(
          document: testDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      // With a single version no delete_outline icon should exist anywhere in the tree
      expect(
        find.byIcon(Icons.delete_outline, skipOffstage: false),
        findsNothing,
      );
      expect(docService.deleteVersionCalled, isFalse);
    });

    testWidgets('confirmation dialog appears on version delete tap', (tester) async {
      docService.mockVersions = [currentVersion, olderVersion];

      await tester.pumpWidget(MaterialApp(
        home: DocumentDetailScreen(
          document: testDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      // Scroll version delete icon into view then tap it
      await tester.scrollUntilVisible(
        find.byIcon(Icons.delete_outline, skipOffstage: false).first,
        200,
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.delete_outline).first);
      await tester.pumpAndSettle();

      expect(find.text('Delete version?'), findsOneWidget);
      expect(find.text('Cancel'), findsOneWidget);
      expect(find.text('Delete'), findsOneWidget);
    });

    testWidgets('cancel on version delete dialog does not call deleteVersion',
        (tester) async {
      docService.mockVersions = [currentVersion, olderVersion];

      await tester.pumpWidget(MaterialApp(
        home: DocumentDetailScreen(
          document: testDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(
        find.byIcon(Icons.delete_outline, skipOffstage: false).first,
        200,
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.delete_outline).first);
      await tester.pumpAndSettle();

      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();

      expect(docService.deleteVersionCalled, isFalse);
      expect(find.text('Delete version?'), findsNothing);
    });

    testWidgets('confirming version delete calls deleteVersion and shows success snackbar',
        (tester) async {
      docService.mockVersions = [currentVersion, olderVersion];
      docService.versionsAfterDelete = [currentVersion];

      await tester.pumpWidget(MaterialApp(
        home: DocumentDetailScreen(
          document: testDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(
        find.byIcon(Icons.delete_outline, skipOffstage: false).first,
        200,
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.delete_outline).first);
      await tester.pumpAndSettle();

      await tester.tap(find.text('Delete'));
      await tester.pumpAndSettle();

      expect(docService.deleteVersionCalled, isTrue);
      expect(find.text('Version deleted'), findsOneWidget);
    });

    testWidgets('version delete failure shows error and preserves UI state',
        (tester) async {
      docService.mockVersions = [currentVersion, olderVersion];
      docService.deleteVersionShouldFail = true;

      await tester.pumpWidget(MaterialApp(
        home: DocumentDetailScreen(
          document: testDoc,
          documentService: docService,
        ),
      ));
      await tester.pumpAndSettle();

      await tester.scrollUntilVisible(
        find.byIcon(Icons.delete_outline, skipOffstage: false).first,
        200,
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byIcon(Icons.delete_outline).first);
      await tester.pumpAndSettle();

      await tester.tap(find.text('Delete'));
      await tester.pumpAndSettle();

      // Error snackbar shown
      expect(find.text('Unable to delete version. Please try again.'), findsOneWidget);
      // Version list still present in tree (UI not cleared on failure)
      expect(find.text('v1.pdf', skipOffstage: false), findsOneWidget);
    });
  });

  group('DocumentItem model', () {
    test('formats file size correctly', () {
      final d = DateTime(2024);
      expect(
        DocumentItem(id: '1', name: 'x', documentType: 'x', documentTypeId: '1',
          fileSizeBytes: 500, status: 'Active', createdAt: d).fileSizeFormatted,
        '500 B',
      );
      expect(
        DocumentItem(id: '1', name: 'x', documentType: 'x', documentTypeId: '1',
          fileSizeBytes: 1536, status: 'Active', createdAt: d).fileSizeFormatted,
        '1.5 KB',
      );
      expect(
        DocumentItem(id: '1', name: 'x', documentType: 'x', documentTypeId: '1',
          fileSizeBytes: 2458123, status: 'Active', createdAt: d).fileSizeFormatted,
        '2.3 MB',
      );
    });
  });
}
