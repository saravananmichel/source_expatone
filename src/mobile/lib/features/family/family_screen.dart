import 'package:flutter/material.dart';
import 'family_dashboard_screen.dart';
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
  String _relationship = 'Friend';
  String _statusFilter = 'All';
  List<FamilyRequest> _requests = [];
  FamilyUser? _found;
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
    await Navigator.push<bool>(context, MaterialPageRoute(builder: (_) => _GrantScreen(
      documentService: widget.documentService, request: request,
    )));
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
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Friends & Family'), actions: [IconButton(
      tooltip: 'Refresh', onPressed: _busy ? null : _load, icon: const Icon(Icons.refresh))]),
    body: _loading ? const Center(child: CircularProgressIndicator()) : RefreshIndicator(
      onRefresh: _load,
      child: ListView(padding: const EdgeInsets.all(16), physics: const AlwaysScrollableScrollPhysics(), children: [
        Wrap(spacing: 8, runSpacing: 8, children: [
          Chip(avatar: const Icon(Icons.people_outline, size: 18),
            label: Text('Active: ${_requests.where((r) => r.status == 'Accepted' && !r.isExpired).length}')),
          Chip(avatar: const Icon(Icons.mark_email_unread_outlined, size: 18),
            label: Text('Incoming: ${_requests.where((r) => r.status == 'Pending' && r.isIncoming).length}')),
          Chip(avatar: const Icon(Icons.outgoing_mail, size: 18),
            label: Text('Sent: ${_requests.where((r) => r.status == 'Pending' && !r.isIncoming).length}')),
        ]),
        const SizedBox(height: 12),
        Card(child: ListTile(
          leading: const Icon(Icons.dashboard_outlined),
          title: const Text('Friends & Family dashboard'),
          subtitle: const Text('Documents by member, expiry dates and alerts'),
          trailing: const Icon(Icons.chevron_right),
          onTap: () async {
            await Navigator.push<void>(context, MaterialPageRoute(builder: (_) =>
              FamilyDashboardScreen(documentService: widget.documentService)));
            if (mounted) await _load();
          },
        )),
        const SizedBox(height: 16),
        const Text('Connect with friends & family', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
        const SizedBox(height: 8),
        const Text('Invite a trusted person. They decide whether to accept and which documents to share, for how long. Existing sharing permissions remain in place.'),
        const SizedBox(height: 16),
        TextField(controller: _email, keyboardType: TextInputType.emailAddress,
          enabled: !_busy, onChanged: (_) => setState(() => _found = null),
          decoration: const InputDecoration(labelText: 'Registered email address', border: OutlineInputBorder())),
        const SizedBox(height: 6),
        const Text('To protect privacy, new connections are found by their complete registered email. You can search existing connections by name below.'),
        const SizedBox(height: 8),
        DropdownButtonFormField<String>(
          value: _relationship,
          decoration: const InputDecoration(labelText: 'Relationship', border: OutlineInputBorder()),
          items: const ['Spouse', 'Daughter', 'Son', 'Father', 'Mother', 'Brother', 'Sister', 'Friend', 'Colleague', 'Guardian', 'Other']
            .map((name) => DropdownMenuItem(value: name, child: Text(name))).toList(),
          onChanged: _busy ? null : (value) { if (value != null) setState(() => _relationship = value); },
        ),
        const SizedBox(height: 8),
        FilledButton.icon(onPressed: _busy ? null : () => _run(() async {
          setState(() => _found = null);
          final user = await _service.search(_email.text);
          if (mounted) setState(() => _found = user);
        }), icon: const Icon(Icons.search), label: const Text('Search')),
        if (_found != null) Card(child: ListTile(
          title: Text(_found!.displayName ?? _found!.email), subtitle: Text(_found!.email),
          trailing: TextButton(onPressed: _busy ? null : () => _run(() async {
            final confirmed = await showDialog<bool>(context: context, builder: (dialogContext) => AlertDialog(
              title: const Text('Send connection invitation?'),
              content: Text('Invite ${_found!.displayName ?? _found!.email} as $_relationship? They will choose whether to accept and which documents to share.'),
              actions: [
                TextButton(onPressed: () => Navigator.pop(dialogContext, false), child: const Text('Cancel')),
                FilledButton(onPressed: () => Navigator.pop(dialogContext, true), child: const Text('Send invitation')),
              ],
            ));
            if (confirmed != true) return;
            await _service.send(_found!.email, relationship: _relationship);
            if (mounted) setState(() => _found = null);
            await _load();
          }, success: 'Request sent'), child: const Text('Send request')))),
        if (_busy) const LinearProgressIndicator(),
        if (_error != null) Padding(padding: const EdgeInsets.symmetric(vertical: 12), child: Text(_error!)),
        const SizedBox(height: 24),
        const Text('Connections & document access', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
        const SizedBox(height: 8),
        Wrap(spacing: 8, runSpacing: 8, children: [
          for (final status in const ['All', 'Pending', 'Accepted', 'Rejected', 'Cancelled', 'Revoked'])
            ChoiceChip(label: Text(status), selected: _statusFilter == status,
              onSelected: (_) => setState(() => _statusFilter = status)),
        ]),
        const SizedBox(height: 8),
        TextField(controller: _connectionSearch, onChanged: (_) => setState(() {}),
          decoration: const InputDecoration(labelText: 'Search connections by name or email',
            prefixIcon: Icon(Icons.search), border: OutlineInputBorder())),
        const SizedBox(height: 8),
        if (_requests.isEmpty && _error == null) const Padding(padding: EdgeInsets.all(16), child: Text('No family requests yet.')),
        ..._requests.where((r) {
          final contact = r.isIncoming ? r.requester : r.recipient;
          final query = _connectionSearch.text.trim().toLowerCase();
          return (_statusFilter == 'All' || r.status == _statusFilter) &&
            (query.isEmpty || contact.email.toLowerCase().contains(query) ||
            (contact.displayName?.toLowerCase().contains(query) ?? false));
        }).map((r) => Card(child: Padding(padding: const EdgeInsets.all(12), child: Column(
          crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text((r.isIncoming ? r.requester : r.recipient).email, style: const TextStyle(fontWeight: FontWeight.bold)),
            Text('${r.isIncoming ? "Incoming" : "Sent"} • ${r.relationship} • ${r.status}'),
            if (r.status == 'Accepted' && !r.isExpired) Text('${r.documentIds.length} document(s) shared ${r.isIncoming ? "by you" : "with you"}'),
            if (r.status == 'Accepted') Text(r.isExpired
              ? 'Access expired — owner can renew sharing'
              : r.accessExpiresAt == null ? 'Access: no expiry' : 'Access ends: ${_formatAccessDate(r.accessExpiresAt!)}'),
            Wrap(spacing: 8, children: [
              if (r.isIncoming && r.status == 'Pending') ...[
                TextButton(onPressed: _busy ? null : () => _choose(r), child: const Text('Review & accept')),
                TextButton(onPressed: _busy ? null : () => _run(() async {
                  await _service.respond(r, false, []); await _load();
                }), child: const Text('Reject')),
              ],
              if (r.isIncoming && r.status == 'Accepted')
                TextButton(onPressed: _busy ? null : () => _choose(r), child: Text(r.isExpired ? 'Renew sharing' : 'Manage documents')),
              if (r.status == 'Accepted' || (!r.isIncoming && r.status == 'Pending'))
                TextButton(onPressed: _busy ? null : () => _end(r), child: Text(r.status == 'Pending' ? 'Cancel' : 'Revoke access')),
            ]),
          ],
        )))),
      ]),
    ),
  );
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
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Choose documents')),
    body: _loading ? const Center(child: CircularProgressIndicator()) : _error != null
      ? Center(child: Column(mainAxisSize: MainAxisSize.min, children: [Text(_error!), TextButton(onPressed: _load, child: const Text('Retry'))]))
      : Column(children: [
        Expanded(child: ListView(children: [
          Padding(padding: const EdgeInsets.all(16), child: Text('Grant ${widget.request.requester.email} read-only access. Select all covers current documents only. New uploads are private until selected here.')),
          SwitchListTile(
            title: const Text('Temporary access'),
            subtitle: const Text('One deadline applies to all documents selected for this member. Access and shared expiry alerts stop automatically.'),
            value: _accessExpiresAt != null,
            onChanged: _saving ? null : (value) => setState(() {
              _expiryChanged = true;
              _accessExpiresAt = value ? DateTime.now().add(const Duration(days: 1)) : null;
            }),
          ),
          if (_accessExpiresAt != null) Padding(padding: const EdgeInsets.symmetric(horizontal: 16), child: Column(
            crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text('Access ends: ${_formatAccessDate(_accessExpiresAt!)}'),
              Wrap(spacing: 8, children: [
                for (final choice in const {'1 hour': Duration(hours: 1), '24 hours': Duration(days: 1),
                    '7 days': Duration(days: 7), '30 days': Duration(days: 30), '90 days': Duration(days: 90)}.entries)
                  ActionChip(label: Text(choice.key), onPressed: _saving ? null : () => setState(() {
                    _accessExpiresAt = DateTime.now().add(choice.value); _expiryChanged = true;
                  })),
                TextButton(onPressed: _saving ? null : _pickExpiry, child: const Text('Choose date & time')),
              ]),
              if (!_accessExpiresAt!.isAfter(DateTime.now()))
                const Text('This access has expired. Choose a future deadline or turn temporary access off to renew it.'),
            ],
          )),
          SwitchListTile(
            title: const Text('Allow shared expiry reminders'),
            subtitle: const Text('This member can receive in-app alerts for selected documents after you confirm their expiry dates.'),
            value: _allowExpiryReminders,
            onChanged: _saving ? null : (value) => setState(() => _allowExpiryReminders = value),
          ),
          SwitchListTile(
            title: const Text('Allow use in visa applications'),
            subtitle: const Text('Separate permission: the connected person may use the selected shared documents in visa applications while sharing remains active. Off by default.'),
            value: _allowVisaApplicationUse,
            onChanged: _saving ? null : (value) => setState(() => _allowVisaApplicationUse = value),
          ),
          CheckboxListTile(title: const Text('Select all current documents'),
            value: _docs.isNotEmpty && _selected.length == _docs.length,
            onChanged: _saving ? null : (value) => setState(() {
              _selected.clear(); if (value == true) _selected.addAll(_docs.map((d) => d.id));
            })),
          if (_docs.isEmpty) const Padding(padding: EdgeInsets.all(16), child: Text('No completed documents yet. You can accept now and grant documents later.')),
          ..._docs.map((d) => CheckboxListTile(
            title: Text(d.name), subtitle: Text(d.documentType), value: _selected.contains(d.id),
            onChanged: _saving ? null : (value) => setState(() { if (value == true) { _selected.add(d.id); } else { _selected.remove(d.id); } }),
          )),
        ])),
        SafeArea(child: Padding(padding: const EdgeInsets.all(16), child: FilledButton(
          onPressed: _saving ? null : _save,
          child: Text(_saving ? 'Saving…' : '${widget.request.status == "Pending" ? "Accept & share" : "Save access"} (${_selected.length})'),
        ))),
      ]),
  );
}

String _formatAccessDate(DateTime value) {
  final local = value.toLocal();
  final offset = local.timeZoneOffset;
  final minutes = offset.inMinutes.abs();
  String two(int n) => n.toString().padLeft(2, '0');
  return '${local.year}-${two(local.month)}-${two(local.day)} ${two(local.hour)}:${two(local.minute)} '
      '(UTC${offset.isNegative ? "-" : "+"}${two(minutes ~/ 60)}:${two(minutes % 60)})';
}
