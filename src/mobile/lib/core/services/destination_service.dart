import '../networking/api_client.dart';

class Destination {
  final String code, name;
  final String? weatherCity, landmark, bannerImageUrl;
  const Destination({
    required this.code,
    required this.name,
    this.weatherCity,
    this.landmark,
    this.bannerImageUrl,
  });
  factory Destination.fromJson(Map<String, dynamic> j) => Destination(
    code: j['code'] as String,
    name: j['name'] as String,
    weatherCity: j['weatherCity'] as String?,
    landmark: j['landmark'] as String?,
    bannerImageUrl: j['bannerImageUrl'] as String?,
  );
}

class DestinationWeather {
  final String countryCode, city;
  final bool available;
  final double? temperatureC;
  final int? weatherCode;
  final bool? isDay;
  final DateTime? observedAt;
  const DestinationWeather({
    required this.countryCode,
    required this.city,
    required this.available,
    this.temperatureC,
    this.weatherCode,
    this.isDay,
    this.observedAt,
  });
  factory DestinationWeather.fromJson(Map<String, dynamic> j) =>
      DestinationWeather(
        countryCode: j['countryCode'] as String,
        city: j['city'] as String? ?? '',
        available: j['available'] == true,
        temperatureC: (j['temperatureC'] as num?)?.toDouble(),
        weatherCode: (j['weatherCode'] as num?)?.toInt(),
        isDay: j['isDay'] as bool?,
        observedAt: DateTime.tryParse(j['observedAt'] as String? ?? ''),
      );
}

class DestinationService {
  final ApiClient _api;
  DestinationService(this._api);
  Future<List<Destination>> getDestinations() async =>
      (await _api.getList('/destinations')).map(Destination.fromJson).toList();
  Future<DestinationWeather> getWeather(String code) async =>
      DestinationWeather.fromJson(
        await _api.get('/destinations/${Uri.encodeComponent(code)}/weather'),
      );
}
