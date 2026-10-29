import 'package:flutter/material.dart';

import '../../core/services/document_service.dart';
import '../../core/services/family_service.dart';
import 'family_occasions_screen.dart';
import '../documents/document_detail_screen.dart';
import 'family_alerts_panel.dart';
import '../../shared/widgets/profile_avatar.dart';

class FamilyDashboardScreen extends StatefulWidget {
  final DocumentService documentService;
  const FamilyDashboardScreen({super.key, required this.documentService});
  @override
  State<FamilyDashboardScreen> createState() => _FamilyDashboardScreenState();
}

class _FamilyDashboardScreenState extends State<FamilyDashboardScreen>
    with SingleTickerProviderStateMixin, WidgetsBindingObserver {
  late final TabController _tabs;
  List<FamilyDashboardDocument> _documents = [];
  List<FamilyRequest> _connections = [];
  final Map<String, String?> _memberPhotos = {};
  List<Map<String, dynamic>> _occasions = [];
  bool _loading = true;
  String? _error;
  String _filter = 'All';
  int _loadVersion = 0, _alertsVersion = 0;
  final Set<String> _busy = {};
  FamilyService get _service => widget.documentService.familyService;
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _tabs = TabController(length: 2, vsync: this)..addListener(_tabChanged);
    _load();
    _loadOverview();
  }

  Future<void> _loadMemberPhotos() async {
    final members = _connections.where((r) => r.status == 'Accepted' && !r.isExpired)
        .map((r) => r.isIncoming ? r.requester : r.recipient);
    for (final member in members) {
      if (_memberPhotos.containsKey(member.id)) continue;
      final url = await _service.memberPhotoUrl(member.id);
      if (!mounted) return;
      setState(() => _memberPhotos[member.id] = url);
    }
  }

  Future<void> _loadOverview() async {
    try {
      final connections = await _service.requests();
      final occasions = await widget.documentService.apiClient.getList(
        '/family-occasions',
      );
      if (mounted)
        setState(() {
          _connections = connections;
          _memberPhotos.clear();
          _occasions = occasions;
        });
      _loadMemberPhotos();
    } catch (_) {
      // Keep the document dashboard usable if companion feeds are unavailable.
    }
  }

  void _tabChanged() {
    if (_tabs.indexIsChanging) return;
    if (_tabs.index == 0) {
      _load();
    } else {
      setState(() => _alertsVersion++);
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _tabs.removeListener(_tabChanged);
    _tabs.dispose();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _load();
      _loadOverview();
    }
  }

  Future<void> _load() async {
    final version = ++_loadVersion;
    setState(() {
      _loading = true;
      _documents = [];
      _error = null;
    });
    try {
      final documents = await _service.dashboard();
      if (mounted && version == _loadVersion)
        setState(() {
          _documents = documents;
          _loading = false;
        });
    } catch (e) {
      if (mounted && version == _loadVersion)
        setState(() {
          _loading = false;
          _error = 'Unable to load dashboard. $e';
        });
    }
  }

  Future<void> _open(String id) async {
    if (_busy.contains(id)) return;
    setState(() => _busy.add(id));
    try {
      // Recheck current document permission before displaying detail.
      final document = await widget.documentService.getDocument(id);
      if (!mounted) return;
      await Navigator.push<void>(
        context,
        MaterialPageRoute(
          builder: (_) => DocumentDetailScreen(
            document: document,
            documentService: widget.documentService,
          ),
        ),
      );
    } catch (e) {
      if (mounted)
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text('Unable to open document. $e')));
    } finally {
      if (mounted) {
        setState(() {
          _busy.remove(id);
          _alertsVersion++;
        });
        await _load();
      }
    }
  }

  Future<void> _confirm(FamilyDashboardDocument document) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Confirm expiry date'),
        content: Text(
          'Check the original document first. Is ${_date(document.expiryDate!)} the correct expiry date for ${document.name}? This enables in-app expiry alerts.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Confirm date'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    setState(() => _busy.add(document.id));
    try {
      await _service.confirmExpiry(document);
      if (mounted) {
        setState(() => _alertsVersion++);
        await _load();
      }
    } catch (e) {
      if (mounted)
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text('Unable to confirm date. $e')));
    } finally {
      if (mounted) setState(() => _busy.remove(document.id));
    }
  }

  String _date(DateTime date) => date.toIso8601String().substring(0, 10);
  Color _statusColor(String status) => switch (status) {
    'Expired' => Colors.red.shade700,
    'Due soon' => Colors.orange.shade900,
    'Valid' => Colors.green.shade700,
    _ => Colors.grey.shade700,
  };
  Widget _summaryCard(
    BuildContext context,
    String label,
    int count,
    IconData icon,
  ) {
    return SizedBox(
      width: double.infinity,
      child: Card(
        color: const Color(0xFFEAF2FF),
        elevation: 0,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(icon, color: Theme.of(context).colorScheme.primary),
              const SizedBox(height: 8),
              Text(
                '$count',
                style: Theme.of(context).textTheme.headlineMedium
                    ?.copyWith(fontWeight: FontWeight.bold),
              ),
              Text(label, style: Theme.of(context).textTheme.bodySmall),
            ],
          ),
        ),
      ),
    );
  }

  int _daysToOccasion(Map<String, dynamic> item) {
    final dateString = item['occasionDate'] as String?;
    if (dateString == null) return 999999;
    final original = DateTime.tryParse(dateString);
    if (original == null) return 999999;
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    var next = DateTime(original.year, original.month, original.day);
    if (item['repeatsAnnually'] == true) {
      next = DateTime(now.year, original.month, original.day);
      if (next.isBefore(today))
        next = DateTime(now.year + 1, original.month, original.day);
    }
    return next.difference(today).inDays;
  }

  List<Map<String, dynamic>> _upcomingOccasions() {
    final result = _occasions.where((item) {
      final days = _daysToOccasion(item);
      return days >= 0 && days <= 30;
    }).toList();
    result.sort((a, b) => _daysToOccasion(a).compareTo(_daysToOccasion(b)));
    return result;
  }

  Widget _dashboard() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    final groups = <String, List<FamilyDashboardDocument>>{};
    for (final document in _documents.where((d) => d.matches(_filter))) {
      groups.putIfAbsent(document.owner.id, () => []).add(document);
    }
    final members = groups.values.toList()
      ..sort((a, b) {
        if (a.first.isOwned != b.first.isOwned) return a.first.isOwned ? -1 : 1;
        return a.first.owner.email.compareTo(b.first.owner.email);
      });
    return RefreshIndicator(
      onRefresh: () async {
        await _load();
        await _loadOverview();
      },
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.all(16),
        children: [
          if (_error != null) ...[
            Text(_error!),
            TextButton(onPressed: _load, child: const Text('Retry')),
          ] else ...[
            Row(
              children: [
                const Icon(
                  Icons.people_alt_outlined,
                  color: Color(0xFF2563EB),
                  size: 26,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Friends & Family Dashboard',
                        style: Theme.of(context).textTheme.titleLarge
                            ?.copyWith(fontWeight: FontWeight.bold),
                      ),
                      const Text('Overview of your trusted circle'),
                    ],
                  ),
                ),
                FilledButton.icon(
                  onPressed: () => Navigator.push(
                    context,
                    MaterialPageRoute(
                      builder: (_) => FamilyOccasionsScreen(
                        api: widget.documentService.apiClient,
                      ),
                    ),
                  ),
                  icon: const Icon(Icons.event_outlined),
                  label: const Text('Occasions'),
                ),
              ],
            ),
            const SizedBox(height: 16),
            LayoutBuilder(builder: (context, constraints) {
              final columns = constraints.maxWidth >= 880 ? 4 : constraints.maxWidth >= 480 ? 2 : 1;
              return GridView.count(
                crossAxisCount: columns,
                crossAxisSpacing: 12,
                mainAxisSpacing: 12,
                childAspectRatio: columns == 1 ? 4.2 : columns == 2 ? 2.6 : 1.75,
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                children: [
                _summaryCard(
                  context,
                  'Connections',
                  _connections
                      .where((r) => r.status == 'Accepted' && !r.isExpired)
                      .length,
                  Icons.people_outline,
                ),
                _summaryCard(
                  context,
                  'Shared Documents',
                  _documents.where((d) => !d.isOwned).length,
                  Icons.folder_shared_outlined,
                ),
                _summaryCard(
                  context,
                  'Pending Requests',
                  _connections.where((r) => r.status == 'Pending').length,
                  Icons.schedule_outlined,
                ),
                _summaryCard(
                  context,
                  'Upcoming Events',
                  _upcomingOccasions().length,
                  Icons.event_available_outlined,
                ),
              ],
              );
            }),
            const SizedBox(height: 16),
            LayoutBuilder(
              builder: (context, layout) => Wrap(
                spacing: 12,
                runSpacing: 12,
                children: [
                  SizedBox(
                    width: layout.maxWidth > 700
                        ? (layout.maxWidth - 12) / 2
                        : layout.maxWidth,
                    child: Card(
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                const Icon(
                                  Icons.cake_outlined,
                                  color: Color(0xFFE11D48),
                                ),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: Text(
                                    'Upcoming Celebrations',
                                    style: Theme.of(context)
                                        .textTheme
                                        .titleMedium,
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 8),
                            if (_upcomingOccasions().isEmpty)
                              const Text(
                                'No upcoming occasions in the next 30 days',
                              ),
                            for (final item in _upcomingOccasions().take(4))
                              ListTile(
                                dense: true,
                                leading: ProfileAvatar(
                                  imageUrl: item['profilePhotoUrl'] as String?,
                                  displayName: item['personName'] as String? ?? item['title'] as String?,
                                  radius: 20),
                                title: Text(
                                  item['title'] as String? ?? 'Occasion',
                                ),
                                subtitle: Text(
                                  item['kind'] as String? ?? 'Event',
                                ),
                                trailing: Text(
                                  _daysToOccasion(item).toString() + ' days',
                                ),
                              ),
                          ],
                        ),
                      ),
                    ),
                  ),
                  SizedBox(
                    width: layout.maxWidth > 700
                        ? (layout.maxWidth - 12) / 2
                        : layout.maxWidth,
                    child: Card(
                      child: Padding(
                        padding: const EdgeInsets.all(16),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                const Icon(
                                  Icons.history_outlined,
                                  color: Color(0xFF2563EB),
                                ),
                                const SizedBox(width: 8),
                                Expanded(
                                  child: Text(
                                    'Recent Connection Activity',
                                    style: Theme.of(context)
                                        .textTheme
                                        .titleMedium,
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 8),
                            if (_connections.isEmpty)
                              const Text('No connection activity'),
                            for (final r in _connections.take(4))
                              ListTile(
                                dense: true,
                                leading: ProfileAvatar(
                                  imageUrl: _memberPhotos[(r.isIncoming ? r.requester : r.recipient).id],
                                  displayName: (r.isIncoming ? r.requester : r.recipient).displayName,
                                  email: (r.isIncoming ? r.requester : r.recipient).email,
                                  radius: 20,
                                ),
                                title: Text(
                                  r.isIncoming
                                      ? (r.requester.displayName ??
                                            r.requester.email)
                                      : (r.recipient.displayName ??
                                            r.recipient.email),
                                ),
                                subtitle: Text(
                                  '${r.relationship} • ${r.status}',
                                ),
                              ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 16),
            const SizedBox(height: 16),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final status in [
                  'Expired',
                  'Due soon',
                  'Valid',
                  'No expiry date',
                ])
                  Chip(
                    label: Text(
                      '$status: ${_documents.where((d) => d.expiryStatus == status).length}',
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 8),
            const Text(
              'Your trusted circle: shared documents and expiry alerts. Due soon means today through 90 days; dates use UTC calendar days.',
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              children: [
                for (final filter in [
                  'All',
                  'Expired',
                  'Next 30 days',
                  'Next 90 days',
                  'Valid',
                  'No expiry date',
                ])
                  ChoiceChip(
                    label: Text(filter),
                    selected: filter == _filter,
                    onSelected: (_) => setState(() => _filter = filter),
                  ),
              ],
            ),
            const SizedBox(height: 16),
            if (members.isEmpty) const Text('No documents match this filter.'),
            for (final documents in members) ...[
              Text(
                documents.first.isOwned
                    ? 'My documents'
                    : documents.first.owner.displayName ??
                          documents.first.owner.email,
                style: Theme.of(context).textTheme.titleLarge,
              ),
              Text(documents.first.owner.email),
              ...documents.map(
                (d) => Card(
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          d.name,
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                        Text(d.documentType),
                        Text(
                          d.expiryStatus,
                          style: TextStyle(
                            color: _statusColor(d.expiryStatus),
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        if (d.expiryDate != null) ...[
                          Text('Expiry: ${_date(d.expiryDate!)}'),
                          Text(
                            d.expiryConfirmed ? 'Date confirmed by owner' : 'Awaiting owner confirmation • expiry alerts paused',
                          ),
                          if (!d.isOwned)
                            Text(
                              d.remindersAllowed
                                  ? 'Owner allows shared expiry alerts'
                                  : 'Owner has not enabled shared expiry alerts',
                            ),
                        ],
                        Wrap(
                          spacing: 8,
                          children: [
                            TextButton(
                              onPressed: _busy.contains(d.id)
                                  ? null
                                  : () => _open(d.id),
                              child: const Text('View document'),
                            ),
                            if (d.isOwned &&
                                d.expiryDate != null &&
                                !d.expiryConfirmed)
                              TextButton(
                                onPressed: _busy.contains(d.id)
                                    ? null
                                    : () => _confirm(d),
                                child: const Text('Confirm expiry date'),
                              ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              const SizedBox(height: 16),
            ],
          ],
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: const Text('Friends & Family dashboard'),
      actions: [
        IconButton(
          tooltip: 'Refresh dashboard',
          onPressed: () {
            _load();
            _loadOverview();
            setState(() => _alertsVersion++);
          },
          icon: const Icon(Icons.refresh),
        ),
      ],
      bottom: TabBar(
        controller: _tabs,
        tabs: const [
          Tab(text: 'Documents'),
          Tab(text: 'Alerts'),
        ],
      ),
    ),
    body: TabBarView(
      controller: _tabs,
      children: [
        _dashboard(),
        FamilyAlertsPanel(
          key: ValueKey(_alertsVersion),
          service: _service,
          onOpenDocument: _open,
        ),
      ],
    ),
  );
}
