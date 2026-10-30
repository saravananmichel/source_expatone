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

  group('Redesigned homepage', () {
    testWidgets('service cards replace legacy status and checklist panels', (tester) async {
      await tester.pumpWidget(_buildDashboard());
      await tester.pumpAndSettle();
      expect(find.text('Your ExpatOne services'), findsOneWidget);
      expect(find.text('Your Checklist'), findsNothing);
    });
  });

  group('Quick actions', () {
    testWidgets('eight actions are available', (tester) async {
      await tester.pumpWidget(_buildDashboard());
      await tester.pumpAndSettle();
      for (final label in [
        'Add Document', 'Add Family', 'Share Document', 'Add Occasion',
        'New Reminder', 'Visa Application', 'AI Assistant', 'Find Services',
      ]) {
        expect(find.text(label, skipOffstage: false), findsWidgets);
      }
    });

    testWidgets('Add Document opens documents tab', (tester) async {
      int? selected;
      await tester.pumpWidget(_buildDashboard(onNavigate: (index) => selected = index));
      await tester.pumpAndSettle();
      final target = find.text('Add Document', skipOffstage: false).first;
      await tester.ensureVisible(target);
      await tester.pumpAndSettle();
      await tester.tap(target);
      expect(selected, 1);
    });
  });

  group('15. No overflow at target device size', () {
    for (final width in [360, 390, 430]) {
    testWidgets('renders without RenderFlex overflow at ${width}x800dp', (tester) async {
      tester.view.physicalSize = Size(width * 3.0, 2400);
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
    }
  });
}
