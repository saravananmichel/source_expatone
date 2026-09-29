import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/reminder_service.dart';
import 'package:expatone_app/core/services/user_service.dart';
import 'package:expatone_app/core/error/result.dart';
import 'package:expatone_app/features/dashboard/dashboard_screen.dart';
import 'package:expatone_app/shared/services/health_service.dart';

// ──────────────────────────────────────────────────────────────────────────────
// Mock services
// ──────────────────────────────────────────────────────────────────────────────

class _FakeHealthService extends HealthService {
  final bool fail;
  _FakeHealthService({this.fail = false}) : super(ApiClient());
  @override
  Future<Result<HealthStatus>> checkHealth() async {
    if (fail) return const Failure('Network error');
    return const Success(HealthStatus(
      status: 'healthy',
      database: 'connected',
      service: 'ExpatOne.Api',
      version: '1.0.0',
    ));
  }
}

class _FakeUserService extends UserService {
  final UserProfile? profile;
  final List<ChecklistItem> checklist;

  _FakeUserService({this.profile, List<ChecklistItem>? checklist})
      : checklist = checklist ?? [],
        super(ApiClient());

  @override
  Future<UserProfile> getMe() async {
    if (profile == null) throw Exception('No profile');
    return profile!;
  }

  @override
  Future<List<ChecklistItem>> getOnboardingChecklist() async => checklist;
}

class _FakeReminderService extends ReminderService {
  _FakeReminderService() : super(ApiClient());

  @override
  Future<List<ReminderItem>> getReminders() async => [];
}

UserProfile _makeProfile({String? name, String? visaPassType}) => UserProfile(
      id: '1',
      email: 'austin@test.com',
      displayName: name ?? 'Austin Celestia',
      countryCode: 'MY',
      preferredLanguage: 'en',
      externalProvider: 'firebase',
      isActive: true,
      onboardingCompleted: true,
      createdAt: DateTime(2024),
      visaPassType: visaPassType,
    );

ChecklistItem _makeItem(String title, String desc, {bool done = false, String? action}) =>
    ChecklistItem(
      id: title,
      category: 'test',
      title: title,
      description: desc,
      isCompleted: done,
      action: action,
    );

// ──────────────────────────────────────────────────────────────────────────────
// Helper builder
// ──────────────────────────────────────────────────────────────────────────────

Widget _buildDashboard({
  _FakeHealthService? health,
  _FakeUserService? user,
  _FakeReminderService? reminders,
  String? userName,
  ValueChanged<int>? onNavigate,
}) {
  return MaterialApp(
    home: DashboardScreen(
      healthService: health ?? _FakeHealthService(),
      userService: user ?? _FakeUserService(profile: _makeProfile()),
      reminderService: reminders ?? _FakeReminderService(),
      userName: userName,
      onNavigateToTab: onNavigate ?? (i) {},
    ),
  );
}

// ──────────────────────────────────────────────────────────────────────────────
// Tests
// ──────────────────────────────────────────────────────────────────────────────

