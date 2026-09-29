import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';
import 'package:share_plus/share_plus.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../core/error/app_exception.dart';
import '../../core/services/emergency_service.dart';
import '../../core/theme/app_theme.dart';

class EmergencyScreen extends StatefulWidget {
  final EmergencyService emergencyService;

  const EmergencyScreen({super.key, required this.emergencyService});

  @override
  State<EmergencyScreen> createState() => _EmergencyScreenState();
}

class _EmergencyScreenState extends State<EmergencyScreen> {
  Position? _currentPosition;
  bool _isLoadingLocation = false;
  String? _locationError;

  final _descriptionController = TextEditingController();
  String _selectedLanguage = 'ms';

  bool _isLoadingAi = false;
  String? _aiError;
  EmergencyAssistResult? _aiResult;

  static const _emergencyNumber = '999';

  static const _targetLanguages = [
    _LangOption(code: 'ms', name: 'Malay'),
    _LangOption(code: 'zh', name: 'Chinese'),
    _LangOption(code: 'ta', name: 'Tamil'),
    _LangOption(code: 'hi', name: 'Hindi'),
    _LangOption(code: 'ar', name: 'Arabic'),
    _LangOption(code: 'ja', name: 'Japanese'),
    _LangOption(code: 'ko', name: 'Korean'),
    _LangOption(code: 'en', name: 'English'),
  ];

