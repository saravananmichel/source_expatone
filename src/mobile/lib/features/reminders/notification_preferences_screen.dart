import 'package:flutter/material.dart';
import '../../core/services/notification_preferences_service.dart';

class NotificationPreferencesScreen extends StatefulWidget {
  final NotificationPreferencesService service;
  const NotificationPreferencesScreen({super.key, required this.service});

  @override
  State<NotificationPreferencesScreen> createState() => _NotificationPreferencesScreenState();
}

class _NotificationPreferencesScreenState extends State<NotificationPreferencesScreen> {
  NotificationPreferences? _preferences;
  bool _loading = true;
  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final result = await widget.service.get();
      if (mounted) setState(() { _preferences = result; _loading = false; });
    } catch (_) {
      if (mounted) setState(() { _error = 'Could not load notification settings'; _loading = false; });
    }
  }

  void _change(NotificationPreferences Function(NotificationPreferences) transform) {
    final current = _preferences;
    if (current == null || _saving) return;
    setState(() => _preferences = transform(current));
  }

  NotificationPreferences _copy(NotificationPreferences p, String field, bool value) =>
      NotificationPreferences(
        inAppEnabled: field == 'inApp' ? value : p.inAppEnabled,
        pushEnabled: field == 'push' ? value : p.pushEnabled,
        documentEnabled: field == 'document' ? value : p.documentEnabled,
        connectionEnabled: field == 'connection' ? value : p.connectionEnabled,
        corporateEnabled: field == 'corporate' ? value : p.corporateEnabled,
        sharingEnabled: field == 'sharing' ? value : p.sharingEnabled,
        visaEnabled: field == 'visa' ? value : p.visaEnabled,
        occasionEnabled: field == 'occasion' ? value : p.occasionEnabled,
      );

  Widget _toggle(String label, String field, bool value, {String? subtitle}) =>
      SwitchListTile(
        title: Text(label),
        subtitle: subtitle == null ? null : Text(subtitle),
        value: value,
        onChanged: _saving ? null : (next) => _change((p) => _copy(p, field, next)),
      );

  Future<void> _save() async {
    final current = _preferences;
    if (current == null || _saving) return;
    setState(() => _saving = true);
    try {
      final saved = await widget.service.update(current);
      if (!mounted) return;
      setState(() => _preferences = saved);
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Notification preferences saved')));
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Unable to save notification preferences')));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final p = _preferences;
    return Scaffold(
      appBar: AppBar(title: const Text('Notification settings')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: TextButton(onPressed: _load, child: Text('Retry: $_error')))
              : p == null
                  ? const Center(child: Text('Settings unavailable'))
                  : ListView(children: [
                      _toggle('In-app notifications', 'inApp', p.inAppEnabled),
                      _toggle('Push notifications', 'push', p.pushEnabled,
                          subtitle: 'Requires device registration and push delivery setup'),
                      const Divider(),
                      _toggle('Documents', 'document', p.documentEnabled),
                      _toggle('Connections', 'connection', p.connectionEnabled),
                      _toggle('Corporate tasks', 'corporate', p.corporateEnabled),
                      _toggle('Sharing expiry', 'sharing', p.sharingEnabled),
                      _toggle('Visa drafts', 'visa', p.visaEnabled),
                      _toggle('Family occasions', 'occasion', p.occasionEnabled),
                      Padding(
                        padding: const EdgeInsets.all(16),
                        child: FilledButton(
                          onPressed: _saving ? null : _save,
                          child: Text(_saving ? 'Saving...' : 'Save preferences'),
                        ),
                      ),
                    ]),
    );
  }
}
