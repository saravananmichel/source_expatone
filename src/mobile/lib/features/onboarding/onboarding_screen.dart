import 'package:flutter/material.dart';

import '../../core/services/user_service.dart';
import '../../core/theme/app_theme.dart';

class OnboardingScreen extends StatefulWidget {
  final UserService userService;
  final VoidCallback onComplete;

  const OnboardingScreen({
    super.key,
    required this.userService,
    required this.onComplete,
  });

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen> {
  final _pageController = PageController();
  int _currentStep = 0;
  static const _totalSteps = 8;

  ProfileOptions? _options;
  bool _isLoadingOptions = true;
  bool _isSaving = false;
  String? _error;

  String? _nationality;
  String _countryOfResidence = 'MY';
  String _residenceLocation = '';
  String? _visaPassType;
  String? _employmentStatus;
  String? _familyStatus;
  bool _hasChildren = false;
  int _numberOfChildren = 0;
  String _preferredLanguage = 'en';

  final _residenceController = TextEditingController();

  @override
  void initState() {
    super.initState();
    _loadOptions();
  }

  @override
  void dispose() {
    _pageController.dispose();
    _residenceController.dispose();
    super.dispose();
  }

  Future<void> _loadOptions() async {
    try {
      final options = await widget.userService.getProfileOptions();
      if (mounted) {
        setState(() {
          _options = options;
          _isLoadingOptions = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = 'Unable to load profile options';
          _isLoadingOptions = false;
        });
      }
    }
  }

  Future<void> _saveProgress() async {
    setState(() => _isSaving = true);
    try {
      await widget.userService.updateMe(
        nationality: _nationality,
        countryCode: _countryOfResidence,
        residenceLocation: _residenceLocation.isNotEmpty
            ? _residenceLocation
            : null,
        visaPassType: _visaPassType,
        employmentStatus: _employmentStatus,
        familyStatus: _familyStatus,
        hasChildren: _hasChildren,
        numberOfChildren: _hasChildren ? _numberOfChildren : 0,
        preferredLanguage: _preferredLanguage,
      );
    } catch (_) {
      // Progress save is best-effort; don't block navigation
    }
    if (mounted) setState(() => _isSaving = false);
  }

  Future<void> _completeOnboarding() async {
    setState(() => _isSaving = true);
    try {
      await widget.userService.updateMe(
        nationality: _nationality,
        countryCode: _countryOfResidence,
        residenceLocation: _residenceLocation.isNotEmpty
            ? _residenceLocation
            : null,
        visaPassType: _visaPassType,
        employmentStatus: _employmentStatus,
        familyStatus: _familyStatus,
        hasChildren: _hasChildren,
        numberOfChildren: _hasChildren ? _numberOfChildren : 0,
        preferredLanguage: _preferredLanguage,
        onboardingCompleted: true,
      );
      if (mounted) widget.onComplete();
    } catch (e) {
      if (mounted) {
        setState(() => _isSaving = false);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Failed to save. Please try again.')),
        );
      }
    }
  }

  void _next() {
    if (_currentStep < _totalSteps - 1) {
      if (_currentStep > 0) _saveProgress();
      _pageController.nextPage(
        duration: const Duration(milliseconds: 300),
        curve: Curves.easeInOut,
      );
    }
  }