  @override
  void dispose() {
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _callEmergency() async {
    final uri = Uri(scheme: 'tel', path: _emergencyNumber);
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri);
    } else {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: const Text(
              'Calling is not available on this device. Dial 999 manually.',
            ),
            backgroundColor: AppTheme.errorColor,
            duration: const Duration(seconds: 5),
          ),
        );
      }
    }
  }

  Future<void> _getLocation() async {
    setState(() {
      _isLoadingLocation = true;
      _locationError = null;
    });

    try {
      final serviceEnabled = await Geolocator.isLocationServiceEnabled();
      if (!serviceEnabled) {
        if (mounted) {
          setState(() {
            _locationError = 'Location services are disabled. Please enable them in device settings.';
            _isLoadingLocation = false;
          });
        }
        return;
      }

      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
        if (permission == LocationPermission.denied) {
          if (mounted) {
            setState(() {
              _locationError = 'Location permission denied.';
              _isLoadingLocation = false;
            });
          }
          return;
        }
      }

      if (permission == LocationPermission.deniedForever) {
        if (mounted) {
          setState(() {
            _locationError = 'Location permission permanently denied. Please enable in device settings.';
            _isLoadingLocation = false;
          });
        }
        return;
      }

      final position = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.high,
          timeLimit: Duration(seconds: 15),
        ),
      );

      if (mounted) {
        setState(() {
          _currentPosition = position;
          _isLoadingLocation = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _locationError = 'Could not get location. Please try again.';
          _isLoadingLocation = false;
        });
      }
    }
  }

  Future<void> _shareLocation() async {
    if (_currentPosition == null) {
      await _getLocation();
      if (_currentPosition == null) return;
    }

    final lat = _currentPosition!.latitude;
    final lng = _currentPosition!.longitude;
    final mapsUrl = 'https://www.google.com/maps?q=$lat,$lng';

    final text = 'Emergency location:\n'
        'Latitude: $lat\n'
        'Longitude: $lng\n'
        'Maps: $mapsUrl';

    await SharePlus.instance.share(ShareParams(text: text));
  }

  Future<void> _getAiAssistance() async {
    final message = _descriptionController.text.trim();
    if (message.isEmpty) return;

    setState(() {
      _isLoadingAi = true;
      _aiError = null;
      _aiResult = null;
    });

    try {
      final result = await widget.emergencyService.assist(
        message: message,
        targetLanguage: _selectedLanguage,
      );
      if (mounted) {
        setState(() {
          _aiResult = result;
          _isLoadingAi = false;
        });
      }
    } on NetworkException catch (e) {
      if (mounted) {
        setState(() {
          _aiError = e.statusCode == 503
              ? 'AI assistance is unavailable. You can still call 999 directly.'
              : 'AI assistance failed. You can still call 999 directly.';
          _isLoadingAi = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _aiError = 'AI assistance failed. You can still call 999 directly.';
          _isLoadingAi = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Emergency'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _buildEmergencyCallSection(),
            const SizedBox(height: 20),
            _buildQuickActions(),
            const SizedBox(height: 20),
            _buildLocationSection(),
            const SizedBox(height: 20),
            _buildAiAssistanceSection(),
          ],
        ),
      ),
    );
  }

  Widget _buildEmergencyCallSection() {
    return Card(
      color: AppTheme.errorColor.withValues(alpha: 0.06),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(color: AppTheme.errorColor.withValues(alpha: 0.3)),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 28),
        child: Column(
          children: [
            const Icon(
              Icons.emergency,
              size: 48,
              color: AppTheme.errorColor,
            ),
            const SizedBox(height: 12),
            Text(
              'EMERGENCY',
              style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                    color: AppTheme.errorColor,
                    fontWeight: FontWeight.bold,
                  ),
            ),
            const SizedBox(height: 8),
            Text(
              'If you are in immediate danger:',
              style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                    color: AppTheme.textSecondary,
                  ),
            ),
            const SizedBox(height: 20),
            SizedBox(
              width: double.infinity,
              height: 56,
              child: ElevatedButton.icon(
                onPressed: _callEmergency,
                icon: const Icon(Icons.phone, size: 24),
                label: const Text(
                  'CALL 999',
                  style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
                ),
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppTheme.errorColor,
                  foregroundColor: Colors.white,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                ),
              ),
            ),
            const SizedBox(height: 12),
            Text(
              'Malaysia Emergency Response Services (MERS)\nPolice, Fire, Ambulance',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: AppTheme.textSecondary,
                    fontSize: 12,
                  ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildQuickActions() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Quick Actions', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 12),
        Row(
          children: [
            Expanded(
              child: _QuickActionCard(
                icon: Icons.local_police,
                label: 'Police',
                onTap: _callEmergency,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _QuickActionCard(
                icon: Icons.local_hospital,
                label: 'Medical',
                onTap: _callEmergency,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: _QuickActionCard(
                icon: Icons.local_fire_department,
                label: 'Fire',
                onTap: _callEmergency,
              ),
            ),
          ],
        ),
        const SizedBox(height: 6),
        Text(
          'All emergency services are reached through 999',
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(fontSize: 12),
        ),
      ],
    );
  }

  Widget _buildLocationSection() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Share My Location', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (_currentPosition != null) ...[
                  Row(
                    children: [
                      const Icon(Icons.location_on, color: AppTheme.successColor, size: 20),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          '${_currentPosition!.latitude.toStringAsFixed(6)}, '
                          '${_currentPosition!.longitude.toStringAsFixed(6)}',
                          style: const TextStyle(fontSize: 14, fontFamily: 'monospace'),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                ] else if (_locationError != null) ...[
                  Row(
                    children: [
                      const Icon(Icons.location_off, color: AppTheme.warningColor, size: 20),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          _locationError!,
                          style: const TextStyle(fontSize: 13, color: AppTheme.warningColor),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                ] else ...[
                  Row(
                    children: [
                      Icon(Icons.location_off, color: AppTheme.textSecondary.withValues(alpha: 0.5), size: 20),
                      const SizedBox(width: 8),
                      Text(
                        'Location not yet retrieved',
                        style: TextStyle(
                          fontSize: 13,
                          color: AppTheme.textSecondary.withValues(alpha: 0.7),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                ],
                Row(
                  children: [
                    Expanded(
                      child: OutlinedButton.icon(
                        onPressed: _isLoadingLocation ? null : _getLocation,
                        icon: _isLoadingLocation
                            ? const SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Icon(Icons.my_location, size: 18),
                        label: Text(_isLoadingLocation ? 'Getting location...' : 'Get Location'),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: ElevatedButton.icon(
                        onPressed: _shareLocation,
                        icon: const Icon(Icons.share, size: 18),
                        label: const Text('Share Location'),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildAiAssistanceSection() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('AI Emergency Help', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 4),
        Text(
          'Need help explaining your situation to responders?',
          style: Theme.of(context).textTheme.bodyMedium,
        ),
        const SizedBox(height: 12),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextField(
                  controller: _descriptionController,
                  maxLines: 3,
                  maxLength: 2000,
                  decoration: const InputDecoration(
                    hintText: 'Describe your emergency situation...',
                    counterText: '',
                  ),
                  onChanged: (_) => setState(() {}),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    const Text('Translate to: ', style: TextStyle(fontSize: 14)),
                    const SizedBox(width: 8),
                    Expanded(
                      child: DropdownButtonHideUnderline(
                        child: DropdownButton<String>(
                          value: _selectedLanguage,
                          isExpanded: true,
                          items: _targetLanguages
                              .map((lang) => DropdownMenuItem(
                                    value: lang.code,
                                    child: Text(lang.name, style: const TextStyle(fontSize: 14)),
                                  ))
                              .toList(),
                          onChanged: (value) {
                            if (value != null) setState(() => _selectedLanguage = value);
                          },
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                SizedBox(
                  height: 44,
                  child: ElevatedButton.icon(
                    onPressed: _descriptionController.text.trim().isNotEmpty && !_isLoadingAi
                        ? _getAiAssistance
                        : null,
                    icon: _isLoadingAi
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                          )
                        : const Icon(Icons.translate, size: 18),
                    label: Text(_isLoadingAi ? 'Getting help...' : 'Get Help'),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppTheme.secondaryColor,
                    ),
                  ),
                ),
                if (_aiError != null) ...[
                  const SizedBox(height: 12),
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: AppTheme.warningColor.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.info_outline, color: AppTheme.warningColor, size: 18),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            _aiError!,
                            style: const TextStyle(fontSize: 13, color: AppTheme.warningColor),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
                if (_aiResult != null) ...[
                  const SizedBox(height: 16),
                  _buildAiResultCard(),
                ],
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),
        Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: AppTheme.warningColor.withValues(alpha: 0.08),
            borderRadius: BorderRadius.circular(8),
          ),
          child: const Row(
            children: [
              Icon(Icons.warning_amber, color: AppTheme.warningColor, size: 18),
              SizedBox(width: 8),
              Expanded(
                child: Text(
                  'AI assistance is optional. Always call 999 first in a real emergency.',
                  style: TextStyle(fontSize: 12, color: AppTheme.warningColor),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildAiResultCard() {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppTheme.secondaryColor.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: AppTheme.secondaryColor.withValues(alpha: 0.2)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'AI Guidance',
            style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: AppTheme.secondaryColor),
          ),
          const SizedBox(height: 8),
          SelectableText(
            _aiResult!.response,
            style: const TextStyle(fontSize: 14, height: 1.5),
          ),
          if (_aiResult!.translatedMessage != null) ...[
            const SizedBox(height: 14),
            const Divider(height: 1),
            const SizedBox(height: 14),
            Text(
              'Translation (${_getLanguageName(_aiResult!.targetLanguage ?? _selectedLanguage)})',
              style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: AppTheme.secondaryColor),
            ),
            const SizedBox(height: 8),
            SelectableText(
              _aiResult!.translatedMessage!,
              style: const TextStyle(fontSize: 14, height: 1.5),
            ),
          ],
        ],
      ),
    );
  }

  String _getLanguageName(String code) {
    for (final lang in _targetLanguages) {
      if (lang.code == code) return lang.name;
    }
    return code;
  }
}

class _QuickActionCard extends StatelessWidget {
  final IconData icon;
  final String label;
  final VoidCallback onTap;

  const _QuickActionCard({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Card(
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 16, horizontal: 8),
          child: Column(
            children: [
              Icon(icon, color: AppTheme.errorColor, size: 28),
              const SizedBox(height: 8),
              Text(
                label,
                style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w500),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _LangOption {
  final String code;
  final String name;
  const _LangOption({required this.code, required this.name});
}
