import 'package:flutter/material.dart';
import '../../core/services/family_service.dart';

class FamilyAlertsPanel extends StatefulWidget {
  final FamilyService service;
  final Future<void> Function(String)? onOpenDocument;
  const FamilyAlertsPanel({super.key, required this.service, this.onOpenDocument});
  @override
  State<FamilyAlertsPanel> createState() => _FamilyAlertsPanelState();
}
class _FamilyAlertsPanelState extends State<FamilyAlertsPanel> with WidgetsBindingObserver {
  List<FamilyAlert> _alerts = [];
  final Set<String> _busy = {};
  bool _loading = true;
  String? _error;
  int _loadVersion = 0;
  @override
  void initState() { super.initState(); WidgetsBinding.instance.addObserver(this); _load(); }
  @override
  void dispose() { WidgetsBinding.instance.removeObserver(this); super.dispose(); }
  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) _load();
  }
  Future<void> _load() async {
    final version = ++_loadVersion;
    setState(() { _loading = true; _alerts = []; _error = null; });
    try {
      final alerts = await widget.service.alerts();
      if (mounted && version == _loadVersion) setState(() { _alerts = alerts; _loading = false; });
    } catch (e) {
      if (mounted && version == _loadVersion) setState(() { _loading = false; _error = 'Unable to load family alerts. $e'; });
    }
  }
  Future<void> _dismiss(FamilyAlert alert) async {
    setState(() => _busy.add(alert.id));
    try { await widget.service.dismissAlert(alert.id); if (mounted) await _load(); }
    catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Unable to dismiss alert. $e')));
    } finally { if (mounted) setState(() => _busy.remove(alert.id)); }
  }
  @override
  Widget build(BuildContext context) => Column(children: [
    Padding(padding: const EdgeInsets.fromLTRB(16, 8, 8, 0), child: Row(children: [
      const Expanded(child: Text('In-app alerts • refresh to check for updates')),
      IconButton(tooltip: 'Refresh alerts', onPressed: _loading ? null : _load, icon: const Icon(Icons.refresh)),
    ])),
    Expanded(child: _loading ? const Center(child: CircularProgressIndicator()) : RefreshIndicator(
      onRefresh: _load,
      child: ListView(physics: const AlwaysScrollableScrollPhysics(), padding: const EdgeInsets.all(16), children: [
        if (_error != null) ...[Text(_error!), TextButton(onPressed: _load, child: const Text('Retry'))]
        else if (_alerts.isEmpty) const Text('No new family alerts. Expiry alerts need an owner-confirmed date and, for shared documents, the owner’s reminder permission.'),
        ..._alerts.map((alert) => Card(child: Padding(padding: const EdgeInsets.all(16), child: Column(
          crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(alert.title, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8), Text(alert.message),
            if (alert.expiryDate != null) Text('Expiry: ${alert.expiryDate!.toIso8601String().substring(0, 10)}'),
            if (alert.kind == 'request') const Text('Manage this request in More → Family.'),
            Wrap(spacing: 12, children: [
              if (alert.documentId != null && widget.onOpenDocument != null)
                TextButton(onPressed: () => widget.onOpenDocument!(alert.documentId!), child: const Text('View document')),
              TextButton(onPressed: _busy.contains(alert.id) ? null : () => _dismiss(alert), child: const Text('Dismiss')),
            ]),
          ],
        )))),
      ]),
    )),
  ]);
}
