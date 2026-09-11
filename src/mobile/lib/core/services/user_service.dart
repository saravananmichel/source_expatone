import '../networking/api_client.dart';

class UserProfile {
  final String id;
  final String email;
  final String? displayName;
  final String? phoneNumber;
  final String countryCode;
  final String preferredLanguage;
  final String externalProvider;
  final bool isActive;
  final DateTime createdAt;

  const UserProfile({
    required this.id,
    required this.email,
    this.displayName,
    this.phoneNumber,
    required this.countryCode,
    required this.preferredLanguage,
    required this.externalProvider,
    required this.isActive,
    required this.createdAt,
  });

  factory UserProfile.fromJson(Map<String, dynamic> json) {
    return UserProfile(
      id: json['id'] as String,
      email: json['email'] as String,
      displayName: json['displayName'] as String?,
      phoneNumber: json['phoneNumber'] as String?,
      countryCode: json['countryCode'] as String? ?? 'MY',
      preferredLanguage: json['preferredLanguage'] as String? ?? 'en',
      externalProvider: json['externalProvider'] as String? ?? 'firebase',
      isActive: json['isActive'] as bool? ?? true,
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }
}

class UserService {
  final ApiClient _apiClient;

  UserService(this._apiClient);

  Future<UserProfile> getMe() async {
    final response = await _apiClient.get('/users/me');
    return UserProfile.fromJson(response);
  }

  Future<UserProfile> updateMe({
    String? displayName,
    String? phoneNumber,
    String? countryCode,
    String? preferredLanguage,
  }) async {
    final body = <String, dynamic>{};
    if (displayName != null) body['displayName'] = displayName;
    if (phoneNumber != null) body['phoneNumber'] = phoneNumber;
    if (countryCode != null) body['countryCode'] = countryCode;
    if (preferredLanguage != null) body['preferredLanguage'] = preferredLanguage;

    final response = await _apiClient.put('/users/me', body: body);
    return UserProfile.fromJson(response);
  }
}
