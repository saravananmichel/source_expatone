import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/shared/services/health_service.dart';
import 'package:expatone_app/main.dart';
import 'package:expatone_app/features/dashboard/dashboard_screen.dart';

void main() {
  late ApiClient apiClient;
  late HealthService healthService;

  setUp(() {
    apiClient = ApiClient();
    healthService = HealthService(apiClient);
  });

  testWidgets('App renders with bottom navigation', (WidgetTester tester) async {
    await tester.pumpWidget(ExpatOneApp(healthService: healthService));
    expect(find.text('Home'), findsOneWidget);
    expect(find.text('Assistant'), findsOneWidget);
    expect(find.text('Profile'), findsOneWidget);
  });

  testWidgets('Dashboard shows greeting', (WidgetTester tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: DashboardScreen(healthService: healthService),
      ),
    );

    expect(find.text('Good morning'), findsOneWidget);
    expect(find.text('Your ExpatOne Assistant'), findsOneWidget);
  });

  testWidgets('Dashboard shows loading state initially', (WidgetTester tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: DashboardScreen(healthService: healthService),
      ),
    );

    expect(find.text('Connecting to ExpatOne...'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });

  testWidgets('Dashboard shows error after failed health check', (WidgetTester tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: DashboardScreen(healthService: healthService),
      ),
    );

    await tester.pumpAndSettle();

    expect(find.byIcon(Icons.cloud_off), findsOneWidget);
    expect(find.byIcon(Icons.refresh), findsOneWidget);
  });

  testWidgets('Bottom navigation switches tabs', (WidgetTester tester) async {
    await tester.pumpWidget(ExpatOneApp(healthService: healthService));

    // Tap Profile tab
    await tester.tap(find.text('Profile'));
    await tester.pump();

    expect(find.text('Coming soon'), findsOneWidget);

    // Tap Home tab
    await tester.tap(find.text('Home'));
    await tester.pump();

    expect(find.text('Good morning'), findsOneWidget);
  });
}
