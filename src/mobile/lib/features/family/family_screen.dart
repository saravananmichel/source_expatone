import 'package:flutter/material.dart';
import 'family_dashboard_screen.dart';
import '../../shared/widgets/profile_avatar.dart';
import 'family_occasions_screen.dart';
import '../../core/services/document_service.dart';
import '../../core/services/family_service.dart';

class FamilyScreen extends StatefulWidget {
  final DocumentService documentService;
  const FamilyScreen({super.key, required this.documentService});
  @override
  State<FamilyScreen> createState() => _FamilyScreenState();
}
class _FamilyScreenState extends State<FamilyScreen> {
  final _email = TextEditingController();
  final _connectionSearch = TextEditingController();
  String _statusFilter = 'All';
  List<FamilyRequest> _requests = [];
  bool _loading = true, _busy = false;
  String? _error;
  FamilyService get _service => widget.documentService.familyService;
  @override
  void initState() { super.initState(); _load(); }
  @override
  void dispose() { _email.dispose(); _connectionSearch.dispose(); super.dispose(); }
  Future<void> _load() async {
    try {
      final requests = await _service.requests();
      if (mounted) setState(() { _requests = requests; _loading = false; _error = null; });
    } catch (e) {
      if (mounted) setState(() { _loading = false; _error = 'Unable to load requests. $e'; });
    }
  }
  Future<void> _run(Future<void> Function() action, {String? success}) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      await action();
      if (success != null && mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(success)));
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }
  Future<void> _choose(FamilyRequest request) async {
    await showDialog<bool>(context: context, builder: (ctx) => Dialog(
      clipBehavior: Clip.antiAlias,
      child: SizedBox(
        width: MediaQuery.sizeOf(ctx).width > 700 ? 620 : MediaQuery.sizeOf(ctx).width * .95,
        height: MediaQuery.sizeOf(ctx).height * .85,
        child: _GrantScreen(documentService: widget.documentService, request: request),
      ),
    ));
    if (mounted) await _load();
  }
  Future<void> _end(FamilyRequest r) async {
    final ok = await showDialog<bool>(context: context, builder: (context) => AlertDialog(
      title: Text(r.status == 'Pending' ? 'Cancel request?' : 'Revoke family access?'),
      content: const Text('This request will no longer provide document access.'),
      actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Keep')),
        TextButton(onPressed: () => Navigator.pop(context, true), child: const Text('Confirm'))],
    ));
    if (ok == true && mounted) await _run(() async { await _service.end(r); await _load(); });
  }
  Widget _metric(String label, int count, IconData icon, Color bg, Color fg) =>
    Container(width: 166, padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(17)),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Icon(icon, color: fg, size: 26),
        const SizedBox(height: 12),
        Text('$count', style: const TextStyle(fontSize: 25, fontWeight: FontWeight.bold)),
        Text(label, style: const TextStyle(fontSize: 12)),
      ]));

  Future<void> _inviteDialog() async {
    final email = TextEditingController();
    final message = TextEditingController();
    FamilyUser? found;
    String relationship = 'Friend';
    int step = 0;
    bool searching = false;
    String? error;
    final result = await showDialog<(String, String)>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, update) => AlertDialog(
          title: Row(children: [
            const Expanded(child: Text('Add a Connection')),
            IconButton(icon: const Icon(Icons.close), tooltip: 'Close',
              onPressed: () => Navigator.pop(context)),
          ]),
          content: SizedBox(width: 460, child: SingleChildScrollView(
            child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
              Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
                for (var i = 0; i < 3; i++)
                  Expanded(child: Column(children: [
                    CircleAvatar(radius: 14, backgroundColor: i <= step ? const Color(0xFF1264EF) : const Color(0xFFE8EEF8),
                      child: Text('${i + 1}', style: TextStyle(color: i <= step ? Colors.white : Colors.blueGrey, fontSize: 12))),
                    const SizedBox(height: 4),
                    Text(['Search User', 'Relationship', 'Send Invite'][i], style: const TextStyle(fontSize: 11)),
                  ])),
              ]),
              const SizedBox(height: 20),
              if (step == 0) ...[
                const Text('Find a registered ExpatOne user'),
                const SizedBox(height: 8),
                TextField(controller: email, keyboardType: TextInputType.emailAddress,
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search), hintText: 'Enter full registered email',
                    border: OutlineInputBorder())),
                const SizedBox(height: 8),
                const Text('For privacy, searches require the complete registered email.',
                  style: TextStyle(fontSize: 12, color: Colors.blueGrey)),
                if (searching) const LinearProgressIndicator(),
                if (found != null) Card(child: ListTile(
                  leading: ProfileAvatar(imageUrl: found!.profilePhotoUrl, displayName: found!.displayName, email: found!.email),
                  title: Text(found!.displayName ?? found!.email),
                  subtitle: Text(found!.email),
                  trailing: const Icon(Icons.check_circle, color: Colors.green),
                )),
              ],
              if (step == 1) ...[
                Text('Connecting with ${found?.displayName ?? found?.email ?? email.text}',
                  style: const TextStyle(fontWeight: FontWeight.bold)),
                const SizedBox(height: 12),
                const Text('Select relationship'),
                const SizedBox(height: 8),
                ...['Spouse', 'Daughter', 'Son', 'Father', 'Mother', 'Brother', 'Sister', 'Friend', 'Colleague', 'Guardian', 'Other']
                  .map((r) => RadioListTile<String>(
                    dense: true, title: Text(r), value: r, groupValue: relationship,
                    onChanged: (value) => update(() => relationship = value ?? relationship))),
              ],
              if (step == 2) ...[
                ListTile(leading: const Icon(Icons.person_outline),
                  title: Text(found?.displayName ?? email.text), subtitle: Text(email.text)),
                ListTile(leading: const Icon(Icons.favorite_outline),
                  title: Text(relationship), subtitle: const Text('Relationship')),
                TextField(controller: message, maxLines: 3,
                  decoration: const InputDecoration(labelText: 'Personal message (optional)',
                    hintText: 'Message support requires an API update',
                    border: OutlineInputBorder())),
                const SizedBox(height: 6),
                const Text('Personal messages are not sent yet; the current invitation API only stores email and relationship.',
                  style: TextStyle(fontSize: 12, color: Colors.blueGrey)),
              ],
              if (error != null) Padding(padding: const EdgeInsets.only(top: 12),
                child: Text(error!, style: const TextStyle(color: Colors.red))),
            ]),
          )),
          actions: [
            TextButton(onPressed: () {
              if (step == 0) Navigator.pop(context);
              else update(() { step--; error = null; });
            }, child: Text(step == 0 ? 'Cancel' : 'Back')),
            FilledButton(
              onPressed: searching ? null : () async {
                if (step == 0) {
                  if (!email.text.trim().contains('@')) {
                    update(() => error = 'Enter a registered email.'); return;
                  }
                  update(() { searching = true; error = null; found = null; });
                  try {
                    final user = await _service.search(email.text.trim());
                    if (!context.mounted) return;
                    update(() { found = user; step = 1; searching = false; });
                  } catch (_) {
                    if (context.mounted) update(() { searching = false; error = 'User not found or search unavailable.'; });
                  }
                } else if (step == 1) {
                  update(() { step = 2; error = null; });
                } else {
                  Navigator.pop(context, (email.text.trim(), relationship));
                }
              },
              child: Text(step == 2 ? 'Send Invite' : 'Next'),
            ),
          ],
        ),
      ),
    );
    email.dispose();
    message.dispose();
    if (result == null || !mounted) return;
    await _run(() async {
      await _service.send(result.$1, relationship: result.$2);
      await _load();
    }, success: 'Invitation sent');
  }

  Widget _shortcut(String title, String subtitle, IconData icon, VoidCallback onTap) =>
    Card(child: ListTile(leading: Icon(icon, color: const Color(0xFF2563EB)),
      title: Text(title, style: const TextStyle(fontWeight: FontWeight.w600)),
      subtitle: Text(subtitle), trailing: const Icon(Icons.chevron_right),
      onTap: onTap));

  @override
  Widget build(BuildContext context) {
    final connected = _requests.where((r) => r.status == 'Accepted' && !r.isExpired).toList();
    final pending = _requests.where((r) => r.status == 'Pending').toList();
    final shared = connected.fold<int>(0, (count, r) => count + r.documentIds.length);
    final expiring = connected.where((r) => r.accessExpiresAt != null &&
      r.accessExpiresAt!.isBefore(DateTime.now().add(const Duration(days: 30)))).length;
    final query = _connectionSearch.text.trim().toLowerCase();
    final filtered = _requests.where((r) {
      final contact = r.isIncoming ? r.requester : r.recipient;
      final relationship = r.relationship.toLowerCase();
      final fits = switch (_statusFilter) {
        'Family' => relationship != 'friend' && relationship != 'colleague',
        'Friends' => relationship == 'friend' || relationship == 'colleague',
        'Pending' => r.status == 'Pending',
        _ => true,
      };
      return fits && (query.isEmpty || contact.email.toLowerCase().contains(query) ||
        (contact.displayName?.toLowerCase().contains(query) ?? false));
    }).toList();
    return Scaffold(
      appBar: AppBar(title: const Text('Friends & Family'),
        actions: [IconButton(tooltip: 'Refresh', onPressed: _busy ? null : _load,
          icon: const Icon(Icons.refresh))]),
      body: _loading ? const Center(child: CircularProgressIndicator()) :
        RefreshIndicator(onRefresh: _load, child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(16),
          children: [
            LayoutBuilder(builder: (context, bounds) => Wrap(
              alignment: WrapAlignment.spaceBetween, crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 12, runSpacing: 12, children: [
                Column(crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Friends & Family', style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.bold, color: const Color(0xFF102552))),
                    const Text('Your trusted circle, connected across borders'),
                  ]),
                FilledButton.icon(onPressed: _busy ? null : _inviteDialog,
                  icon: const Icon(Icons.add), label: const Text('Add Connection')),
              ])),
            const SizedBox(height: 16),
            LayoutBuilder(builder: (context, bounds) => Wrap(
              spacing: 12, runSpacing: 12,
              children: [
                _metric('Connections', connected.length, Icons.people,
                  const Color(0xFFE9F2FF), const Color(0xFF1769EC)),
                _metric('Shared Documents', shared, Icons.folder_shared_outlined,
                  const Color(0xFFE3F7F2), const Color(0xFF009D78)),
                _metric('Pending Requests', pending.length, Icons.schedule_outlined,
                  const Color(0xFFF6EDFF), const Color(0xFF7C3AED)),
                _metric('Access Expiring', expiring, Icons.event_outlined,
                  const Color(0xFFFFF0E8), const Color(0xFFEA580C)),
              ])),
            const SizedBox(height: 16),
            TextField(controller: _connectionSearch, onChanged: (_) => setState(() {}),
              decoration: InputDecoration(prefixIcon: const Icon(Icons.search),
                hintText: 'Search by name or email to find your connections...',
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)))),
            const SizedBox(height: 12),
            SingleChildScrollView(scrollDirection: Axis.horizontal, child: Row(children: [
              for (final status in ['All', 'Family', 'Friends', 'Pending'])
                Padding(padding: const EdgeInsets.only(right: 8), child: ChoiceChip(
                  label: Text(status), selected: _statusFilter == status,
                  onSelected: (_) => setState(() => _statusFilter = status))),
            ])),
            if (_error != null) Padding(padding: const EdgeInsets.all(12), child: Text(_error!)),
            if (_busy) const LinearProgressIndicator(),
            const SizedBox(height: 8),
            if (filtered.isEmpty) const Card(child: Padding(padding: EdgeInsets.all(24),
              child: Center(child: Text('No matching connections yet.')))),
            for (final r in filtered)
              Card(child: ListTile(
                contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                leading: ProfileAvatar(imageUrl: (r.isIncoming ? r.requester : r.recipient).profilePhotoUrl,
                  displayName: (r.isIncoming ? r.requester : r.recipient).displayName,
                  email: (r.isIncoming ? r.requester : r.recipient).email),
                title: Text((r.isIncoming ? r.requester : r.recipient).displayName ??
                  (r.isIncoming ? r.requester : r.recipient).email,
                  style: const TextStyle(fontWeight: FontWeight.w600)),
                subtitle: Wrap(spacing: 8, runSpacing: 4, children: [
                  Text(r.relationship),
                  Text('${r.documentIds.length} shared documents'),
                  Text(r.status),
                  if (r.accessExpiresAt != null) Text('Until ${_formatAccessDate(r.accessExpiresAt!)}'),
                ]),
                trailing: const Icon(Icons.chevron_right),
                onTap: () async {
                  if (r.isIncoming && r.status == 'Pending') {
                    await _incomingInvitationDialog(r);
                  } else if (r.isIncoming && r.status == 'Accepted') {
                    await _choose(r);
                  } else if (r.status == 'Accepted' || r.status == 'Pending') {
                    await _connectionActions(r);
                  }
                },
              )),
            const SizedBox(height: 16),
            Text('Explore your circle', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 8),
            _shortcut('Friends & Family Dashboard', 'Sharing overview and document alerts',
              Icons.dashboard_outlined, () async {
                await Navigator.push<void>(context, MaterialPageRoute(builder: (_) =>
                  FamilyDashboardScreen(documentService: widget.documentService)));
                if (mounted) _load();
              }),
            _shortcut('Birthdays & Special Occasions', 'Celebrations, reminders and upcoming events',
              Icons.cake_outlined, () => Navigator.push<void>(context, MaterialPageRoute(
                builder: (_) => FamilyOccasionsScreen(api: widget.documentService.apiClient)))),
          ],
        )),
    );
  }

  Future<void> _incomingInvitationDialog(FamilyRequest request) async {
    final decision = await showDialog<String>(context: context, builder: (ctx) => AlertDialog(
      title: const Text('You have a new connection request'),
      content: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
        ListTile(leading: ProfileAvatar(imageUrl: request.requester.profilePhotoUrl,
          displayName: request.requester.displayName, email: request.requester.email),
          title: Text(request.requester.displayName ?? request.requester.email),
          subtitle: Text(request.requester.email)),
        Chip(avatar: const Icon(Icons.favorite_outline, size: 16),
          label: Text(request.relationship)),
        const SizedBox(height: 10),
        const Text('Accept to choose which documents to share and for how long. '
          'You can also accept without sharing documents.'),
        const SizedBox(height: 8),
        const Text('Blocking users is not available from the current API.',
          style: TextStyle(fontSize: 12, color: Colors.blueGrey)),
      ]),
      actions: [
        TextButton(onPressed: () => Navigator.pop(ctx, 'reject'),
          child: const Text('Reject', style: TextStyle(color: Colors.red))),
        TextButton(onPressed: null, child: const Text('Block')),
        FilledButton(onPressed: () => Navigator.pop(ctx, 'accept'),
          child: const Text('Accept')),
      ],
    ));
    if (!mounted) return;
    if (decision == 'accept') {
      await _choose(request);
    } else if (decision == 'reject') {
      await _run(() async {
        await _service.respond(request, false, []);
        await _load();
      }, success: 'Invitation declined');
    }
  }

  Future<void> _connectionActions(FamilyRequest request) async {
    final shouldEnd = await showDialog<bool>(context: context, builder: (ctx) => AlertDialog(
      title: Text(request.status == 'Pending' ? 'Pending invitation' : 'Connection details'),
      content: Text('${request.relationship} • ${request.status} • ${request.documentIds.length} shared documents'),
      actions: [
        TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Close')),
        TextButton(onPressed: () => Navigator.pop(ctx, true),
          child: Text(request.status == 'Pending' ? 'Cancel invitation' : 'Revoke access')),
      ],
    ));
    if (shouldEnd == true && mounted) _end(request);
  }

}

