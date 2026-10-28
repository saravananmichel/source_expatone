import '../networking/api_client.dart';

class InboxNotification {
  final String id;
  final String reminderKey;
  final String title;
  final String? body;
  final DateTime? deliveredAt;

  const InboxNotification({
    required this.id,
    required this.reminderKey,
    required this.title,
    required this.body,
    required this.deliveredAt,
  });

  factory InboxNotification.fromJson(Map<String, dynamic> json) =>
      InboxNotification(
        id: json['id'] as String,
        reminderKey: json['reminderKey'] as String,
        title: json['title'] as String,
        body: json['body'] as String?,
        deliveredAt: json['deliveredAt'] == null
            ? null
            : DateTime.parse(json['deliveredAt'] as String).toLocal(),
      );
}

class NotificationInboxService {
  final ApiClient _api;
  NotificationInboxService(this._api);

  Future<List<InboxNotification>> list({int limit = 50}) async {
    if (limit < 1 || limit > 100) {
      throw ArgumentError.value(limit, 'limit', 'Must be between 1 and 100');
    }
    final response = await _api.getList('/notification-inbox',
        queryParams: {'limit': limit.toString()});
    return response.map(InboxNotification.fromJson).toList();
  }
}
