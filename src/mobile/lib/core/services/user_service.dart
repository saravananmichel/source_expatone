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
  final String? nationality;
  final String? residenceLocation;
  final String? visaPassType;
  final String? employmentStatus;
  final String? familyStatus;
  final bool? hasChildren;
  final int? numberOfChildren;
  final bool onboardingCompleted;

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
    this.nationality,
    this.residenceLocation,
    this.visaPassType,
    this.employmentStatus,
    this.familyStatus,
    this.hasChildren,
    this.numberOfChildren,
    required this.onboardingCompleted,
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
      nationality: json['nationality'] as String?,
      residenceLocation: json['residenceLocation'] as String?,
      visaPassType: json['visaPassType'] as String?,
      employmentStatus: json['employmentStatus'] as String?,
      familyStatus: json['familyStatus'] as String?,
      hasChildren: json['hasChildren'] as bool?,
      numberOfChildren: json['numberOfChildren'] as int?,
      onboardingCompleted: json['onboardingCompleted'] as bool? ?? false,
    );
  }
}

class ProfileOptions {
  final List<ProfileOptionItem> visaPassTypes;
  final List<ProfileOptionItem> employmentStatuses;
  final List<ProfileOptionItem> familyStatuses;
  final List<ProfileOptionItem> supportedLanguages;

  const ProfileOptions({
    required this.visaPassTypes,
    required this.employmentStatuses,
    required this.familyStatuses,
    required this.supportedLanguages,
  });

  factory ProfileOptions.fromJson(Map<String, dynamic> json) {
    return ProfileOptions(
      visaPassTypes: _parseOptions(json['visaPassTypes']),
      employmentStatuses: _parseOptions(json['employmentStatuses']),
      familyStatuses: _parseOptions(json['familyStatuses']),
      supportedLanguages: _parseOptions(json['supportedLanguages']),
    );
  }

  static List<ProfileOptionItem> _parseOptions(dynamic list) {
    if (list is! List) return [];
    return list
        .map((e) => ProfileOptionItem.fromJson(e as Map<String, dynamic>))
        .toList();
  }
}

class ProfileOptionItem {
  final String value;
  final String label;

  const ProfileOptionItem({required this.value, required this.label});

  factory ProfileOptionItem.fromJson(Map<String, dynamic> json) {
    return ProfileOptionItem(
      value: json['value'] as String,
      label: json['label'] as String,
    );
  }
}

class ChecklistItem {
  final String id;
  final String category;
  final String title;
  final String description;
  final bool isCompleted;
  final String? action;

  const ChecklistItem({
    required this.id,
    required this.category,
    required this.title,
    required this.description,
    required this.isCompleted,
    this.action,
  });

  factory ChecklistItem.fromJson(Map<String, dynamic> json) {
    return ChecklistItem(
      id: json['id'] as String,
      category: json['category'] as String,
      title: json['title'] as String,
      description: json['description'] as String,
      isCompleted: json['isCompleted'] as bool? ?? false,
      action: json['action'] as String?,
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
    String? nationality,
    String? residenceLocation,
    String? visaPassType,
    String? employmentStatus,
    String? familyStatus,
    bool? hasChildren,
    int? numberOfChildren,
    bool? onboardingCompleted,
  }) async {
    final body = <String, dynamic>{};
    if (displayName != null) body['displayName'] = displayName;
    if (phoneNumber != null) body['phoneNumber'] = phoneNumber;
    if (countryCode != null) body['countryCode'] = countryCode;
    if (preferredLanguage != null) body['preferredLanguage'] = preferredLanguage;
    if (nationality != null) body['nationality'] = nationality;
    if (residenceLocation != null) body['residenceLocation'] = residenceLocation;
    if (visaPassType != null) body['visaPassType'] = visaPassType;
    if (employmentStatus != null) body['employmentStatus'] = employmentStatus;
    if (familyStatus != null) body['familyStatus'] = familyStatus;
    if (hasChildren != null) body['hasChildren'] = hasChildren;
    if (numberOfChildren != null) body['numberOfChildren'] = numberOfChildren;
    if (onboardingCompleted != null) body['onboardingCompleted'] = onboardingCompleted;

    final response = await _apiClient.put('/users/me', body: body);
    return UserProfile.fromJson(response);
  }

  Future<ProfileOptions> getProfileOptions() async {
    final response = await _apiClient.get('/profile/options');
    return ProfileOptions.fromJson(response);
  }

  Future<List<ChecklistItem>> getOnboardingChecklist() async {
    final list = await _apiClient.getList('/profile/checklist');
    return list.map((e) => ChecklistItem.fromJson(e)).toList();
  }
}
