import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:share_plus/share_plus.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/services/document_service.dart';
import '../../core/services/visa_service.dart';

class ApplicationAssistantScreen extends StatefulWidget {
  final DocumentService documentService;
  final List<String> selectedDocumentIds;
  const ApplicationAssistantScreen({
    super.key,
    required this.documentService,
    this.selectedDocumentIds = const [],
  });
  @override
  State<ApplicationAssistantScreen> createState() =>
      _ApplicationAssistantScreenState();
}

class _ApplicationAssistantScreenState
    extends State<ApplicationAssistantScreen> {
  Map<String, dynamic>? _catalog;
  List<Map<String, dynamic>> _drafts = [];
  String _country = 'SG', _purpose = 'Tourism';
  bool _busy = false;
  String? _error;
  VisaService get _service => widget.documentService.visaService;
  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final catalog = await _service.catalog();
      final drafts = await _service.list();
      if (mounted) {
        setState(() {
          _catalog = catalog;
          _drafts = drafts;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() => _error = e.toString());
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _open(Map<String, dynamic> draft) async {
    final templates = (_catalog!['templates'] as List)
        .cast<Map<String, dynamic>>();
    final template = templates
        .where((t) => t['id'] == draft['templateId'])
        .firstOrNull;
    if (template == null) {
      return;
    }
    await Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => VisaDraftScreen(
          documentService: widget.documentService,
          id: draft['id'] as String,
          rules: Map<String, dynamic>.from(template['rules'] as Map),
        ),
      ),
    );
    if (mounted) {
      _load();
    }
  }

  String _titleFor(Map<String, dynamic> draft) {
    final templates = (_catalog?['templates'] as List? ?? [])
        .cast<Map<String, dynamic>>();
    final template = templates
        .where((t) => t['id'] == draft['templateId'])
        .firstOrNull;
    return (template?['rules'] as Map?)?['title']?.toString() ??
        draft['templateId']?.toString() ??
        'Visa application';
  }

  Future<void> _create() async {
    setState(() => _busy = true);
    try {
      final template = (_catalog!['templates'] as List)
          .cast<Map<String, dynamic>>()
          .firstWhere(
            (t) => t['countryCode'] == _country && t['purpose'] == _purpose,
          );
      final draft = await _service.create(
        template['id'] as String,
        widget.selectedDocumentIds
            .map(
              (id) => <String, dynamic>{
                'id': id,
                'role': 'Other supporting document',
              },
            )
            .toList(),
      );
      if (mounted) {
        await _open(draft);
      }
    } catch (e) {
      if (mounted) {
        setState(() => _error = e.toString());
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final countries = (_catalog?['countries'] as List? ?? [])
        .cast<Map<String, dynamic>>();
    final destinationEnabled = countries.any(
      (c) => c['code'] == _country && c['enabled'] == true,
    );
    final supported =
        destinationEnabled &&
        (_catalog?['templates'] as List? ?? []).any(
          (t) => t['countryCode'] == _country && t['purpose'] == _purpose,
        );
    return Scaffold(
      appBar: AppBar(
        title: const Text('Application Assistant'),
        actions: [
          IconButton(
            onPressed: _busy ? null : _load,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (_busy) const LinearProgressIndicator(),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.all(8),
              child: Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          const Text(
            'Prepare a visa application',
            style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 8),
          const Text(
            'Link documents, review extracted details and prepare your checklist. Official form export is available only where supported. Submit through the linked official portal or authorised filing contact.',
          ),
          const SizedBox(height: 16),
          if (countries.isNotEmpty)
            DropdownButtonFormField<String>(
              initialValue: _country,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Destination'),
              items: countries
                  .map(
                    (c) => DropdownMenuItem(
                      value: c['code'] as String,
                      child: Text(
                        '${c['name']}${c['enabled'] == true ? '' : ' — planned'}',
                      ),
                    ),
                  )
                  .toList(),
              onChanged: _busy ? null : (v) => setState(() => _country = v!),
            ),
          const SizedBox(height: 12),
          DropdownButtonFormField<String>(
            initialValue: _purpose,
            decoration: const InputDecoration(labelText: 'Application purpose'),
            items: [
              'Tourism',
              'Employment',
              'Dependent',
              'Renewal',
            ].map((p) => DropdownMenuItem(value: p, child: Text(p))).toList(),
            onChanged: _busy ? null : (v) => setState(() => _purpose = v!),
          ),
          const SizedBox(height: 12),
          Text(
            '${widget.selectedDocumentIds.length} documents selected from your wallet. You can change them inside the draft.',
          ),
          if (!supported && _catalog != null)
            const Text(
              'This destination / purpose is not available yet. Choose an enabled combination.',
            ),
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: supported && !_busy ? _create : null,
            icon: const Icon(Icons.add),
            label: const Text('Create draft'),
          ),
          const SizedBox(height: 24),
          const Text(
            'Saved applications',
            style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
          ),
          if (_drafts.isEmpty && !_busy)
            const Padding(
              padding: EdgeInsets.all(16),
              child: Text('No applications yet.'),
            ),
          ..._drafts.map(
            (d) => Card(
              child: ListTile(
                leading: const Icon(Icons.description_outlined),
                title: Text(_titleFor(d)),
                subtitle: Text(
                  '${d['status']} · ${d['updatedAt']?.toString().split('T').first ?? ''}',
                ),
                trailing: const Icon(Icons.chevron_right),
                onTap: _busy ? null : () => _open(d),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class VisaDraftScreen extends StatefulWidget {
  final DocumentService documentService;
  final String id;
  final Map<String, dynamic> rules;
  const VisaDraftScreen({
    super.key,
    required this.documentService,
    required this.id,
    required this.rules,
  });
  @override
  State<VisaDraftScreen> createState() => _VisaDraftScreenState();
}

class _VisaDraftScreenState extends State<VisaDraftScreen>
    with WidgetsBindingObserver {
  Map<String, dynamic>? _draft;
  Map<String, String> _values = {};
  Map<String, dynamic> _checks = {};
  bool _busy = false, _fieldsDirty = false, _checksDirty = false;
  String? _error;
  Timer? _expiry, _poll;
  VisaService get _service => widget.documentService.visaService;
  String get _revision => _draft!['revision'] as String;
  bool get _blocked => _draft?['accessBlocked'] == true;
  List<Map<String, dynamic>> get _docs =>
      (_draft?['documents'] as List? ?? []).cast<Map<String, dynamic>>();
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _load();
    _poll = Timer.periodic(const Duration(seconds: 45), (_) => _probe());
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _expiry?.cancel();
    _poll?.cancel();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _probe();
    }
  }

  void _accept(Map<String, dynamic> data) {
    _expiry?.cancel();
    setState(() {
      _draft = data;
      _values = Map<String, String>.from(data['values'] as Map? ?? {});
      _checks = Map<String, dynamic>.from(data['checks'] as Map? ?? {});
      _fieldsDirty = false;
      _checksDirty = false;
    });
    final deadline = DateTime.tryParse(
      data['accessValidUntilUtc']?.toString() ?? '',
    );
    final server = DateTime.tryParse(data['serverUtc']?.toString() ?? '');
    if (deadline != null && server != null) {
      final remaining = deadline.difference(server);
      _expiry = Timer(
        remaining.isNegative ? Duration.zero : remaining,
        _hideExpired,
      );
    }
  }

  void _hideExpired() {
    if (mounted) {
      _accept({
        'id': widget.id,
        'revision': _revision,
        'accessBlocked': true,
        'message':
            'Shared-document access expired. Reset document links to continue.',
      });
    }
  }

  Future<void> _probe() async {
    if (_busy || _draft == null || _blocked) {
      return;
    }
    try {
      final data = await _service.get(widget.id);
      if (mounted && data['accessBlocked'] == true) {
        _accept(data);
      }
    } catch (_) {
      if (mounted && _docs.isNotEmpty) {
        _accept({
          'id': widget.id,
          'revision': _revision,
          'accessBlocked': true,
          'message':
              'Unable to verify document access. Reload before continuing.',
        });
      }
    }
  }

  Future<void> _run(Future<Map<String, dynamic>> Function() action) async {
    if (_busy) {
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final data = await action();
      if (mounted) {
        _accept(data);
      }
    } catch (e) {
      if (mounted) {
        setState(() => _error = e.toString());
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _load() => _run(() => _service.get(widget.id));
  Future<void> _save() =>
      _run(() => _service.save(widget.id, _revision, _values, _checks));
  bool get _supportsSgPdf => widget.rules['id'] == 'SG-VISIT-2026-10-12';
  bool _active(String? condition) {
    if (condition?.startsWith('eq:') == true) {
      final parts = condition!.split(':');
      return parts.length == 3 &&
          parts[1].isNotEmpty &&
          (_values[parts[1]] ?? '') == parts[2];
    }
    final dob = DateTime.tryParse(_values['birthDate'] ?? '');
    final now = DateTime.now().toUtc();
    final child =
        dob != null &&
        dob.isBefore(now) &&
        DateTime.utc(dob.year + 13, dob.month, dob.day).isAfter(now);
    final spouse = [
      'Married',
      'Separated',
      'Customary',
    ].contains(_values['maritalStatus']);
    return switch (condition) {
      null => true,
      'spouse' => spouse,
      'spouseSg' =>
        spouse && (_values['spouseStatus'] ?? '').startsWith('Singapore'),
      'spouseOther' => spouse && _values['spouseStatus'] == 'Other',
      'otherPassport' => _values['passportType'] == 'Other',
      'chinese' => [
        'china',
        'chinese',
      ].contains(_values['nationality']?.toLowerCase()),
      'longStay' => _values['stayBand'] == 'More than 30 days',
      'otherStay' => _values['accommodationType'] == 'Other',
      'history' => _values['otherResidence'] == 'Yes',
      'child' => child,
      'companion' => child && _values['airlineRepresentative'] != 'Yes',
      'antecedent' => [1, 2, 3, 4].any((i) => _values['antecedent$i'] == 'Yes'),
      'loi' => _values['loiRequired'] == 'Yes',
      'translation' => _values['translationRequired'] == 'Yes',
      'loiIndividual' =>
        _values['loiRequired'] == 'Yes' &&
            _values['loiContactType'] == 'Individual',
      'loiCompany' =>
        _values['loiRequired'] == 'Yes' &&
            _values['loiContactType'] == 'Company',
      _ => false,
    };
  }

  Future<void> _documents() async {
    try {
      final available = await widget.documentService.getDocuments();
      if (!mounted) {
        return;
      }
      final selection = <String, String>{
        for (final d in _docs) d['id'] as String: d['role'] as String,
      };
      final result = await showDialog<List<Map<String, dynamic>>>(
        context: context,
        builder: (context) => StatefulBuilder(
          builder: (context, update) => AlertDialog(
            title: const Text('Link documents'),
            content: SizedBox(
              width: 560,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text(
                      'Choose ONE applicant passport. Adding evidence clears checklist confirmations. Removing documents or changing roles clears the form to prevent using stale details.',
                    ),
                    ...available.map(
                      (d) => Column(
                        children: [
                          CheckboxListTile(
                            contentPadding: EdgeInsets.zero,
                            title: Text(d.name),
                            subtitle: Text(
                              d.isSharedWithMe
                                  ? 'Shared document'
                                  : 'Your document',
                            ),
                            value: selection.containsKey(d.id),
                            onChanged: (v) => update(() {
                              if (v == true) {
                                selection[d.id] = 'Other supporting document';
                              } else {
                                selection.remove(d.id);
                              }
                            }),
                          ),
                          if (selection.containsKey(d.id))
                            DropdownButtonFormField<String>(
                              key: ValueKey('${d.id}-${selection[d.id]}'),
                              initialValue: selection[d.id],
                              isExpanded: true,
                              items: (widget.rules['documentRoles'] as List)
                                  .map(
                                    (r) => DropdownMenuItem(
                                      value: r as String,
                                      child: Text(r),
                                    ),
                                  )
                                  .toList(),
                              onChanged: (v) =>
                                  update(() => selection[d.id] = v!),
                            ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: () => Navigator.pop(
                  context,
                  selection.entries
                      .map(
                        (e) => <String, dynamic>{'id': e.key, 'role': e.value},
                      )
                      .toList(),
                ),
                child: const Text('Save links'),
              ),
            ],
          ),
        ),
      );
      if (result != null && mounted) {
        await _run(() => _service.documents(widget.id, _revision, result));
      }
    } catch (e) {
      if (mounted) {
        setState(() => _error = e.toString());
      }
    }
  }

  Future<void> _export(String form) async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final data = await _service.action(widget.id, _revision, 'export/$form');
      if (!mounted || _blocked) {
        return;
      }
      final box = context.findRenderObject() as RenderBox?;
      await SharePlus.instance.share(
        ShareParams(
          files: [
            XFile.fromData(
              base64Decode(data['base64'] as String),
              mimeType: 'application/pdf',
            ),
          ],
          fileNameOverrides: [data['fileName'] as String],
          sharePositionOrigin: box == null
              ? null
              : box.localToGlobal(Offset.zero) & box.size,
        ),
      );
    } catch (e) {
      if (mounted) {
        setState(() => _error = e.toString());
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _delete() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete this application?'),
        content: const Text('Your original wallet documents will remain.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (confirmed != true) {
      return;
    }
    try {
      await _service.delete(widget.id, _revision);
      if (mounted) {
        Navigator.pop(context);
      }
    } catch (e) {
      if (mounted) {
        setState(() => _error = e.toString());
      }
    }
  }

  Widget _field(Map<String, dynamic> f) {
    final key = f['key'] as String;
    final sourceId = (_draft?['sources'] as Map?)?[key];
    final source = _docs.where((d) => d['id'] == sourceId).firstOrNull;
    final label = '${f['label']}${f['required'] == true ? ' *' : ''}';
    void change(String value) {
      setState(() {
        _values[key] = value;
        _fieldsDirty = true;
      });
    }

    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (f['type'] == 'choice')
            DropdownButtonFormField<String>(
              key: ValueKey('$_revision-$key'),
              initialValue: (f['options'] as List).contains(_values[key])
                  ? _values[key]
                  : null,
              isExpanded: true,
              decoration: InputDecoration(labelText: label),
              items: (f['options'] as List)
                  .map(
                    (o) => DropdownMenuItem(value: o as String, child: Text(o)),
                  )
                  .toList(),
              onChanged: _busy ? null : (v) => change(v!),
            )
          else
            TextFormField(
              key: ValueKey('$_revision-$key'),
              initialValue: _values[key] ?? '',
              enabled: !_busy,
              maxLength: f['maxLength'] as int?,
              minLines: f['type'] == 'multiline' ? 3 : 1,
              maxLines: f['type'] == 'multiline' ? 6 : 1,
              keyboardType: f['type'] == 'number'
                  ? TextInputType.number
                  : TextInputType.text,
              decoration: InputDecoration(
                labelText: label,
                hintText: f['type'] == 'date' ? 'YYYY-MM-DD' : null,
                counterText: '',
              ),
              onChanged: change,
            ),
          if (source != null)
            Text(
              'Suggested from ${source['name']} — verify against the original.',
              style: Theme.of(context).textTheme.bodySmall,
            ),
        ],
      ),
    );
  }

  Widget _check(Map<String, dynamic> c) {
    final key = c['key'] as String;
    final input = Map<String, dynamic>.from(_checks[key] as Map? ?? {});
    final enabled = !_busy && !_fieldsDirty;
    void change(String name, dynamic value) {
      setState(() {
        _checks[key] = {...input, name: value};
        _checksDirty = true;
      });
    }

    final evidence = _docs.where((d) => d['role'] == c['role']).toList();
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              title: Text(c['label'] as String),
              value: input['confirmed'] == true,
              onChanged: enabled ? (v) => change('confirmed', v) : null,
            ),
            if (c['needsDocument'] == true)
              DropdownButtonFormField<String>(
                key: ValueKey('$_revision-$key-doc'),
                initialValue:
                    evidence.any((d) => d['id'] == input['documentId'])
                    ? input['documentId'] as String?
                    : null,
                isExpanded: true,
                decoration: InputDecoration(
                  labelText: 'Evidence: ${c['role']}',
                ),
                items: evidence
                    .map(
                      (d) => DropdownMenuItem(
                        value: d['id'] as String,
                        child: Text(
                          d['name'] as String,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    )
                    .toList(),
                onChanged: enabled ? (v) => change('documentId', v) : null,
              ),
            if (c['noteRequired'] == true)
              TextFormField(
                key: ValueKey('$_revision-$key-note'),
                initialValue: input['note'] as String? ?? '',
                enabled: enabled,
                maxLength: 1500,
                minLines: 2,
                maxLines: 4,
                decoration: const InputDecoration(
                  labelText: 'Review note / official page or filing-contact requirements',
                ),
                onChanged: (v) => change('note', v),
              ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final fields = (widget.rules['fields'] as List)
        .cast<Map<String, dynamic>>()
        .where((f) => _active(f['when'] as String?))
        .toList();
    final sections = fields.map((f) => f['section'] as String).toSet();
    final assessment = _draft?['assessment'] as Map? ?? {};
    final complete =
        !_fieldsDirty &&
        !_checksDirty &&
        ['ChecklistComplete', 'Submitted'].contains(_draft?['status']);
    return PopScope(
      canPop: !_fieldsDirty && !_checksDirty,
      onPopInvokedWithResult: (didPop, result) async {
        if (didPop) {
          return;
        }
        final leave = await showDialog<bool>(
          context: context,
          builder: (context) => AlertDialog(
            title: const Text('Discard unsaved edits?'),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(context, false),
                child: const Text('Keep editing'),
              ),
              TextButton(
                onPressed: () => Navigator.pop(context, true),
                child: const Text('Discard'),
              ),
            ],
          ),
        );
        if (leave == true && context.mounted) {
          setState(() {
            _fieldsDirty = false;
            _checksDirty = false;
          });
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (mounted) {
              Navigator.pop(context);
            }
          });
        }
      },
      child: Scaffold(
        appBar: AppBar(
          title: Text(widget.rules['title']?.toString() ?? 'Visa application'),
          actions: [
            IconButton(
              onPressed: _busy || _fieldsDirty || _checksDirty ? null : _load,
              tooltip: 'Reload',
              icon: const Icon(Icons.refresh),
            ),
            IconButton(
              onPressed: _busy || _draft == null || _fieldsDirty || _checksDirty
                  ? null
                  : _delete,
              tooltip: 'Delete draft',
              icon: const Icon(Icons.delete_outline),
            ),
          ],
        ),
        bottomNavigationBar: !_blocked && _draft != null
            ? SafeArea(
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: FilledButton.icon(
                    onPressed: _busy ? null : _save,
                    icon: const Icon(Icons.save_outlined),
                    label: Text(
                      _fieldsDirty
                          ? 'Save fields and refresh checklist'
                          : 'Save checklist',
                    ),
                  ),
                ),
              )
            : null,
        body: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            if (_busy) const LinearProgressIndicator(),
            if (_error != null)
              Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            if (_blocked) ...[
              Text(
                _draft?['message'] as String? ??
                    'Document access needs review.',
              ),
              FilledButton(
                onPressed: _busy
                    ? null
                    : () => _run(
                        () => _service.documents(widget.id, _revision, []),
                      ),
                child: const Text('Reset links and clear old form'),
              ),
            ] else if (_draft != null) ...[
              Card(
                child: ListTile(
                  leading: Icon(
                    complete ? Icons.check_circle : Icons.edit_note,
                    color: complete ? Colors.green : Colors.orange,
                  ),
                  title: Text(
                    complete
                        ? 'Application checklist complete'
                        : 'Application in progress',
                  ),
                  subtitle: Text(
                    widget.rules['readinessScope']?.toString() ?? 'Readiness rules: Indian ordinary passport, Singapore visit. Other nationalities can prepare forms but require a reviewed rule pack. No guarantee of approval or entry.',
                  ),
                ),
              ),
              if (widget.rules['guidance'] != null)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 12),
                  child: Text(widget.rules['guidance'].toString()),
                ),
              Wrap(
                spacing: 8,
                children: [
                  OutlinedButton.icon(
                    onPressed: _busy || _fieldsDirty || _checksDirty
                        ? null
                        : _documents,
                    icon: const Icon(Icons.attach_file),
                    label: Text('Documents (${_docs.length})'),
                  ),
                  OutlinedButton.icon(
                    onPressed: _busy || _fieldsDirty || _checksDirty
                        ? null
                        : () => _run(
                            () => _service.action(
                              widget.id,
                              _revision,
                              'prefill',
                            ),
                          ),
                    icon: const Icon(Icons.auto_awesome),
                    label: const Text('Prefill from documents'),
                  ),
                ],
              ),
              ..._docs.map((d) => Text('${d['role']}: ${d['name']}')),
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Text(
                  'Prefill uses existing local analysis. If a value is unavailable, enter it or ask the document owner to finish analysis. Every suggestion needs your review.',
                ),
              ),
              ...sections.map(
                (section) => ExpansionTile(
                  key: PageStorageKey(section),
                  title: Text(section),
                  initiallyExpanded: section == 'Applicant',
                  childrenPadding: const EdgeInsets.all(12),
                  children: fields
                      .where((f) => f['section'] == section)
                      .map(_field)
                      .toList(),
                ),
              ),
              const SizedBox(height: 20),
              const Text(
                'Submission checklist',
                style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
              ),
              if (_fieldsDirty)
                const Text(
                  'Save your fields first. Changing form details clears previous checklist confirmations.',
                ),
              ...(widget.rules['checks'] as List)
                  .cast<Map<String, dynamic>>()
                  .where((c) => _active(c['when'] as String?))
                  .map(_check),
              ExpansionTile(
                title: Text(
                  'Items to resolve (${(assessment['blockers'] as List? ?? []).length})',
                ),
                children: (assessment['blockers'] as List? ?? [])
                    .map(
                      (b) => ListTile(
                        leading: const Icon(Icons.info_outline),
                        title: Text(b.toString()),
                      ),
                    )
                    .toList(),
              ),
              const SizedBox(height: 16),
              if (_supportsSgPdf) ...[
                const Text(
                  'Download, review and sign',
                  style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
                ),
                const Text(
                  'Exported forms remain editable. Signatures and signing dates are left blank. Upload the signed forms to Documents, link them here and complete the checklist. Downloaded copies cannot be withdrawn when sharing ends.',
                ),
                Wrap(
                  spacing: 8,
                  children: [
                    OutlinedButton(
                      onPressed: _busy || _fieldsDirty || _checksDirty
                          ? null
                          : () => _export('14a'),
                      child: const Text('Download Form 14A'),
                    ),
                    if (_values['loiRequired'] == 'Yes')
                      OutlinedButton(
                        onPressed: _busy || _fieldsDirty || _checksDirty
                            ? null
                            : () => _export('v39a'),
                        child: const Text('Download V39A'),
                      ),
                  ],
                ),
              ] else
                const Text(
                  'Complete the official application using the authority links below. This preparation checklist does not generate or submit a government form.',
                ),
              if (complete && _draft?['status'] != 'Submitted')
                OutlinedButton(
                  onPressed: _busy
                      ? null
                      : () => _run(
                          () => _service.action(
                            widget.id,
                            _revision,
                            'mark-submitted',
                          ),
                        ),
                  child: const Text(
                    'I have submitted through my filing contact',
                  ),
                ),
              const SizedBox(height: 16),
              const Text(
                'Travel readiness is separate',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
              Text(assessment['travelNote']?.toString() ?? ''),
              const SizedBox(height: 12),
              Text(
                'Guidance checked ${widget.rules['reviewedOn']}. Recheck due ${widget.rules['reviewUntil']}.',
              ),
              ...(widget.rules['sources'] as List).map(
                (s) => TextButton(
                  onPressed: () => launchUrl(
                    Uri.parse(s['url'] as String),
                    mode: LaunchMode.externalApplication,
                  ),
                  child: Text(s['title'] as String),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