  void _back() {
    if (_currentStep > 0) {
      _pageController.previousPage(
        duration: const Duration(milliseconds: 300),
        curve: Curves.easeInOut,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_isLoadingOptions) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    if (_error != null) {
      return Scaffold(
        body: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text(_error!, style: TextStyle(color: AppTheme.errorColor)),
              const SizedBox(height: 12),
              ElevatedButton(
                onPressed: _loadOptions,
                child: const Text('Retry'),
              ),
            ],
          ),
        ),
      );
    }

    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            if (_currentStep > 0) _buildProgressBar(),
            Expanded(
              child: PageView(
                controller: _pageController,
                physics: const NeverScrollableScrollPhysics(),
                onPageChanged: (index) => setState(() => _currentStep = index),
                children: [
                  _buildWelcomeStep(),
                  _buildNationalityStep(),
                  _buildResidenceStep(),
                  _buildVisaStep(),
                  _buildEmploymentStep(),
                  _buildFamilyStep(),
                  _buildLanguageStep(),
                  _buildCompleteStep(),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildProgressBar() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 12, 20, 0),
      child: Column(
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Step $_currentStep of ${_totalSteps - 1}',
                style: TextStyle(color: AppTheme.textSecondary, fontSize: 13),
              ),
              Text(
                '${((_currentStep / (_totalSteps - 1)) * 100).round()}%',
                style: TextStyle(color: AppTheme.textSecondary, fontSize: 13),
              ),
            ],
          ),
          const SizedBox(height: 8),
          LinearProgressIndicator(
            value: _currentStep / (_totalSteps - 1),
            backgroundColor: AppTheme.primaryColor.withValues(alpha: 0.1),
            color: AppTheme.primaryColor,
            minHeight: 4,
            borderRadius: BorderRadius.circular(2),
          ),
        ],
      ),
    );
  }

  Widget _buildStepScaffold({
    required String title,
    required String subtitle,
    required Widget content,
    bool showBack = true,
    bool showContinue = true,
    bool canContinue = true,
    VoidCallback? onContinue,
    String continueLabel = 'Continue',
  }) {
    return Padding(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const SizedBox(height: 16),
          Text(title, style: Theme.of(context).textTheme.headlineMedium),
          const SizedBox(height: 8),
          Text(
            subtitle,
            style: Theme.of(context).textTheme.bodyLarge
                ?.copyWith(color: AppTheme.textSecondary),
          ),
          const SizedBox(height: 32),
          Expanded(child: content),
          const SizedBox(height: 16),
          Row(
            children: [
              if (showBack)
                Expanded(
                  child: OutlinedButton(
                    onPressed: _isSaving ? null : _back,
                    child: const Text('Back'),
                  ),
                ),
              if (showBack && showContinue) const SizedBox(width: 12),
              if (showContinue)
                Expanded(
                  flex: showBack ? 2 : 1,
                  child: SizedBox(
                    height: 50,
                    child: ElevatedButton(
                      onPressed: (_isSaving || !canContinue)
                          ? null
                          : (onContinue ?? _next),
                      child: _isSaving
                          ? const SizedBox(
                              width: 20,
                              height: 20,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: Colors.white,
                              ),
                            )
                          : Text(
                              continueLabel,
                              style: const TextStyle(fontSize: 16),
                            ),
                    ),
                  ),
                ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildWelcomeStep() {
    return _buildStepScaffold(
      title: 'Welcome to ExpatOne',
      subtitle: 'Your AI-powered assistant for living in Malaysia. Let\'s personalize your experience.',
      showBack: false,
      continueLabel: 'Get Started',
      content: Column(
        children: [
          const SizedBox(height: 24),
          Icon(
            Icons.public,
            size: 80,
            color: AppTheme.primaryColor.withValues(alpha: 0.3),
          ),
          const SizedBox(height: 32),
          _buildFeatureRow(
            Icons.description_outlined,
            'Secure document wallet',
          ),
          const SizedBox(height: 16),
          _buildFeatureRow(
            Icons.account_balance_outlined,
            'Government process guidance',
          ),
          const SizedBox(height: 16),
          _buildFeatureRow(
            Icons.translate_outlined,
            'Multilingual translation',
          ),
          const SizedBox(height: 16),
          _buildFeatureRow(
            Icons.notifications_outlined,
            'Smart expiry reminders',
          ),
        ],
      ),
    );
  }

  Widget _buildFeatureRow(IconData icon, String label) {
    return Row(
      children: [
        Icon(icon, color: AppTheme.secondaryColor, size: 24),
        const SizedBox(width: 12),
        Text(label, style: const TextStyle(fontSize: 16)),
      ],
    );
  }

  Widget _buildNationalityStep() {
    final commonCountries = [
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

    return _buildStepScaffold(
      title: 'What\'s your nationality?',
      subtitle: 'This helps us tailor government process information for you.',
      content: RadioGroup<String>(
        groupValue: _nationality,
        onChanged: (v) => setState(() => _nationality = v),
        child: ListView.builder(
          itemCount: commonCountries.length,
          itemBuilder: (context, index) {
            final (code, name) = commonCountries[index];
            return RadioListTile<String>(
              value: code,
              title: Text(name),
              subtitle: Text(code),
              activeColor: AppTheme.primaryColor,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(8),
              ),
            );
          },
        ),
      ),
    );
  }

  Widget _buildResidenceStep() {
    final countries = [('MY', 'Malaysia'), ('SG', 'Singapore')];

    return _buildStepScaffold(
      title: 'Where do you live?',
      subtitle: 'Your country and city of residence in Malaysia.',
      content: RadioGroup<String>(
        groupValue: _countryOfResidence,
        onChanged: (v) => setState(() => _countryOfResidence = v ?? 'MY'),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            ...countries.map((c) {
              final (code, name) = c;
              return RadioListTile<String>(
                value: code,
                title: Text(name),
                activeColor: AppTheme.primaryColor,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(8),
                ),
              );
            }),
            const SizedBox(height: 20),
            TextField(
              controller: _residenceController,
              decoration: const InputDecoration(
                labelText: 'City / Area (optional)',
                hintText: 'e.g. Kuala Lumpur, Penang, Johor Bahru',
                prefixIcon: Icon(Icons.location_on_outlined),
              ),
              onChanged: (v) => _residenceLocation = v.trim(),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildVisaStep() {
    final types = _options?.visaPassTypes ?? [];
    return _buildStepScaffold(
      title: 'Your visa / pass type',
      subtitle: 'Select the type of visa or pass you currently hold.',
      content: RadioGroup<String>(
        groupValue: _visaPassType,
        onChanged: (v) => setState(() => _visaPassType = v),
        child: ListView.builder(
          itemCount: types.length,
          itemBuilder: (context, index) {
            final option = types[index];
            return RadioListTile<String>(
              value: option.value,
              title: Text(option.label),
              activeColor: AppTheme.primaryColor,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(8),
              ),
            );
          },
        ),
      ),
    );
  }

  Widget _buildEmploymentStep() {
    final statuses = _options?.employmentStatuses ?? [];
    return _buildStepScaffold(
      title: 'Employment status',
      subtitle: 'This helps us show relevant employment-related guidance.',
      content: RadioGroup<String>(
        groupValue: _employmentStatus,
        onChanged: (v) => setState(() => _employmentStatus = v),
        child: ListView.builder(
          itemCount: statuses.length,
          itemBuilder: (context, index) {
            final option = statuses[index];
            return RadioListTile<String>(
              value: option.value,
              title: Text(option.label),
              activeColor: AppTheme.primaryColor,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(8),
              ),
            );
          },
        ),
      ),
    );
  }

  Widget _buildFamilyStep() {
    final statuses = _options?.familyStatuses ?? [];
    return _buildStepScaffold(
      title: 'Family status',
      subtitle: 'We\'ll tailor family-related reminders and guidance for you.',
      content: RadioGroup<String>(
        groupValue: _familyStatus,
        onChanged: (v) => setState(() => _familyStatus = v),
        child: ListView(
          children: [
            ...statuses.map(
              (option) => RadioListTile<String>(
                value: option.value,
                title: Text(option.label),
                activeColor: AppTheme.primaryColor,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(8),
                ),
              ),
            ),
            const SizedBox(height: 24),
            SwitchListTile(
              value: _hasChildren,
              onChanged: (v) => setState(() {
                _hasChildren = v;
                if (!v) _numberOfChildren = 0;
              }),
              title: const Text('Do you have children?'),
              activeThumbColor: AppTheme.primaryColor,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(8),
              ),
            ),
            if (_hasChildren) ...[
              const SizedBox(height: 12),
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Row(
                  children: [
                    const Text('Number of children:'),
                    const Spacer(),
                    IconButton(
                      icon: const Icon(Icons.remove_circle_outline),
                      onPressed: _numberOfChildren > 1
                          ? () => setState(() => _numberOfChildren--)
                          : null,
                    ),
                    Text(
                      '$_numberOfChildren',
                      style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    IconButton(
                      icon: const Icon(Icons.add_circle_outline),
                      onPressed: _numberOfChildren < 20
                          ? () => setState(() => _numberOfChildren++)
                          : null,
                    ),
                  ],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildLanguageStep() {
    final languages = _options?.supportedLanguages ?? [];
    return _buildStepScaffold(
      title: 'Preferred language',
      subtitle: 'Choose the language you\'d like to use in ExpatOne.',
      content: RadioGroup<String>(
        groupValue: _preferredLanguage,
        onChanged: (v) => setState(() => _preferredLanguage = v ?? 'en'),
        child: ListView.builder(
          itemCount: languages.length,
          itemBuilder: (context, index) {
            final option = languages[index];
            return RadioListTile<String>(
              value: option.value,
              title: Text(option.label),
              activeColor: AppTheme.primaryColor,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(8),
              ),
            );
          },
        ),
      ),
    );
  }

  Widget _buildCompleteStep() {
    return _buildStepScaffold(
      title: 'You\'re all set!',
      subtitle: 'Your profile is ready. You can always update it later.',
      showBack: true,
      continueLabel: 'Go to Dashboard',
      onContinue: _completeOnboarding,
      content: ListView(
        children: [
          Icon(Icons.check_circle, size: 64, color: AppTheme.successColor),
          const SizedBox(height: 24),
          if (_nationality != null)
            _buildSummaryTile('Nationality', _nationality!),
          _buildSummaryTile('Country', _countryOfResidence),
          if (_residenceLocation.isNotEmpty)
            _buildSummaryTile('Location', _residenceLocation),
          if (_visaPassType != null)
            _buildSummaryTile(
              'Visa/Pass',
              _getOptionLabel(_options?.visaPassTypes, _visaPassType!),
            ),
          if (_employmentStatus != null)
            _buildSummaryTile(
              'Employment',
              _getOptionLabel(_options?.employmentStatuses, _employmentStatus!),
            ),
          if (_familyStatus != null)
            _buildSummaryTile(
              'Family',
              _getOptionLabel(_options?.familyStatuses, _familyStatus!),
            ),
          if (_hasChildren) _buildSummaryTile('Children', '$_numberOfChildren'),
          _buildSummaryTile(
            'Language',
            _getOptionLabel(_options?.supportedLanguages, _preferredLanguage),
          ),
        ],
      ),
    );
  }

  Widget _buildSummaryTile(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        children: [
          Text(
            label,
            style: TextStyle(color: AppTheme.textSecondary, fontSize: 14),
          ),
          const Spacer(),
          Text(
            value,
            style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500),
          ),
        ],
      ),
    );
  }

  String _getOptionLabel(List<ProfileOptionItem>? options, String value) {
    if (options == null) return value;
    for (final o in options) {
      if (o.value == value) return o.label;
    }
    return value;
  }
}
