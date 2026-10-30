import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Local UI preferences. Notification delivery requires separate server-side configuration.
class SettingsScreen extends StatefulWidget {
  const SettingsScreen({super.key});

  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  bool _expiryAlerts = true;
  bool _compactCards = false;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final prefs = await SharedPreferences.getInstance();
    if (!mounted) return;
    setState(() {
      _expiryAlerts = prefs.getBool('expatone.ui.expiryAlerts') ?? true;
      _compactCards = prefs.getBool('expatone.ui.compactCards') ?? false;
      _loading = false;
    });
  }

  Future<void> _setExpiryAlerts(bool value) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool('expatone.ui.expiryAlerts', value);
    if (mounted) setState(() => _expiryAlerts = value);
  }

  Future<void> _setCompactCards(bool value) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool('expatone.ui.compactCards', value);
    if (mounted) setState(() => _compactCards = value);
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Settings')),
    body: _loading
      ? const Center(child: CircularProgressIndicator())
      : ListView(padding: const EdgeInsets.all(20), children: [
          Text('App preferences', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 8),
          SwitchListTile(
            title: const Text('Document expiry alert preference'),
            subtitle: const Text('Saved on this device; does not change server notifications.'),
            value: _expiryAlerts,
            onChanged: _setExpiryAlerts,
          ),
          SwitchListTile(
            title: const Text('Compact dashboard cards'),
            subtitle: const Text('Saved for future dashboard layout options.'),
            value: _compactCards,
            onChanged: _setCompactCards,
          ),
          const Divider(),
          const ListTile(
            leading: Icon(Icons.lock_outline),
            title: Text('Privacy & Security'),
            subtitle: Text('Your documents remain protected by your account permissions.'),
          ),
        ]),
  );
}
