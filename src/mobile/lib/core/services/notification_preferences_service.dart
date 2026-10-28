import '../networking/api_client.dart';

class NotificationPreferences {
  final bool inAppEnabled;
  final bool pushEnabled;
  final bool documentEnabled;
  final bool connectionEnabled;
  final bool corporateEnabled;
  final bool sharingEnabled;
  final bool visaEnabled;
  final bool occasionEnabled;

  const NotificationPreferences({
    this.inAppEnabled = true,
    this.pushEnabled = false,
    this.documentEnabled = true,
    this.connectionEnabled = true,
    this.corporateEnabled = true,
    this.sharingEnabled = true,
    this.visaEnabled = true,
    this.occasionEnabled = true,
  });

  factory NotificationPreferences.fromJson(Map<String, dynamic> json) =>
      NotificationPreferences(
        inAppEnabled: json['inAppEnabled'] as bool? ?? true,
        pushEnabled: json['pushEnabled'] as bool? ?? false,
        documentEnabled: json['documentEnabled'] as bool? ?? true,
        connectionEnabled: json['connectionEnabled'] as bool? ?? true,
        corporateEnabled: json['corporateEnabled'] as bool? ?? true,
        sharingEnabled: json['sharingEnabled'] as bool? ?? true,
        visaEnabled: json['visaEnabled'] as bool? ?? true,
        occasionEnabled: json['occasionEnabled'] as bool? ?? true,
      );

  Map<String, dynamic> toJson() => {
        'inAppEnabled': inAppEnabled,
        'pushEnabled': pushEnabled,
        'documentEnabled': documentEnabled,
        'connectionEnabled': connectionEnabled,
        'corporateEnabled': corporateEnabled,
        'sharingEnabled': sharingEnabled,
        'visaEnabled': visaEnabled,
        'occasionEnabled': occasionEnabled,
      };
}

class NotificationPreferencesService {
  final ApiClient _api;
  NotificationPreferencesService(this._api);

  Future<NotificationPreferences> get() async =>
      NotificationPreferences.fromJson(
          await _api.get('/notification-preferences'));

  Future<NotificationPreferences> update(NotificationPreferences preferences) async =>
      NotificationPreferences.fromJson(
          await _api.put('/notification-preferences', body: preferences.toJson()));
}
