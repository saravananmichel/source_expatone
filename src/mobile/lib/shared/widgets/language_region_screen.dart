import 'package:flutter/material.dart';
import '../../core/services/user_service.dart';

/// Account preferences persisted in the existing user profile API.
class LanguageRegionScreen extends StatefulWidget {
  final UserService userService;
  const LanguageRegionScreen({super.key, required this.userService});

  @override
  State<LanguageRegionScreen> createState() => _LanguageRegionScreenState();
}

class _LanguageRegionScreenState extends State<LanguageRegionScreen> {
  String? _language;
  String? _country;
  bool _busy = true;
  String? _error;

  static const _languages = {
    'en': 'English',
    'ms': 'Bahasa Melayu',
    'id': 'Bahasa Indonesia',
    'zh': 'Chinese',
    'ta': 'Tamil',
    'hi': 'Hindi',
    'ar': 'Arabic',
  };
  static const _countries = {
    'MY': 'Malaysia', 'SG': 'Singapore', 'IN': 'India',
    'ID': 'Indonesia', 'CN': 'China', 'AE': 'United Arab Emirates',
    'BH': 'Bahrain', 'KW': 'Kuwait', 'OM': 'Oman',
    'QA': 'Qatar', 'SA': 'Saudi Arabia',
  };

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final profile = await widget.userService.getMe();
      if (!mounted) return;
      setState(() {
        _language = profile.preferredLanguage;
        _country = profile.countryCode;
        _busy = false;
      });
    } catch (_) {
      if (mounted) setState(() {
        _error = 'Unable to load your preferences.';
        _busy = false;
      });
    }
  }

  Future<void> _save() async {
    setState(() { _busy = true; _error = null; });
    try {
      await widget.userService.updateMe(
        preferredLanguage: _language,
        countryCode: _country,
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Language and region saved.')),
      );
      Navigator.of(context).pop(true);
    } catch (_) {
      if (mounted) setState(() {
        _error = 'Unable to save preferences.';
        _busy = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Language & Region')),
    body: _busy ? const Center(child: CircularProgressIndicator()) :
      ListView(padding: const EdgeInsets.all(20), children: [
        const Text('Choose your preferred language and destination region.'),
        const SizedBox(height: 24),
        DropdownButtonFormField<String>(
          value: _languages.containsKey(_language) ? _language : null,
          decoration: const InputDecoration(labelText: 'Preferred language'),
          items: _languages.entries.map((e) => DropdownMenuItem(
            value: e.key, child: Text(e.value))).toList(),
          onChanged: (v) => setState(() => _language = v),
        ),
        const SizedBox(height: 18),
        DropdownButtonFormField<String>(
          value: _countries.containsKey(_country) ? _country : null,
          decoration: const InputDecoration(labelText: 'Region'),
          items: _countries.entries.map((e) => DropdownMenuItem(
            value: e.key, child: Text(e.value))).toList(),
          onChanged: (v) => setState(() => _country = v),
        ),
        if (_error != null) Padding(
          padding: const EdgeInsets.symmetric(vertical: 12),
          child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
        ),
        const SizedBox(height: 24),
        FilledButton(onPressed: _save, child: const Text('Save preferences')),
      ]),
  );
}
