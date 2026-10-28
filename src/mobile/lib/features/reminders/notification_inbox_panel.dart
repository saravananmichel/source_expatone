import 'package:flutter/material.dart';
import '../../core/services/notification_inbox_service.dart';

class NotificationInboxPanel extends StatefulWidget {
  final NotificationInboxService service;
  const NotificationInboxPanel({super.key, required this.service});

  @override
  State<NotificationInboxPanel> createState() => _NotificationInboxPanelState();
}

class _NotificationInboxPanelState extends State<NotificationInboxPanel> {
  bool _loading = true;
  String? _error;
  List<InboxNotification> _items = [];
  int _version = 0;

  @override
  void initState() { super.initState(); _load(); }

  @override
  void dispose() { _version++; super.dispose(); }

  Future<void> _load() async {
    final version = ++_version;
    setState(() { _loading = true; _error = null; });
    try {
      final items = await widget.service.list();
      if (mounted && version == _version) {
        setState(() { _items = items; _loading = false; });
      }
    } catch (_) {
      if (mounted && version == _version) {
        setState(() { _error = 'Unable to load notifications'; _loading = false; });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_error != null) {
      return Center(child: TextButton(onPressed: _load, child: const Text('Retry loading notifications')));
    }
    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.builder(
        physics: const AlwaysScrollableScrollPhysics(),
        itemCount: _items.isEmpty ? 1 : _items.length,
        itemBuilder: (context, index) {
          if (_items.isEmpty) {
            return const ListTile(
              leading: Icon(Icons.notifications_none),
              title: Text('No notifications yet'),
              subtitle: Text('Delivered reminders will appear here.'));
          }
          final item = _items[index];
          return ListTile(
            leading: const Icon(Icons.notifications_outlined),
            title: Text(item.title),
            subtitle: Text([
              if (item.body != null) item.body!,
              if (item.deliveredAt != null)
                item.deliveredAt!.toIso8601String().split('T').first,
            ].join(' • ')),
          );
        },
      ),
    );
  }
}