class _GrantScreen extends StatefulWidget {
  final DocumentService documentService;
  final FamilyRequest request;
  const _GrantScreen({required this.documentService, required this.request});
  @override
  State<_GrantScreen> createState() => _GrantScreenState();
}
class _GrantScreenState extends State<_GrantScreen> {
  List<DocumentItem> _docs = [];
  final Set<String> _selected = {};
  bool _loading = true, _saving = false;
  int _wizardStep = 0;
  late bool _allowExpiryReminders;
  late bool _allowVisaApplicationUse;
  DateTime? _accessExpiresAt;
  bool _expiryChanged = false;
  String? _error;
  @override
  void initState() { super.initState(); _allowExpiryReminders = widget.request.allowExpiryReminders; _allowVisaApplicationUse = widget.request.allowVisaApplicationUse; _accessExpiresAt = widget.request.accessExpiresAt?.toLocal(); _load(); }
  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final docs = (await widget.documentService.getDocuments()).where((d) => !d.isSharedWithMe).toList();
      if (mounted) {
        setState(() {
        _docs = docs;
        _selected.addAll(widget.request.documentIds.where((id) => docs.any((d) => d.id == id)));
        _loading = false;
      });
      }
    } catch (e) { if (mounted) setState(() { _loading = false; _error = '$e'; }); }
  }
  Future<void> _save() async {
    if (_accessExpiresAt != null && !_accessExpiresAt!.isAfter(DateTime.now())) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Choose a future deadline or turn temporary access off.')));
      return;
    }
    setState(() => _saving = true);
    try {
      final service = widget.documentService.familyService;
      if (widget.request.status == 'Pending') {
        await service.respond(widget.request, true, _selected.toList(), allowExpiryReminders: _allowExpiryReminders, allowVisaApplicationUse: _allowVisaApplicationUse,
          accessExpiresAt: _accessExpiresAt, updateAccessExpiry: _expiryChanged);
      } else {
        await service.update(widget.request, _selected.toList(), allowExpiryReminders: _allowExpiryReminders,
          accessExpiresAt: _accessExpiresAt, updateAccessExpiry: _expiryChanged);
      }
      if (mounted) Navigator.pop(context, true);
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    } finally { if (mounted) setState(() => _saving = false); }
  }
  Future<void> _pickExpiry() async {
    final now = DateTime.now();
    final initial = _accessExpiresAt != null && _accessExpiresAt!.isAfter(now)
        ? _accessExpiresAt! : now.add(const Duration(days: 1));
    final date = await showDatePicker(context: context, initialDate: initial,
      firstDate: DateTime(now.year, now.month, now.day),
      lastDate: DateTime(initial.year > now.year + 10 ? initial.year : now.year + 10, 12, 31));
    if (date == null || !mounted) return;
    final time = await showTimePicker(context: context, initialTime: TimeOfDay.fromDateTime(initial));
    if (time == null || !mounted) return;
    final expiry = DateTime(date.year, date.month, date.day, time.hour, time.minute);
    if (!expiry.isAfter(DateTime.now())) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Choose a future date and time.')));
      return;
    }
    setState(() { _accessExpiresAt = expiry; _expiryChanged = true; });
  }
  @override
  Widget build(BuildContext context) {
    final isNew = widget.request.status == 'Pending';
    final name = widget.request.requester.displayName ?? widget.request.requester.email;
    return Scaffold(
      appBar: AppBar(
        title: Text(isNew ? 'Share Documents with $name' : 'Manage Sharing with $name'),
        leading: IconButton(icon: const Icon(Icons.close), onPressed: () => Navigator.pop(context)),
      ),
      body: _loading ? const Center(child: CircularProgressIndicator()) :
        _error != null ? Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
          Text(_error!), TextButton(onPressed: _load, child: const Text('Retry')),
        ])) : Column(children: [
          Padding(padding: const EdgeInsets.all(12), child: Row(children: [
            for (var i = 0; i < 3; i++) Expanded(child: Column(children: [
              CircleAvatar(radius: 14,
                backgroundColor: i <= _wizardStep ? const Color(0xFF1769EC) : const Color(0xFFE5EDF8),
                child: Text('${i + 1}', style: TextStyle(
                  color: i <= _wizardStep ? Colors.white : Colors.blueGrey, fontSize: 12))),
              Text(['Select Documents', 'Access Duration', 'Confirm'][i],
                style: const TextStyle(fontSize: 11)),
            ])),
          ])),
          Expanded(child: _wizardStep == 0 ? _selectDocumentsStep()
            : _wizardStep == 1 ? _durationStep() : _confirmStep()),
          SafeArea(child: Padding(padding: const EdgeInsets.all(12), child: Row(children: [
            Expanded(child: OutlinedButton(
              onPressed: _saving ? null : () {
                if (_wizardStep == 0) Navigator.pop(context);
                else setState(() => _wizardStep--);
              }, child: Text(_wizardStep == 0 ? 'Cancel' : 'Back'))),
            const SizedBox(width: 12),
            Expanded(child: FilledButton(
              onPressed: _saving ? null : () {
                if (_wizardStep == 2) { _save(); return; }
                if (_wizardStep == 1 &&
                    _accessExpiresAt != null && !_accessExpiresAt!.isAfter(DateTime.now())) {
                  ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
                    content: Text('Choose a future access deadline.')));
                  return;
                }
                setState(() => _wizardStep++);
              },
              child: Text(_saving ? 'Saving…' : _wizardStep == 2
                ? (isNew ? 'Confirm & Share' : 'Save Sharing') : 'Next'))),
          ]))),
        ]),
    );
  }

  Widget _selectDocumentsStep() => ListView(padding: const EdgeInsets.all(16), children: [
    Text('Choose which documents to share', style: Theme.of(context).textTheme.titleMedium),
    const SizedBox(height: 8),
    const Text('Only selected documents are shared. New uploads remain private until you grant access.'),
    const SizedBox(height: 12),
    RadioListTile<bool>(title: const Text('Share all eligible documents'),
      value: true, groupValue: _docs.isNotEmpty && _selected.length == _docs.length,
      onChanged: (_) => setState(() { _selected..clear()..addAll(_docs.map((d) => d.id)); })),
    RadioListTile<bool>(title: const Text('Share specific documents'),
      value: false, groupValue: _docs.isNotEmpty && _selected.length == _docs.length,
      onChanged: (_) => setState(() => _selected.clear())),
    if (_docs.isEmpty) const Padding(padding: EdgeInsets.all(12),
      child: Text('No completed documents. You may accept now and share later.')),
    ..._docs.map((d) => CheckboxListTile(
      secondary: const Icon(Icons.description_outlined, color: Color(0xFF2563EB)),
      title: Text(d.name), subtitle: Text(d.documentType),
      value: _selected.contains(d.id),
      onChanged: _saving ? null : (v) => setState(() {
        if (v == true) { _selected.add(d.id); } else { _selected.remove(d.id); }
      }),
    )),
  ]);

  Widget _durationStep() => ListView(padding: const EdgeInsets.all(16), children: [
    Text('Access Duration', style: Theme.of(context).textTheme.titleMedium),
    const SizedBox(height: 8),
    const Text('Sharing expires automatically at the selected deadline. You can revoke access earlier.'),
    RadioListTile<String>(title: const Text('No expiry'), value: 'none',
      groupValue: _durationChoice(), onChanged: _saving ? null : (_) => setState(() {
        _accessExpiresAt = null; _expiryChanged = true;
      })),
    for (final entry in const {
      '7 days': Duration(days: 7),
      '30 days': Duration(days: 30),
      '90 days': Duration(days: 90),
    }.entries)
      RadioListTile<String>(title: Text(entry.key), value: entry.key,
        groupValue: _durationChoice(), onChanged: _saving ? null : (_) => setState(() {
          _accessExpiresAt = DateTime.now().add(entry.value); _expiryChanged = true;
        })),
    RadioListTile<String>(title: const Text('Custom expiry date'), value: 'custom',
      groupValue: _durationChoice(), onChanged: _saving ? null : (_) => _pickExpiry()),
    if (_accessExpiresAt != null) Text('Access ends: ${_formatAccessDate(_accessExpiresAt!)}'),
    const SizedBox(height: 12),
    SwitchListTile(title: const Text('Allow shared expiry reminders'),
      subtitle: const Text('Only for documents with owner-confirmed expiry dates.'),
      value: _allowExpiryReminders,
      onChanged: _saving ? null : (v) => setState(() => _allowExpiryReminders = v)),
    if (widget.request.status == 'Pending')
      SwitchListTile(title: const Text('Allow use in visa applications'),
        subtitle: const Text('Permission is separate from document viewing. Off by default.'),
        value: _allowVisaApplicationUse,
        onChanged: _saving ? null : (v) => setState(() => _allowVisaApplicationUse = v)),
  ]);

  String _durationChoice() {
    if (_accessExpiresAt == null) return 'none';
    final days = _accessExpiresAt!.difference(DateTime.now()).inHours;
    if (days >= 24 * 7 - 1 && days <= 24 * 7 + 1) return '7 days';
    if (days >= 24 * 30 - 1 && days <= 24 * 30 + 1) return '30 days';
    if (days >= 24 * 90 - 1 && days <= 24 * 90 + 1) return '90 days';
    return 'custom';
  }

  Widget _confirmStep() => ListView(padding: const EdgeInsets.all(16), children: [
    Text('Confirm Sharing', style: Theme.of(context).textTheme.titleMedium),
    const SizedBox(height: 12),
    ListTile(leading: const Icon(Icons.person_outline),
      title: Text(widget.request.requester.displayName ?? widget.request.requester.email),
      subtitle: Text('Relationship: ${widget.request.relationship}')),
    const Divider(),
    const ListTile(leading: Icon(Icons.folder_shared_outlined),
      title: Text('Documents to share')),
    for (final d in _docs.where((d) => _selected.contains(d.id)))
      ListTile(dense: true, leading: const Icon(Icons.description_outlined),
        title: Text(d.name), subtitle: Text(d.documentType)),
    if (_selected.isEmpty) const ListTile(title: Text('No documents selected')),
    const Divider(),
    ListTile(leading: const Icon(Icons.event_available_outlined),
      title: Text(_accessExpiresAt == null ? 'No expiry' : _formatAccessDate(_accessExpiresAt!)),
      subtitle: const Text('Access duration')),
    const Card(child: Padding(padding: EdgeInsets.all(12),
      child: Text('The connected person can only access documents you share. '
        'You may revoke access at any time.'))),
  ]);

}

String _formatAccessDate(DateTime value) {
  final local = value.toLocal();
  final offset = local.timeZoneOffset;
  final minutes = offset.inMinutes.abs();
  String two(int n) => n.toString().padLeft(2, '0');
  return '${local.year}-${two(local.month)}-${two(local.day)} ${two(local.hour)}:${two(local.minute)} '
      '(UTC${offset.isNegative ? "-" : "+"}${two(minutes ~/ 60)}:${two(minutes % 60)})';
}