void main() {
  group('Dashboard header', () {
    testWidgets('ExpatOne branding is visible', (tester) async {
      await tester.pumpWidget(_buildDashboard());
      await tester.pumpAndSettle();
      // Branding is rendered via RichText/TextSpan — check the widget tree
      expect(find.byType(RichText), findsWidgets);
      // The tagline is a plain Text widget
      expect(find.text('Your Life Abroad, Simplified'), findsOneWidget);
    });

    testWidgets('12. SOS button is visible', (tester) async {
      await tester.pumpWidget(_buildDashboard());
      await tester.pumpAndSettle();
      expect(find.text('SOS'), findsOneWidget);
    });

    testWidgets('SOS button invokes emergency tab', (tester) async {
      int? tapped;
      await tester.pumpWidget(_buildDashboard(onNavigate: (i) => tapped = i));
      await tester.pumpAndSettle();
      await tester.tap(find.text('SOS'));
      expect(tapped, 4);
    });
  });

  group('1. Greeting section', () {
    testWidgets('shows dynamic greeting with first name from userName', (tester) async {
      await tester.pumpWidget(_buildDashboard(userName: 'Austin Celestia'));
      await tester.pumpAndSettle();
      expect(find.textContaining('Austin'), findsWidgets);
    });

    testWidgets('shows dynamic greeting with first name from profile', (tester) async {
      final user = _FakeUserService(profile: _makeProfile(name: 'Maria Santos'));
      await tester.pumpWidget(_buildDashboard(user: user));
      await tester.pumpAndSettle();
      expect(find.textContaining('Maria'), findsWidgets);
    });

    testWidgets('shows fallback greeting without name', (tester) async {
      final user = _FakeUserService(profile: _makeProfile(name: ''));
      await tester.pumpWidget(_buildDashboard(user: user));
      await tester.pumpAndSettle();
      // One of "Good morning", "Good afternoon", "Good evening" is present
      final hasGreeting = tester.any(find.textContaining('Good morning')) ||
          tester.any(find.textContaining('Good afternoon')) ||
          tester.any(find.textContaining('Good evening'));
      expect(hasGreeting, isTrue);
    });

    testWidgets('subtitle text is present', (tester) async {
      await tester.pumpWidget(_buildDashboard());
      await tester.pumpAndSettle();
      expect(
        find.textContaining("Here's what's happening"),
        findsOneWidget,
      );
    });
  });

  group('2. Backend status card', () {
    testWidgets('shows healthy state', (tester) async {
      await tester.pumpWidget(_buildDashboard(health: _FakeHealthService()));
      await tester.pumpAndSettle();
      expect(find.textContaining('Backend: Healthy'), findsOneWidget);
      expect(find.textContaining('All systems operational'), findsOneWidget);
    });

    testWidgets('shows error state when backend unreachable', (tester) async {
      await tester.pumpWidget(_buildDashboard(health: _FakeHealthService(fail: true)));
      await tester.pumpAndSettle();
      expect(find.textContaining('Backend: Unavailable'), findsOneWidget);
    });
  });

  group('3. Employment Pass card', () {
    testWidgets('Pass card is visible', (tester) async {
      await tester.pumpWidget(_buildDashboard(
        user: _FakeUserService(profile: _makeProfile(visaPassType: 'EmploymentPass')),
      ));
      await tester.pumpAndSettle();
      expect(find.textContaining('Manage your visa and work documents'), findsOneWidget);
    });

    testWidgets('Pass card View button is present', (tester) async {
      await tester.pumpWidget(_buildDashboard(
        user: _FakeUserService(profile: _makeProfile(visaPassType: 'EmploymentPass')),
      ));
      await tester.pumpAndSettle();
      expect(find.text('View'), findsOneWidget);
    });

    testWidgets('Pass card navigates to documents tab', (tester) async {
      int? tapped;
      await tester.pumpWidget(_buildDashboard(
        user: _FakeUserService(profile: _makeProfile(visaPassType: 'EmploymentPass')),
        onNavigate: (i) => tapped = i,
      ));
      await tester.pumpAndSettle();
      await tester.tap(find.text('View'));
      expect(tapped, 1);
    });
  });

  group('4. Checklist renders', () {
    testWidgets('shows checklist section when items are present', (tester) async {
      final items = [
        _makeItem('Upload passport', 'Keep a secure copy'),
        _makeItem('Set up reminders', 'Never miss expiry', done: true),
      ];
      await tester.pumpWidget(_buildDashboard(
        user: _FakeUserService(profile: _makeProfile(), checklist: items),
      ));
      await tester.pumpAndSettle();
      expect(find.text('Your Checklist'), findsOneWidget);
      expect(find.text('Upload passport'), findsOneWidget);
      expect(find.text('Set up reminders'), findsOneWidget);
    });

    testWidgets('does not show checklist section when empty', (tester) async {
      await tester.pumpWidget(_buildDashboard(
        user: _FakeUserService(profile: _makeProfile(), checklist: []),
      ));
      await tester.pumpAndSettle();
      expect(find.text('Your Checklist'), findsNothing);
    });
  });

  group('5. Checklist completion count is dynamic', () {
    testWidgets('shows correct completion fraction', (tester) async {
      final items = [
        _makeItem('A', 'desc', done: true),
        _makeItem('B', 'desc', done: true),
        _makeItem('C', 'desc', done: false),
        _makeItem('D', 'desc', done: false),
        _makeItem('E', 'desc', done: false),
      ];
      await tester.pumpWidget(_buildDashboard(
        user: _FakeUserService(profile: _makeProfile(), checklist: items),
      ));
      await tester.pumpAndSettle();
      expect(find.text('2/5 completed'), findsOneWidget);
    });

    testWidgets('shows zero completed when none done', (tester) async {
      final items = [
        _makeItem('X', 'desc', done: false),
        _makeItem('Y', 'desc', done: false),
      ];
      await tester.pumpWidget(_buildDashboard(
        user: _FakeUserService(profile: _makeProfile(), checklist: items),
      ));
      await tester.pumpAndSettle();
      expect(find.text('0/2 completed'), findsOneWidget);
    });
  });

  group('6. Progress indicator', () {
    testWidgets('LinearProgressIndicator is present when checklist loaded', (tester) async {
      final items = [
        _makeItem('A', 'desc', done: true),
        _makeItem('B', 'desc', done: false),
      ];
      await tester.pumpWidget(_buildDashboard(
        user: _FakeUserService(profile: _makeProfile(), checklist: items),
      ));
      await tester.pumpAndSettle();
      expect(find.byType(LinearProgressIndicator), findsOneWidget);
    });
  });

  group('7. Completed checklist items show completed state', () {
    testWidgets('completed item title has strikethrough decoration', (tester) async {
      final items = [_makeItem('Done task', 'desc', done: true)];
      await tester.pumpWidget(_buildDashboard(
        user: _FakeUserService(profile: _makeProfile(), checklist: items),
      ));
      await tester.pumpAndSettle();
      final textWidget = tester.widget<Text>(find.text('Done task'));
      expect(textWidget.style?.decoration, TextDecoration.lineThrough);
    });

    testWidgets('incomplete item title has no strikethrough', (tester) async {
      final items = [_makeItem('Todo task', 'desc', done: false)];
      await tester.pumpWidget(_buildDashboard(
        user: _FakeUserService(profile: _makeProfile(), checklist: items),
      ));
      await tester.pumpAndSettle();
      final textWidget = tester.widget<Text>(find.text('Todo task'));
      expect(textWidget.style?.decoration, isNot(TextDecoration.lineThrough));
    });
  });

  group('Quick actions', () {
    testWidgets('8. Add Document action is present and taps to tab 1', (tester) async {
      int? tapped;
      await tester.pumpWidget(_buildDashboard(onNavigate: (i) => tapped = i));
      await tester.pumpAndSettle();
      expect(find.textContaining('Add'), findsWidgets);
      await tester.tap(find.text('Add\nDocument'));
      expect(tapped, 1);
    });

    testWidgets('9. Ask Assistant action is present and taps to tab 2', (tester) async {
      int? tapped;
      await tester.pumpWidget(_buildDashboard(onNavigate: (i) => tapped = i));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Ask\nAssistant'));
      expect(tapped, 2);
    });

    testWidgets('10. View Reminders action is present and taps to tab 3', (tester) async {
      int? tapped;
      await tester.pumpWidget(_buildDashboard(onNavigate: (i) => tapped = i));
      await tester.pumpAndSettle();
      await tester.tap(find.text('View\nReminders'));
      expect(tapped, 3);
    });

    testWidgets('11. Emergency Help action is present and taps to tab 4', (tester) async {
      int? tapped;
      await tester.pumpWidget(_buildDashboard(onNavigate: (i) => tapped = i));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Emergency\nHelp'));
      expect(tapped, 4);
    });
  });

  group('15. No overflow at target device size', () {
    testWidgets('renders without RenderFlex overflow at 360x800dp', (tester) async {
      tester.view.physicalSize = const Size(1080, 2400);
      tester.view.devicePixelRatio = 3.0;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final items = [
        _makeItem('Upload passport', 'Keep a secure copy', done: false),
        _makeItem('Upload Employment Pass', 'Stored securely', done: true),
        _makeItem('Review employment', 'Check requirements', done: true),
        _makeItem('Explore Government Assistant', 'Ask questions', done: true),
        _makeItem('Set up reminders', 'Never miss expiry', done: false),
      ];
      await tester.pumpWidget(_buildDashboard(
        user: _FakeUserService(
          profile: _makeProfile(name: 'Austin', visaPassType: 'EmploymentPass'),
          checklist: items,
        ),
      ));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });
  });
}
