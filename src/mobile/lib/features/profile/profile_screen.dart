import 'package:flutter/material.dart';
import '../../core/services/auth_service.dart';
import '../../core/services/user_service.dart';
import '../../core/theme/app_theme.dart';

class ProfileScreen extends StatefulWidget {
  final IAuthService authService;
  final UserService userService;
  final VoidCallback onLogout;

  const ProfileScreen({
    super.key,
    required this.authService,
    required this.userService,
    required this.onLogout,
  });

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  UserProfile? _profile;
  ProfileOptions? _options;
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadProfile();
  }

  Future<void> _loadProfile() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    try {
      final results = await Future.wait([
        widget.userService.getMe(),
        widget.userService.getProfileOptions(),
      ]);
      if (mounted) {
        setState(() {
          _profile = results[0] as UserProfile;
          _options = results[1] as ProfileOptions;
          _isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = 'Unable to load profile';
          _isLoading = false;
        });
      }
    }
  }

  Future<void> _logout() async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Sign Out'),
        content: const Text('Are you sure you want to sign out?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Sign Out'),
          ),
        ],
      ),
    );

    if (confirm != true) return;

    try {
      await widget.authService.signOut();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Sign out failed. Please try again.')),
        );
      }
      return;
    }

    if (mounted) {
      // Pop all pushed routes (ProfileScreen, MoreScreen overlay, etc.) so that
      // when main.dart rebuilds home as LoginScreen it becomes immediately visible.
      Navigator.of(context).popUntil((route) => route.isFirst);
      widget.onLogout();
    }
  }

  void _showEditSheet() {
    if (_profile == null || _options == null) return;

    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => _ProfileEditScreen(
          profile: _profile!,
          options: _options!,
          userService: widget.userService,
          onSaved: (updated) {
            setState(() => _profile = updated);
          },
        ),
      ),
    );
  }

  String _getLabel(List<ProfileOptionItem>? options, String? value) {
    if (value == null || options == null) return 'Not set';
    for (final o in options) {
      if (o.value == value) return o.label;
    }
    return value;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Profile'),
        actions: [
          if (_profile != null)
            IconButton(
              icon: const Icon(Icons.edit_outlined),
              onPressed: _showEditSheet,
            ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Text(_error!, style: TextStyle(color: AppTheme.errorColor)),
                      const SizedBox(height: 12),
                      ElevatedButton(onPressed: _loadProfile, child: const Text('Retry')),
                    ],
                  ),
                )
              : _buildProfileContent(),
    );
  }

  Widget _buildProfileContent() {
    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        Center(
          child: CircleAvatar(
            radius: 48,
            backgroundColor: AppTheme.primaryColor.withValues(alpha: 0.1),
            child: Text(
              (_profile!.displayName?.isNotEmpty == true)
                  ? _profile!.displayName![0].toUpperCase()
                  : (_profile!.email[0].toUpperCase()),
              style: TextStyle(
                fontSize: 36,
                fontWeight: FontWeight.bold,
                color: AppTheme.primaryColor,
              ),
            ),
          ),
        ),
        const SizedBox(height: 16),
        Text(
          _profile!.displayName ?? 'ExpatOne User',
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.headlineMedium,
        ),
        const SizedBox(height: 4),
        Text(
          _profile!.email,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                color: AppTheme.textSecondary,
              ),
        ),
        const SizedBox(height: 32),
        _buildSectionHeader('Account'),
        _buildInfoTile(Icons.email_outlined, 'Email', _profile!.email),
        _buildInfoTile(Icons.person_outlined, 'Name', _profile!.displayName ?? 'Not set'),
        _buildInfoTile(Icons.phone_outlined, 'Phone', _profile!.phoneNumber ?? 'Not set'),
        const SizedBox(height: 20),
        _buildSectionHeader('Personal'),
        _buildInfoTile(Icons.flag_outlined, 'Nationality', _profile!.nationality ?? 'Not set'),
        _buildInfoTile(Icons.location_on_outlined, 'Country', _profile!.countryCode),
        _buildInfoTile(Icons.place_outlined, 'Location', _profile!.residenceLocation ?? 'Not set'),
        const SizedBox(height: 20),
        _buildSectionHeader('Status'),
        _buildInfoTile(Icons.badge_outlined, 'Visa/Pass', _getLabel(_options?.visaPassTypes, _profile!.visaPassType)),
        _buildInfoTile(Icons.work_outlined, 'Employment', _getLabel(_options?.employmentStatuses, _profile!.employmentStatus)),
        _buildInfoTile(Icons.family_restroom_outlined, 'Family', _getLabel(_options?.familyStatuses, _profile!.familyStatus)),
        if (_profile!.hasChildren == true)
          _buildInfoTile(Icons.child_care_outlined, 'Children', '${_profile!.numberOfChildren ?? 0}'),
        const SizedBox(height: 20),
        _buildSectionHeader('Preferences'),
        _buildInfoTile(Icons.language_outlined, 'Language', _getLabel(_options?.supportedLanguages, _profile!.preferredLanguage)),
        const SizedBox(height: 32),
        OutlinedButton.icon(
          onPressed: _logout,
          icon: const Icon(Icons.logout, color: AppTheme.errorColor),
          label: const Text('Sign Out', style: TextStyle(color: AppTheme.errorColor)),
          style: OutlinedButton.styleFrom(
            side: BorderSide(color: AppTheme.errorColor.withValues(alpha: 0.5)),
            padding: const EdgeInsets.symmetric(vertical: 14),
          ),
        ),
      ],
    );
  }

  Widget _buildSectionHeader(String title) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Text(
        title,
        style: TextStyle(
          fontSize: 13,
          fontWeight: FontWeight.w600,
          color: AppTheme.textSecondary,
          letterSpacing: 0.5,
        ),
      ),
    );
  }

  Widget _buildInfoTile(IconData icon, String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          Icon(icon, size: 20, color: AppTheme.textSecondary),
          const SizedBox(width: 12),
          Text(label, style: TextStyle(color: AppTheme.textSecondary, fontSize: 14)),
          const Spacer(),
          Text(value, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500)),
        ],
      ),
    );
  }
}

