import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:expatone_app/core/networking/api_client.dart';
import 'package:expatone_app/core/services/destination_service.dart';
import 'package:expatone_app/features/dashboard/widgets/destination_banner.dart';

class FakeDestinations extends DestinationService {
  final Map<String, Completer<DestinationWeather>> pending = {};
  bool empty = false, fail = false;
  FakeDestinations() : super(ApiClient());
  @override
  Future<List<Destination>> getDestinations() async {
    if (fail) throw Exception('offline');
    return empty
        ? []
        : const [
            Destination(
              code: 'MY',
              name: 'Malaysia',
              weatherCity: 'Kuala Lumpur',
            ),
            Destination(
              code: 'SG',
              name: 'Singapore',
              weatherCity: 'Singapore',
            ),
          ];
  }

  @override
  Future<DestinationWeather> getWeather(String code) =>
      (pending[code] = Completer<DestinationWeather>()).future;
}

DestinationWeather weather(String code, double t) => DestinationWeather(
  countryCode: code,
  city: code,
  available: true,
  temperatureC: t,
  weatherCode: 0,
  isDay: true,
);
Widget host(FakeDestinations service) => MaterialApp(
  home: Scaffold(
    body: SingleChildScrollView(
      child: DestinationBanner(
        service: service,
        greeting: 'Good morning, Saravanan',
        userId: 'test',
        preferredCountry: 'MY',
      ),
    ),
  ),
);
void main() {
  setUp(() => SharedPreferences.setMockInitialValues({}));
  testWidgets('late weather response cannot overwrite a changed destination', (
    tester,
  ) async {
    final service = FakeDestinations();
    await tester.pumpWidget(host(service));
    await tester.pump();
    await tester.pump();
    await tester.tap(find.byKey(const Key('destination-selector')));
    await tester.pump(const Duration(seconds: 1));
    await tester.tap(find.text('Singapore').last);
    await tester.pump(const Duration(seconds: 1));
    service.pending['SG']!.complete(weather('SG', 28));
    await tester.pump();
    service.pending['MY']!.complete(weather('MY', 35));
    await tester.pump();
    expect(find.text('28°C'), findsOneWidget);
    expect(find.text('35°C'), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });
  testWidgets('empty active-country result has an honest empty state', (
    tester,
  ) async {
    final service = FakeDestinations()..empty = true;
    await tester.pumpWidget(host(service));
    await tester.pumpAndSettle();
    expect(find.text('No active destinations available'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });
  testWidgets('country load failure offers retry', (tester) async {
    await tester.pumpWidget(host(FakeDestinations()..fail = true));
    await tester.pumpAndSettle();
    expect(find.text('Unable to load destinations'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });
  testWidgets('saved selection is scoped to the user and restored', (
    tester,
  ) async {
    SharedPreferences.setMockInitialValues({'expatone.destination.test': 'SG'});
    final service = FakeDestinations();
    await tester.pumpWidget(host(service));
    await tester.pump();
    await tester.pump();
    expect(service.pending.containsKey('SG'), isTrue);
    service.pending['SG']!.complete(weather('SG', 27));
    await tester.pump();
    expect(find.text('27°C'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
  });
}
