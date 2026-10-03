import 'dart:math';
import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:expatone_app/core/services/document_service.dart';
import 'package:expatone_app/core/services/reminder_service.dart';
import 'package:expatone_app/core/services/assistant_service.dart';
import 'package:expatone_app/core/services/user_service.dart';
import 'package:expatone_app/core/services/emergency_service.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:expatone_app/main.dart' as app;
import 'package:expatone_app/core/constants/api_constants.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/auth_service.dart';
import 'package:expatone_app/core/services/translation_service.dart';
import 'package:expatone_app/core/error/app_exception.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  testWidgets('real Firebase SDK, app login and authenticated HTTPS API', (tester) async {
    expect(ApiConstants.baseUrl.startsWith('https://'), isTrue);
    await Firebase.initializeApp();
    final auth = FirebaseAuthService();
    final random = Random.secure();
    final suffix = List.generate(12, (_) => random.nextInt(16).toRadixString(16)).join();
    final email = 'demo-device-$suffix@example.com';
    final password = List.generate(32, (_) => random.nextInt(16).toRadixString(16)).join();
    final api = ApiClient()..setAuthService(auth);
    await auth.registerWithEmail(email, password, 'Synthetic Android Demo');
    final hasToken = (await auth.getIdToken())?.isNotEmpty ?? false;
    expect(hasToken, isTrue, reason: 'Firebase SDK produces a token');
    final me = await api.get('/users/me');
    expect(me['email'] == email, isTrue, reason: 'Backend identity matches Firebase');
    await api.put('/users/me', body: {'onboardingCompleted': true});
    await auth.signOut();
    await expectLater(api.get('/users/me'), throwsA(isA<AuthException>()));
    app.main();
    await tester.pumpAndSettle(const Duration(seconds: 1));
    for (var i = 0; i < 20 && find.byType(TextFormField).evaluate().length < 2; i++) {
      await tester.pump(const Duration(seconds: 1));
    }
    expect(find.byType(TextFormField), findsAtLeastNWidgets(2));
    await tester.enterText(find.byType(TextFormField).at(0), email);
    await tester.enterText(find.byType(TextFormField).at(1), password);
    await tester.tap(find.text('Sign In').first);
    await tester.pump();
    for (var i = 0; i < 40; i++) {
      await tester.pump(const Duration(seconds: 1));
      if (find.text('CALL 999').evaluate().isNotEmpty || find.text('Dashboard').evaluate().isNotEmpty) break;
    }
    expect(find.byType(TextFormField), findsNothing, reason: 'Login leaves the login form');
    expect((await api.get('/users/me'))['id'] == me['id'], isTrue);
    final languages = await TranslationService(api).getSupportedLanguages();
    expect(languages, isNotEmpty);
    final translated = await TranslationService(api).translate(text: 'Thank you', sourceLanguage: 'en', targetLanguage: 'ms');
    expect(translated.translatedText, isNotEmpty);
    final profile = UserService(api);
    final updated = await profile.updateMe(displayName: 'Verified Android Demo');
    expect(updated.displayName, 'Verified Android Demo');
    final docs = DocumentService(api);
    final types = await docs.getDocumentTypes();
    final pdfBytes = base64Decode('JVBERi0xLjQKMSAwIG9iago8PCAvVHlwZSAvQ2F0YWxvZyAvUGFnZXMgMiAwIFIgPj4KZW5kb2JqCjIgMCBvYmoKPDwgL1R5cGUgL1BhZ2VzIC9LaWRzIFszIDAgUl0gL0NvdW50IDEgPj4KZW5kb2JqCjMgMCBvYmoKPDwgL1R5cGUgL1BhZ2UgL1BhcmVudCAyIDAgUiAvTWVkaWFCb3ggWzAgMCA2MTIgNzkyXSAvUmVzb3VyY2VzIDw8IC9Gb250IDw8IC9GMSA0IDAgUiA+PiA+PiAvQ29udGVudHMgNSAwIFIgPj4KZW5kb2JqCjQgMCBvYmoKPDwgL1R5cGUgL0ZvbnQgL1N1YnR5cGUgL1R5cGUxIC9CYXNlRm9udCAvSGVsdmV0aWNhID4+CmVuZG9iago1IDAgb2JqCjw8IC9MZW5ndGggOTQgPj4Kc3RyZWFtCkJUIC9GMSAxMiBUZiA1MCA3NTAgVGQgKEV4cGF0T25lIHN5bnRoZXRpYyBkZW1vIGRvY3VtZW50LiBSZW5ld2FsIGR1ZSAzMSBEZWNlbWJlciAyMDI2LikgVGogRVQKZW5kc3RyZWFtCmVuZG9iagp4cmVmCjAgNgowMDAwMDAwMDAwIDY1NTM1IGYgCjAwMDAwMDAwMDkgMDAwMDAgbiAKMDAwMDAwMDA1OCAwMDAwMCBuIAowMDAwMDAwMTE1IDAwMDAwIG4gCjAwMDAwMDAyNDEgMDAwMDAgbiAKMDAwMDAwMDMxMSAwMDAwMCBuIAp0cmFpbGVyCjw8IC9TaXplIDYgL1Jvb3QgMSAwIFIgPj4Kc3RhcnR4cmVmCjQ1NQolJUVPRgo=');
    final upload = await docs.requestUploadUrl(
      documentTypeId: types.first.id, documentName: 'Synthetic Android Smoke PDF',
      fileName: 'android-smoke.pdf', contentType: 'application/pdf',
      fileSizeBytes: pdfBytes.length, expiryDate: DateTime.utc(2026, 12, 31),
    );
    expect(Uri.parse(upload.uploadUrl).scheme, 'https');
    await docs.uploadFileToS3(upload.uploadUrl, pdfBytes, 'application/pdf');
    final completed = await docs.completeUpload(upload.documentId);
    expect(completed.id, upload.documentId);
    expect((await docs.getDocuments()).any((d) => d.id == completed.id), isTrue);
    expect((await docs.getDocument(completed.id)).id, completed.id);
    final access = await docs.getAccessUrl(completed.id);
    expect(Uri.parse(access).scheme, 'https');
    final download = await http.get(Uri.parse(access));
    expect(download.statusCode, 200);
    expect(download.bodyBytes, pdfBytes);
    final analysis = await docs.analyzeDocument(completed.id);
    expect(analysis['summary'], isNotEmpty);
    expect((await docs.getDocumentAnalysis(completed.id))['documentId'], completed.id);
    final reminders = ReminderService(api);
    await reminders.generateOnly(completed.id, [7]);
    final generated = await reminders.getDocumentReminders(completed.id);
    expect(generated, isNotEmpty);
    await reminders.dismissReminder(generated.first.id);
    await reminders.deleteReminder(generated.first.id);
    await docs.deleteDocument(completed.id);
    expect((await docs.getDocuments()).any((d) => d.id == completed.id), isFalse);
    final assistant = AssistantService(api);
    final conversation = await assistant.createConversation();
    final answer = await assistant.sendMessage(conversation.id,
      'What official steps are required to renew an Employment Pass in Malaysia?');
    expect(answer.content, isNotEmpty);
    expect(answer.responseMode, 'official_grounded');
    expect(answer.sources, isNotEmpty);
    expect((await assistant.getConversation(conversation.id)).messages, isNotEmpty);
    await assistant.deleteConversation(conversation.id);
    final emergency = await EmergencyService(api).assist(
      message: 'Synthetic demo test. How can I describe a need for help to a dispatcher?',
      targetLanguage: 'ms');
    expect(emergency.response, isNotEmpty);
    await auth.signOut();
    await tester.pumpAndSettle();
    await expectLater(api.get('/users/me'), throwsA(isA<AuthException>()));
  }, timeout: const Timeout(Duration(minutes: 8)));
}
