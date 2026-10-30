import 'dart:async';

import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../core/services/destination_service.dart';

class DestinationBanner extends StatefulWidget {
  final DestinationService service;
  final String greeting;
  final String? userId, preferredCountry;
  final int completedSteps, totalSteps;
  final List<String> checklistPreview;
  final List<bool> checklistCompleted;
  final VoidCallback? onViewChecklist;
  const DestinationBanner({
    super.key,
    required this.service,
    required this.greeting,
    this.userId,
    this.preferredCountry,
    this.completedSteps = 0,
    this.totalSteps = 0,
    this.checklistPreview = const [],
    this.checklistCompleted = const [],
    this.onViewChecklist,
  });
  @override
  State<DestinationBanner> createState() => _DestinationBannerState();
}

class _DestinationBannerState extends State<DestinationBanner>
    with WidgetsBindingObserver {
  List<Destination> _countries = [];
  Destination? _selected;
  DestinationWeather? _weather;
  bool _loading = true, _weatherLoading = false;
  String? _error;
  int _request = 0;
  Timer? _timer;
  Future<void> _saveQueue = Future.value();
  String? get _preferenceKey =>
      widget.userId == null ? null : 'expatone.destination.${widget.userId}';

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _load();
    _timer = Timer.periodic(const Duration(minutes: 15), (_) {
      if (mounted &&
          WidgetsBinding.instance.lifecycleState == AppLifecycleState.resumed) {
        _refresh();
      }
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _request++;
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) _refresh();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final countries = await widget.service.getDestinations();
      String? saved;
      try {
        if (_preferenceKey != null) {
          saved = (await SharedPreferences.getInstance()).getString(
            _preferenceKey!,
          );
        }
      } catch (_) {
        /* Selection remains usable if local storage is blocked. */
      }
      if (!mounted) return;
      Destination? selected;
      for (final code in [saved, widget.preferredCountry, 'MY']) {
        for (final country in countries) {
          if (country.code == code) {
            selected = country;
            break;
          }
        }
        if (selected != null) break;
      }
      selected ??= countries.isEmpty ? null : countries.first;
      setState(() {
        _countries = countries;
        _selected = selected;
        _loading = false;
      });
      await _refresh();
    } catch (_) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = 'Unable to load destinations';
        });
      }
    }
  }

  Future<void> _select(String? code) async {
    if (code == null || code == _selected?.code) return;
    setState(() {
      _selected = _countries.firstWhere((c) => c.code == code);
      _weather = null;
    });
    final key = _preferenceKey;
    if (key != null) {
      // Serialize writes so rapid selections cannot persist out of order.
      _saveQueue = _saveQueue.then((_) async {
        try {
          await (await SharedPreferences.getInstance()).setString(key, code);
        } catch (_) {}
      });
    }
    await _refresh();
  }

  Future<void> _refresh() async {
    final code = _selected?.code;
    if (code == null || !mounted) return;
    final request = ++_request;
    setState(() {
      _weatherLoading = true;
      _weather = null;
    });
    DestinationWeather? result;
    try {
      result = await widget.service.getWeather(code);
    } catch (_) {}
    if (!mounted || request != _request || code != _selected?.code) return;
    setState(() {
      _weather = result?.countryCode == code ? result : null;
      _weatherLoading = false;
    });
  }

  IconData _weatherIcon() {
    final code = _weather?.weatherCode;
    if (code == 0) {
      return _weather?.isDay == false
          ? Icons.nightlight_round
          : Icons.wb_sunny_rounded;
    }
    if (code != null && code >= 95) return Icons.thunderstorm;
    if (code != null &&
        ((code >= 71 && code <= 77) || code == 85 || code == 86)) {
      return Icons.ac_unit;
    }
    if (code != null && code >= 51) return Icons.water_drop;
    if (code == 45 || code == 48) return Icons.foggy;
    return Icons.cloud;
  }

  Widget _fallback() => Container(
    decoration: const BoxDecoration(
      gradient: LinearGradient(colors: [Color(0xFFB9E6FF), Color(0xFFE9F8FF)]),
    ),
    child: const Align(
      alignment: Alignment.bottomLeft,
      child: Padding(
        padding: EdgeInsets.all(24),
        child: Icon(Icons.public, size: 120, color: Color(0xFF86C9E9)),
      ),
    ),
  );

  Widget _image() {
    final code = _selected?.code;
    final url = _selected?.bannerImageUrl;
    if (url != null && Uri.tryParse(url)?.scheme == 'https') {
      return Image.network(
        url,
        fit: BoxFit.cover,
        alignment: Alignment.centerLeft,
        errorBuilder: (_, error, stack) => _asset(code),
      );
    }
    return _asset(code);
  }

  Widget _asset(String? code) {
    const supported = {
      'AE',
      'BH',
      'CN',
      'ID',
      'IN',
      'KW',
      'MY',
      'OM',
      'QA',
      'SA',
      'SG',
    };
    if (!supported.contains(code)) return _fallback();
    return Image.asset(
      'assets/destinations/$code.png',
      fit: BoxFit.cover,
      alignment: Alignment.centerLeft,
      errorBuilder: (_, error, stack) => _fallback(),
    );
  }

  @override
  Widget build(BuildContext context) => ClipRRect(
    borderRadius: BorderRadius.circular(22),
    child: Stack(
      children: [
        Positioned.fill(
          child: Semantics(
            label: _selected?.landmark ?? 'Destination landscape',
            image: true,
            child: _image(),
          ),
        ),
        Positioned.fill(
          child: DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: [
                  Colors.white.withValues(alpha: .78),
                  Colors.white.withValues(alpha: .12),
                  const Color(0xFF052951).withValues(alpha: .65),
                ],
                stops: const [0, .48, 1],
              ),
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Align(
                alignment: Alignment.topRight,
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 360),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        widget.greeting,
                        style: const TextStyle(
                          color: Color(0xFF082457),
                          fontSize: 25,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      const SizedBox(height: 6),
                      const Text(
                        'Your expat journey, made simpler.',
                        style: TextStyle(color: Color(0xFF234E78)),
                      ),
                    ],
                  ),
                ),
              ),
              if (widget.totalSteps > 0)
                Align(
                  alignment: Alignment.centerRight,
                  child: Container(
                    constraints: const BoxConstraints(maxWidth: 310),
                    padding: const EdgeInsets.all(14),
                    decoration: BoxDecoration(
                      color: const Color(0xFF0D244B).withValues(alpha: .86),
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Text('Your journey progress',
                          style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                        const SizedBox(height: 8),
                        Row(children: [
                          SizedBox(
                            width: 64, height: 64,
                            child: Stack(alignment: Alignment.center, children: [
                              SizedBox.expand(child: CircularProgressIndicator(
                                value: widget.completedSteps / widget.totalSteps,
                                strokeWidth: 6,
                                color: const Color(0xFF18C788),
                                backgroundColor: Colors.white24,
                              )),
                              Text('${widget.completedSteps}/${widget.totalSteps}',
                                style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
                            ]),
                          ),
                          const SizedBox(width: 12),
                          const Expanded(child: Text('Your relocation checklist',
                            style: TextStyle(color: Colors.white, fontSize: 13))),
                        ]),
                        for (var index = 0; index < widget.checklistPreview.length && index < 4; index++)
                          Padding(
                            padding: const EdgeInsets.only(top: 7),
                            child: Row(children: [
                              Icon(index < widget.checklistCompleted.length && widget.checklistCompleted[index]
                                ? Icons.check_circle : Icons.radio_button_unchecked,
                                color: index < widget.checklistCompleted.length && widget.checklistCompleted[index]
                                  ? const Color(0xFF18C788) : Colors.white70, size: 16),
                              const SizedBox(width: 7),
                              Expanded(child: Text(widget.checklistPreview[index],
                                style: const TextStyle(color: Colors.white, fontSize: 12),
                                maxLines: 1, overflow: TextOverflow.ellipsis)),
                            ]),
                          ),
                        if (widget.onViewChecklist != null)
                          TextButton(
                            onPressed: widget.onViewChecklist,
                            child: const Text('View full checklist',
                              style: TextStyle(color: Colors.white, decoration: TextDecoration.underline)),
                          ),
                      ],
                    ),
                  ),
                ),
              const SizedBox(height: 20),
              if (_loading)
                const LinearProgressIndicator()
              else if (_error != null)
                _pill(
                  Row(
                    children: [
                      Expanded(child: Text(_error!)),
                      TextButton(onPressed: _load, child: const Text('Retry')),
                    ],
                  ),
                )
              else if (_countries.isEmpty)
                _pill(const Text('No active destinations available'))
              else
                LayoutBuilder(
                  builder: (context, size) {
                    final selector = _pill(
                      Row(
                        children: [
                          const Icon(
                            Icons.location_on,
                            color: Color(0xFF007BFF),
                            size: 20,
                          ),
                          const SizedBox(width: 5),
                          Expanded(
                            child: DropdownButtonHideUnderline(
                              child: DropdownButton<String>(
                                key: const Key('destination-selector'),
                                value: _selected?.code,
                                isExpanded: true,
                                dropdownColor: Colors.white,
                                borderRadius: BorderRadius.circular(16),
                                onChanged: _select,
                                hint: const Text('Destination'),
                                items: _countries
                                    .map(
                                      (c) => DropdownMenuItem(
                                        value: c.code,
                                        child: Text(
                                          c.name,
                                          overflow: TextOverflow.ellipsis,
                                        ),
                                      ),
                                    )
                                    .toList(),
                              ),
                            ),
                          ),
                        ],
                      ),
                    );
                    final weather = _pill(
                      InkWell(
                        onTap: _refresh,
                        borderRadius: BorderRadius.circular(14),
                        child: Padding(
                          padding: const EdgeInsets.symmetric(vertical: 8),
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  if (_weatherLoading)
                                    const SizedBox(
                                      width: 16,
                                      height: 16,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                      ),
                                    )
                                  else
                                    Icon(
                                      _weather?.available == true
                                          ? _weatherIcon()
                                          : Icons.cloud_off,
                                      size: 22,
                                      color: const Color(0xFFE8A320),
                                    ),
                                  const SizedBox(width: 7),
                                  Flexible(
                                    child: Text(
                                      _weatherLoading
                                          ? 'Loading weather'
                                          : _weather?.available == true &&
                                                _weather?.temperatureC != null
                                          ? '${_weather!.temperatureC!.round()}°C'
                                          : 'Weather unavailable',
                                      style: const TextStyle(
                                        fontWeight: FontWeight.w700,
                                        color: Color(0xFF082457),
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                              if (_selected?.weatherCity != null)
                                Text(
                                  _selected!.weatherCity!,
                                  style: const TextStyle(
                                    fontSize: 11,
                                    color: Color(0xFF466783),
                                  ),
                                ),
                            ],
                          ),
                        ),
                      ),
                    );
                    if (size.maxWidth < 360) {
                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          selector,
                          const SizedBox(height: 8),
                          weather,
                        ],
                      );
                    }
                    return Row(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Expanded(flex: 3, child: selector),
                        const SizedBox(width: 12),
                        Expanded(flex: 2, child: weather),
                      ],
                    );
                  },
                ),
              const SizedBox(height: 4),
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(
                  style: TextButton.styleFrom(
                    foregroundColor: Colors.white,
                    minimumSize: const Size(48, 40),
                  ),
                  onPressed: () =>
                      launchUrl(Uri.parse('https://open-meteo.com/')),
                  child: Text(
                    'Weather by Open-Meteo${_weather?.observedAt == null ? '' : ' · ${TimeOfDay.fromDateTime(_weather!.observedAt!.toLocal()).format(context)} local'}',
                    style: const TextStyle(fontSize: 10),
                  ),
                ),
              ),
            ],
          ),
        ),
      ],
    ),
  );
  Widget _pill(Widget child) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 12),
    decoration: BoxDecoration(
      color: Colors.white.withValues(alpha: .95),
      borderRadius: BorderRadius.circular(14),
    ),
    child: child,
  );
}