class _ProfileEditScreen extends StatefulWidget {
  final UserProfile profile;
  final ProfileOptions options;
  final UserService userService;
  final ValueChanged<UserProfile> onSaved;

  const _ProfileEditScreen({
    required this.profile,
    required this.options,
    required this.userService,
    required this.onSaved,
  });

  @override
  State<_ProfileEditScreen> createState() => _ProfileEditScreenState();
}

class _ProfileEditScreenState extends State<_ProfileEditScreen> {
  late final TextEditingController _nameController;
  late final TextEditingController _phoneController;
  late final TextEditingController _locationController;

  late String? _nationality;
  late String _countryCode;
  late String? _visaPassType;
  late String? _employmentStatus;
  late String? _familyStatus;
  late bool _hasChildren;
  late int _numberOfChildren;
  late String _preferredLanguage;

  bool _isSaving = false;

  @override
  void initState() {
    super.initState();
    _nameController = TextEditingController(text: widget.profile.displayName ?? '');
    _phoneController = TextEditingController(text: widget.profile.phoneNumber ?? '');
    _locationController = TextEditingController(text: widget.profile.residenceLocation ?? '');
    _nationality = widget.profile.nationality;
    _countryCode = widget.profile.countryCode;
    _visaPassType = widget.profile.visaPassType;
    _employmentStatus = widget.profile.employmentStatus;
    _familyStatus = widget.profile.familyStatus;
    _hasChildren = widget.profile.hasChildren ?? false;
    _numberOfChildren = widget.profile.numberOfChildren ?? 0;
    _preferredLanguage = widget.profile.preferredLanguage;
  }

