import 'package:flutter/material.dart';
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
  List<FamilyRequest> _requests = [];
  FamilyUser? _found;
  bool _loading = true, _busy = false;
  String? _error;
  FamilyService get _service => widget.documentService.familyService;
  @override
  void initState() { super.initState(); _load(); }
  @override
  void dispose() { _email.dispose(); super.dispose(); }
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
    appBar: AppBar(title: const Text('Family'), actions: [IconButton(
      tooltip: 'Refresh', onPressed: _busy ? null : _load, icon: const Icon(Icons.refresh))]),
    body: _loading ? const Center(child: CircularProgressIndicator()) : RefreshIndicator(
      onRefresh: _load,
      child: ListView(padding: const EdgeInsets.all(16), physics: const AlwaysScrollableScrollPhysics(), children: [
        const Text('Request access to a family member’s documents', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
        const SizedBox(height: 8),
        const Text('They choose which documents to share with you. Sharing is one-way and read-only.'),
        const SizedBox(height: 16),
        TextField(controller: _email, keyboardType: TextInputType.emailAddress,
          enabled: !_busy, onChanged: (_) => setState(() => _found = null),
          decoration: const InputDecoration(labelText: 'Exact registered email address', border: OutlineInputBorder())),
        const SizedBox(height: 8),
        FilledButton.icon(onPressed: _busy ? null : () => _run(() async {
          setState(() => _found = null);
          final user = await _service.search(_email.text);
          if (mounted) setState(() => _found = user);
        }), icon: const Icon(Icons.search), label: const Text('Search')),
        if (_found != null) Card(child: ListTile(
          title: Text(_found!.displayName ?? _found!.email), subtitle: Text(_found!.email),
          trailing: TextButton(onPressed: _busy ? null : () => _run(() async {
            await _service.send(_found!.email);
            if (mounted) setState(() => _found = null);
            await _load();
          }, success: 'Request sent'), child: const Text('Send request')))),
        if (_busy) const LinearProgressIndicator(),
        if (_error != null) Padding(padding: const EdgeInsets.symmetric(vertical: 12), child: Text(_error!)),
        const SizedBox(height: 24),
        const Text('Requests & access', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
        if (_requests.isEmpty && _error == null) const Padding(padding: EdgeInsets.all(16), child: Text('No family requests yet.')),
        ..._requests.map((r) => Card(child: Padding(padding: const EdgeInsets.all(12), child: Column(
          crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text((r.isIncoming ? r.requester : r.recipient).email, style: const TextStyle(fontWeight: FontWeight.bold)),
            Text('${r.isIncoming ? "Incoming" : "Sent"} • ${r.status}'),
            if (r.status == 'Accepted') Text('${r.documentIds.length} document(s) shared ${r.isIncoming ? "by you" : "with you"}'),
            Wrap(spacing: 8, children: [
              if (r.isIncoming && r.status == 'Pending') ...[
                TextButton(onPressed: _busy ? null : () => _choose(r), child: const Text('Review & accept')),
                TextButton(onPressed: _busy ? null : () => _run(() async {
                  await _service.respond(r, false, []); await _load();
                }), child: const Text('Reject')),
              ],
              if (r.isIncoming && r.status == 'Accepted')
                TextButton(onPressed: _busy ? null : () => _choose(r), child: const Text('Manage documents')),
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
  String? _error;
  @override
  void initState() { super.initState(); _load(); }
  Future<void> _load() async {
    setState(() { _loading = true; _error = null; });
    try {
      final docs = (await widget.documentService.getDocuments()).where((d) => !d.isSharedWithMe).toList();
      if (mounted) {
          setState(() {
            _docs = docs;
            _selected.addAll(
              widget.request.documentIds.where(
                (id) => docs.any((d) => d.id == id),
              ),
            );
            _loading = false;
          });
        }
    } catch (e) { if (mounted) setState(() { _loading = false; _error = '$e'; }); }
  }
  Future<void> _save() async {
    setState(() => _saving = true);
    try {
      final service = widget.documentService.familyService;
      if (widget.request.status == 'Pending') {
        await service.respond(widget.request, true, _selected.toList());
      } else {
        await service.update(widget.request, _selected.toList());
      }
      if (mounted) Navigator.pop(context, true);
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
    } finally { if (mounted) setState(() => _saving = false); }
  }
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Choose documents')),
    body: _loading ? const Center(child: CircularProgressIndicator()) : _error != null
      ? Center(child: Column(mainAxisSize: MainAxisSize.min, children: [Text(_error!), TextButton(onPressed: _load, child: const Text('Retry'))]))
      : Column(children: [
        Padding(padding: const EdgeInsets.all(16), child: Text('Grant ${widget.request.requester.email} read-only access. Select all covers current documents only. New uploads are private until selected here.')),
        CheckboxListTile(title: const Text('Select all current documents'),
          value: _docs.isNotEmpty && _selected.length == _docs.length,
          onChanged: _saving ? null : (value) => setState(() {
            _selected.clear(); if (value == true) _selected.addAll(_docs.map((d) => d.id));
          })),
        if (_docs.isEmpty) const Padding(padding: EdgeInsets.all(16), child: Text('No completed documents yet. You can accept now and grant documents later.')),
        Expanded(child: ListView(children: _docs.map((d) => CheckboxListTile(
          title: Text(d.name), subtitle: Text(d.documentType), value: _selected.contains(d.id),
          onChanged: _saving ? null : (value) => setState(() { if (value == true) { _selected.add(d.id); } else { _selected.remove(d.id); } }),
        )).toList())),
        SafeArea(child: Padding(padding: const EdgeInsets.all(16), child: FilledButton(
          onPressed: _saving ? null : _save,
          child: Text(_saving ? 'Saving…' : '${widget.request.status == "Pending" ? "Accept & share" : "Save access"} (${_selected.length})'),
        ))),
      ]),
  );
}
