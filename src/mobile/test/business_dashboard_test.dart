import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/corporate_service.dart';
import 'package:expatone_app/features/corporate/business_dashboard.dart';

void main() {
  final employees = <BusinessRecord>[
    {
      'id': 'a',
      'displayName': 'Alice',
      'email': 'alice@example.com',
      'role': 'Viewer',
      'status': 'Active',
    },
    {
      'id': 'b',
      'displayName': 'Bob',
      'email': 'bob@example.com',
      'role': 'HR',
      'status': 'Inactive',
    },
  ];
  final tasks = <BusinessRecord>[
    {
      'id': 't1',
      'title': 'Singapore renewal',
      'employeeUserId': 'a',
      'countryCode': 'SG',
      'status': 'Open',
      'dueAt': '2027-01-01',
    },
    {
      'id': 't2',
      'title': 'Malaysia permit',
      'employeeUserId': 'b',
      'countryCode': 'MY',
      'status': 'Closed',
    },
  ];
  Widget app({bool hr = true, String company = 'one'}) => MaterialApp(
    home: BusinessDashboard(
      companies: [
        {'id': company, 'name': company},
      ],
      selected: {'id': company, 'role': hr ? 'Admin' : 'Viewer'},
      employees: hr ? employees : [],
      tasks: hr ? tasks : [tasks.first],
      metrics: const {
        'activeEmployees': 1,
        'openCases': 1,
        'dueWithin30Days': 0,
      },
      isHr: hr,
      isOwner: hr,
      loading: false,
      onRefresh: () async {},
      onCompany: (_) {},
      onInvite: () async {},
      onNewTask: () async {},
      onEditMember: (_) async {},
      taskActions: (_) => const Icon(Icons.more_vert),
    ),
  );

  testWidgets('mobile navigation, email search and combined employee filters', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(app());
    await tester.pumpAndSettle();
    await tester.tap(
      find.descendant(
        of: find.byType(NavigationBar),
        matching: find.text('Employees'),
      ),
    );
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), '  ALICE@EXAMPLE.COM  ');
    await tester.pumpAndSettle();
    expect(find.text('Alice'), findsOneWidget);
    expect(find.text('Bob'), findsNothing);
    await tester.tap(find.widgetWithText(ChoiceChip, 'Inactive'));
    await tester.pumpAndSettle();
    expect(
      find.text('No employees match your search or filters.'),
      findsOneWidget,
    );
    expect(tester.takeException(), isNull);
  });

  testWidgets('task assignee search, status filter and details', (
    tester,
  ) async {
    await tester.pumpWidget(app());
    await tester.pumpAndSettle();
    await tester.tap(
      find.descendant(
        of: find.byType(NavigationBar),
        matching: find.text('Tasks'),
      ),
    );
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'bob@example.com');
    await tester.pumpAndSettle();
    expect(find.text('Malaysia permit'), findsOneWidget);
    expect(find.text('Singapore renewal'), findsNothing);
    await tester.tap(find.widgetWithText(ChoiceChip, 'Open'));
    await tester.pumpAndSettle();
    expect(find.text('No tasks match your search or filters.'), findsOneWidget);
    await tester.tap(find.widgetWithText(ChoiceChip, 'Closed'));
    await tester.pumpAndSettle();
    final detailsButton = find.widgetWithText(TextButton, 'View details');
    await tester.ensureVisible(detailsButton);
    await tester.pumpAndSettle();
    expect(detailsButton, findsOneWidget);
    await tester.tap(detailsButton);
    await tester.pumpAndSettle();
    expect(find.text('Employee: Bob'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('employee has no workforce management controls', (tester) async {
    await tester.pumpWidget(app(hr: false));
    await tester.pumpAndSettle();
    expect(find.text('Invite'), findsNothing);
    expect(find.text('New task'), findsNothing);
    expect(
      find.descendant(
        of: find.byType(NavigationBar),
        matching: find.text('Employees'),
      ),
      findsNothing,
    );
    expect(tester.takeException(), isNull);
  });

  testWidgets('desktop sidebar and company change reset stale search', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await tester.pumpWidget(app());
    await tester.pumpAndSettle();
    expect(find.byType(NavigationBar), findsNothing);
    await tester.tap(find.widgetWithText(ListTile, 'Employees'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'missing');
    await tester.pumpAndSettle();
    await tester.pumpWidget(app(company: 'two'));
    await tester.pumpAndSettle();
    expect(find.text('Your workforce, connected.'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  test('task search data includes pages after the first 250', () async {
    final offsets = <String?>[];
    final service = CorporateService(
      ApiClient(
        httpClient: MockClient((r) async {
          offsets.add(r.url.queryParameters['skip']);
          final offset = int.parse(r.url.queryParameters['skip']!);
          return http.Response(
            jsonEncode(
              List.generate(
                offset == 0 ? 250 : 1,
                (i) => {'id': '${offset + i}'},
              ),
            ),
            200,
          );
        }),
      ),
    );
    expect((await service.cases('company')).length, 251);
    expect(offsets, ['0', '250']);
  });

  test(
    'old API ignoring pagination fails instead of silently hiding tasks',
    () async {
      final service = CorporateService(
        ApiClient(
          httpClient: MockClient(
            (r) async => http.Response(
              jsonEncode(List.generate(250, (i) => {'id': '$i'})),
              200,
            ),
          ),
        ),
      );
      await expectLater(service.cases('company'), throwsStateError);
    },
  );
}