  @override
  void dispose() {
    _nameController.dispose();
    _phoneController.dispose();
    _locationController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() => _isSaving = true);
    try {
      final updated = await widget.userService.updateMe(
        displayName: _nameController.text.trim(),
        phoneNumber: _phoneController.text.trim(),
        nationality: _nationality,
        countryCode: _countryCode,
        residenceLocation: _locationController.text.trim(),
        visaPassType: _visaPassType,
        employmentStatus: _employmentStatus,
        familyStatus: _familyStatus,
        hasChildren: _hasChildren,
        numberOfChildren: _hasChildren ? _numberOfChildren : 0,
        preferredLanguage: _preferredLanguage,
      );
      widget.onSaved(updated);
      if (mounted) Navigator.pop(context);
    } catch (e) {
      if (mounted) {
        setState(() => _isSaving = false);
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Failed to save: $e')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Edit Profile'),
        actions: [
          TextButton(
            onPressed: _isSaving ? null : _save,
            child: _isSaving
                ? const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Save'),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          _buildReadOnlyField('Email', widget.profile.email),
          const SizedBox(height: 16),
          TextField(
            controller: _nameController,
            decoration: const InputDecoration(
              labelText: 'Display Name',
              prefixIcon: Icon(Icons.person_outlined),
            ),
            textCapitalization: TextCapitalization.words,
          ),
          const SizedBox(height: 16),
          TextField(
            controller: _phoneController,
            decoration: const InputDecoration(
              labelText: 'Phone Number',
              prefixIcon: Icon(Icons.phone_outlined),
            ),
            keyboardType: TextInputType.phone,
          ),
          const SizedBox(height: 24),
          _buildDropdown(
            label: 'Nationality',
            value: _nationality,
            items: _nationalityItems(),
            onChanged: (v) => setState(() => _nationality = v),
          ),
          const SizedBox(height: 16),
          TextField(
            controller: _locationController,
            decoration: const InputDecoration(
              labelText: 'City / Area',
              prefixIcon: Icon(Icons.location_on_outlined),
              hintText: 'e.g. Kuala Lumpur',
            ),
          ),
          const SizedBox(height: 16),
          _buildDropdown(
            label: 'Visa / Pass Type',
            value: _visaPassType,
            items: widget.options.visaPassTypes
                .map((o) => DropdownMenuItem(value: o.value, child: Text(o.label)))
                .toList(),
            onChanged: (v) => setState(() => _visaPassType = v),
          ),
          const SizedBox(height: 16),
          _buildDropdown(
            label: 'Employment Status',
            value: _employmentStatus,
            items: widget.options.employmentStatuses
                .map((o) => DropdownMenuItem(value: o.value, child: Text(o.label)))
                .toList(),
            onChanged: (v) => setState(() => _employmentStatus = v),
          ),
          const SizedBox(height: 16),
          _buildDropdown(
            label: 'Family Status',
            value: _familyStatus,
            items: widget.options.familyStatuses
                .map((o) => DropdownMenuItem(value: o.value, child: Text(o.label)))
                .toList(),
            onChanged: (v) => setState(() => _familyStatus = v),
          ),
          const SizedBox(height: 16),
          SwitchListTile(
            value: _hasChildren,
            onChanged: (v) => setState(() {
              _hasChildren = v;
              if (!v) _numberOfChildren = 0;
            }),
            title: const Text('Have children'),
            activeColor: AppTheme.primaryColor,
            contentPadding: EdgeInsets.zero,
          ),
          if (_hasChildren) ...[
            Row(
              children: [
                const Text('Number of children:'),
                const Spacer(),
                IconButton(
                  icon: const Icon(Icons.remove_circle_outline),
                  onPressed: _numberOfChildren > 1
                      ? () => setState(() => _numberOfChildren--)
                      : null,
                ),
                Text('$_numberOfChildren', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w600)),
                IconButton(
                  icon: const Icon(Icons.add_circle_outline),
                  onPressed: _numberOfChildren < 20
                      ? () => setState(() => _numberOfChildren++)
                      : null,
                ),
              ],
            ),
          ],
          const SizedBox(height: 16),
          _buildDropdown(
            label: 'Preferred Language',
            value: _preferredLanguage,
            items: widget.options.supportedLanguages
                .map((o) => DropdownMenuItem(value: o.value, child: Text(o.label)))
                .toList(),
            onChanged: (v) => setState(() => _preferredLanguage = v ?? 'en'),
          ),
        ],
      ),
    );
  }

  Widget _buildReadOnlyField(String label, String value) {
    return TextField(
      controller: TextEditingController(text: value),
      decoration: InputDecoration(
        labelText: label,
        prefixIcon: const Icon(Icons.email_outlined),
        suffixIcon: const Icon(Icons.lock_outlined, size: 16),
      ),
      readOnly: true,
      enabled: false,
    );
  }

  Widget _buildDropdown({
    required String label,
    required String? value,
    required List<DropdownMenuItem<String>> items,
    required ValueChanged<String?> onChanged,
  }) {
    return DropdownButtonFormField<String>(
      value: value,
      decoration: InputDecoration(labelText: label),
      items: items,
      onChanged: onChanged,
      isExpanded: true,
    );
  }

  List<DropdownMenuItem<String>> _nationalityItems() {
    const countries = [
      ('MY', 'Malaysia'),
      ('IN', 'India'),
      ('CN', 'China'),
      ('GB', 'United Kingdom'),
      ('US', 'United States'),
      ('AU', 'Australia'),
      ('SG', 'Singapore'),
      ('JP', 'Japan'),
      ('KR', 'South Korea'),
      ('BD', 'Bangladesh'),
      ('ID', 'Indonesia'),
      ('PH', 'Philippines'),
      ('PK', 'Pakistan'),
      ('DE', 'Germany'),
      ('FR', 'France'),
      ('NL', 'Netherlands'),
      ('CA', 'Canada'),
      ('NZ', 'New Zealand'),
    ];
    return countries
        .map((c) => DropdownMenuItem(value: c.$1, child: Text(c.$2)))
        .toList();
  }
}
