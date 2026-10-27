import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/services/corporate_service.dart';
import 'business_dashboard.dart';
import 'offer_letter_draft_dialog.dart';
import 'employee_offers_dialog.dart';

class CorporateScreen extends StatefulWidget {
  final CorporateService service;
  const CorporateScreen({super.key, required this.service});
  @override
  State<CorporateScreen> createState() => _CorporateScreenState();
}

class _CorporateScreenState extends State<CorporateScreen> {
  bool loading = true;
  String? error;
  List<Map<String, dynamic>> companies = [], invitations = [];
  Map<String, dynamic>? selected, metrics;
  List<Map<String, dynamic>> employees = [], cases = [];
  bool get isHr =>
      selected?['role'] == 'Admin' ||
      selected?['role'] == 'Owner' ||
      selected?['role'] == 'HR';
  bool get isOwner =>
      selected?['role'] == 'Admin' || selected?['role'] == 'Owner';
  String get companyId => selected?['id']?.toString() ?? '';
  @override
  void initState() {
    super.initState();
    reload();
  }

  Future<void> reload() async {
    setState(() {
      loading = true;
      error = null;
    });
    try {
      final cs = await widget.service.companies();
      final invites = await widget.service.invitations();
      Map<String, dynamic>? active;
      for (final c in cs) {
        if (c['id'] == selected?['id']) {
          active = c;
          break;
        }
      }
      active ??= cs.isNotEmpty ? cs.first : null;
      final id = active?['id']?.toString();
      final es =
          id != null &&
              (active?['role'] == 'Owner' ||
                  active?['role'] == 'Admin' ||
                  active?['role'] == 'HR')
          ? await widget.service.employees(id)
          : <Map<String, dynamic>>[];
      final ks = id != null
          ? await widget.service.cases(id)
          : <Map<String, dynamic>>[];
      final ms =
          id != null &&
              (active?['role'] == 'Owner' ||
                  active?['role'] == 'Admin' ||
                  active?['role'] == 'HR')
          ? await widget.service.dashboard(id)
          : null;
      if (mounted)
        setState(() {
          companies = cs;
          invitations = invites;
          selected = active;
          employees = es;
          cases = ks;
          metrics = ms;
          loading = false;
        });
    } catch (e) {
      if (mounted)
        setState(() {
          loading = false;
          error = e.toString();
        });
    }
  }

  Future<String?> ask(String title, String label) async {
    final c = TextEditingController();
    try {
      return await showDialog<String>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: Text(title),
          content: TextField(
            controller: c,
            autofocus: true,
            decoration: InputDecoration(labelText: label),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(ctx, c.text.trim()),
              child: const Text('Continue'),
            ),
          ],
        ),
      );
    } finally {
      c.dispose();
    }
  }

  Future<void> action(Future<void> Function() fn) async {
    try {
      await fn();
      if (mounted) await reload();
    } catch (e) {
      if (mounted)
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text('Action failed: $e')));
    }
  }

  Widget panel(String title, Widget child) => Card(
    child: Padding(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 10),
          child,
        ],
      ),
    ),
  );
  @override
  Widget build(BuildContext context) {
    if (companies.isEmpty) return legacyScreen(context);
    return BusinessDashboard(
      companies: companies,
      selected: selected!,
      employees: employees,
      tasks: cases,
      metrics: metrics,
      isHr: isHr,
      isOwner: isOwner,
      loading: loading,
      error: error,
      onRefresh: reload,
      onMyOffers: () async {
        await showDialog<void>(
          context: context,
          builder: (_) => EmployeeOffersDialog(service: widget.service),
        );
      },
      onCompany: (id) {
        setState(() {
          selected = companies.firstWhere((c) => c['id'].toString() == id);
        });
        reload();
      },
      onInvite: inviteMember,
      onEditMember: editMember,
      onDraftOffer: (employee) async {
        final name = employee['displayName']?.toString().trim();
        await showDialog<void>(
          context: context,
          builder: (ctx) => OfferLetterDraftDialog(
            onIssue: (renderedText) async {
              final employeeId = employee['id']?.toString() ?? '';
              if (employeeId.isEmpty || companyId.isEmpty) {
                throw StateError('Employee or company ID is missing.');
              }
              await widget.service.issueOffer(companyId, employeeId, renderedText);
              if (mounted) {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Offer issued to employee Document Wallet')),
                );
              }
            },
            employeeName: name == null || name.isEmpty
                ? (employee['email']?.toString() ?? '')
                : name,
            companyName: selected?['name']?.toString() ?? '',
            employmentCountry: employee['employmentCountry']?.toString() ?? '',
            startDate: employee['employmentStartDate']?.toString(),
          ),
        );
      },
      onNewTask: () => action(createCase),
      taskActions: taskActions,
      invitations: invitations
          .map(
            (i) => Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Invitation: ${i['name'] ?? 'Company'}'),
                    Wrap(
                      spacing: 8,
                      children: [
                        TextButton(
                          onPressed: () => action(() async {
                            await widget.service.decline(i['id'].toString());
                          }),
                          child: const Text('Decline'),
                        ),
                        FilledButton(
                          onPressed: () => action(() async {
                            await widget.service.accept(i['id'].toString());
                          }),
                          child: const Text('Accept'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          )
          .toList(),
    );
  }

  Widget taskActions(Map<String, dynamic> c) => PopupMenuButton<String>(
    tooltip: 'Task actions',
    onSelected: (v) => action(() async {
      if (v == 'Edit task') {
        await editCase(c);
      } else if (v == 'View visa') {
        await viewVisa(c);
      } else if (v == 'Link visa') {
        await selectAndLinkVisa(c);
      } else if (v == 'Unlink visa') {
        await widget.service.unlinkVisa(companyId, c['id'].toString());
      } else {
        await widget.service.updateCase(companyId, c['id'].toString(), {
          'status': v,
          'dueAt': c['dueAt'],
          'notes': c['notes'],
        });
      }
    }),
    itemBuilder: (_) => [
      if (isHr)
        const PopupMenuItem(
          value: 'Edit task',
          child: Text('Edit application'),
        ),
      if (isHr && c['visaApplicationId'] != null)
        const PopupMenuItem(
          value: 'View visa',
          child: Text('View linked visa (read-only)'),
        ),
      if (isHr)
        ...[
          'Open',
          'InProgress',
          'Blocked',
          'Closed',
        ].map((v) => PopupMenuItem(value: v, child: Text('Set $v'))),
      if (!isHr)
        const PopupMenuItem(
          value: 'Link visa',
          child: Text('Link My Visa Application'),
        ),
      if (!isHr && c['visaApplicationId'] != null)
        const PopupMenuItem(
          value: 'Unlink visa',
          child: Text('Unlink My Visa Application'),
        ),
    ],
  );

  Widget legacyScreen(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: const Text('ExpatOne Business'),
      actions: [IconButton(onPressed: reload, icon: const Icon(Icons.refresh))],
    ),
    body: loading
        ? const Center(child: CircularProgressIndicator())
        : error != null
        ? Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(error!),
                TextButton(onPressed: reload, child: const Text('Retry')),
              ],
            ),
          )
        : ListView(
            padding: const EdgeInsets.all(12),
            children: [
              if (companies.isEmpty)
                panel(
                  'Individual account',
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'You have no active company membership. Your personal ExpatOne features remain private.',
                      ),
                      const SizedBox(height: 8),
                      FilledButton.icon(
                        onPressed: () => action(() async {
                          final name = await ask(
                            'Create company',
                            'Company name',
                          );
                          if (name != null && name.isNotEmpty)
                            await widget.service.createCompany(name);
                        }),
                        icon: const Icon(Icons.add_business),
                        label: const Text('Register a company'),
                      ),
                    ],
                  ),
                ),
              if (invitations.isNotEmpty)
                panel(
                  'Company invitations',
                  Column(
                    children: invitations
                        .map(
                          (i) => ListTile(
                            title: Text(i['name']?.toString() ?? 'Company'),
                            subtitle: const Text(
                              'Invitation pending — no company access until accepted',
                            ),
                            trailing: Wrap(
                              spacing: 4,
                              children: [
                                TextButton(
                                  onPressed: () => action(() async {
                                    await widget.service.decline(
                                      i['id'].toString(),
                                    );
                                  }),
                                  child: const Text('Decline'),
                                ),
                                FilledButton(
                                  onPressed: () => action(() async {
                                    await widget.service.accept(
                                      i['id'].toString(),
                                    );
                                  }),
                                  child: const Text('Accept'),
                                ),
                              ],
                            ),
                          ),
                        )
                        .toList(),
                  ),
                ),
            ],
          ),
  );
  Future<void> inviteMember() async {
    final email = TextEditingController();
    String role = 'Viewer';
    List<Map<String, dynamic>> matches = [];
    bool searching = false;
    String? searchError;
    try {
      final ok = await showDialog<bool>(
        context: context,
        builder: (ctx) => StatefulBuilder(
          builder: (ctx, setDialog) => AlertDialog(
            title: const Text('Invite company member'),
            content: SizedBox(
              width: 460,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    TextField(
                      controller: email,
                      keyboardType: TextInputType.emailAddress,
                      decoration: const InputDecoration(
                        labelText: 'Search name or enter full email',
                        helperText: 'Search registered users using 3+ characters',
                      ),
                    ),
                    const SizedBox(height: 8),
                    Align(
                      alignment: Alignment.centerLeft,
                      child: OutlinedButton.icon(
                        onPressed: searching ? null : () async {
                          final term = email.text.trim();
                          if (term.length < 3) {
                            setDialog(() => searchError = 'Enter at least 3 characters.');
                            return;
                          }
                          setDialog(() {
                            searching = true;
                            searchError = null;
                            matches = [];
                          });
                          try {
                            final results = await widget.service
                                .searchRegisteredEmployees(companyId, term);
                            if (!ctx.mounted) return;
                            setDialog(() => matches = results);
                          } catch (_) {
                            if (!ctx.mounted) return;
                            setDialog(() => searchError =
                                'Search failed. You can still invite using a full email.');
                          } finally {
                            if (ctx.mounted) setDialog(() => searching = false);
                          }
                        },
                        icon: const Icon(Icons.search),
                        label: Text(searching ? 'Searching...' : 'Find employee'),
                      ),
                    ),
                    if (searchError != null)
                      Text(searchError!, style: const TextStyle(color: Colors.red)),
                    if (matches.isNotEmpty)
                      ConstrainedBox(
                        constraints: const BoxConstraints(maxHeight: 190),
                        child: ListView.builder(
                          shrinkWrap: true,
                          itemCount: matches.length,
                          itemBuilder: (context, index) {
                            final match = matches[index];
                            final candidate = match['email']?.toString() ?? '';
                            return ListTile(
                              dense: true,
                              title: Text(match['displayName']?.toString() ?? candidate),
                              subtitle: Text(candidate),
                              onTap: () => setDialog(() {
                                email.text = candidate;
                                matches = [];
                              }),
                            );
                          },
                        ),
                      ),
                    const SizedBox(height: 8),
                    DropdownButtonFormField<String>(
                      initialValue: role,
                      decoration: const InputDecoration(labelText: 'Role'),
                      items: [
                        const DropdownMenuItem(
                          value: 'Viewer',
                          child: Text('Corporate Employee'),
                        ),
                        if (isOwner)
                          const DropdownMenuItem(
                            value: 'HR',
                            child: Text('HR Administrator'),
                          ),
                      ],
                      onChanged: (v) => setDialog(() => role = v ?? 'Viewer'),
                    ),
                  ],
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(ctx, false),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: () => Navigator.pop(ctx, true),
                child: const Text('Invite'),
              ),
            ],
          ),
        ),
      );
      if (ok == true && email.text.trim().isNotEmpty) {
        await action(() async {
          await widget.service.invite(companyId, email.text.trim(), role);
        });
      }
    } finally {
      email.dispose();
    }
  }

  Future<void> editMember(Map<String, dynamic> e) async {
    String role = e['role']?.toString() ?? 'Viewer';
    String status = e['status'] == 'Active' ? 'Active' : 'Inactive';
    String classification = e['workforceClassification']?.toString() ?? 'Unknown';
    if (!['Local', 'Foreign', 'Unknown'].contains(classification)) {
      classification = 'Unknown';
    }
    final nationality = TextEditingController(
      text: e['nationality']?.toString() ?? '',
    );
    final employmentCountry = TextEditingController(
      text: e['employmentCountry']?.toString() ?? '',
    );
    DateTime? startDate = DateTime.tryParse(
      e['employmentStartDate']?.toString() ?? '',
    );
    try {
      final ok = await showDialog<bool>(
        context: context,
        builder: (ctx) => StatefulBuilder(
          builder: (ctx, update) => AlertDialog(
            title: Text('Edit ${e['email']}'),
            content: SizedBox(
              width: 460,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    DropdownButtonFormField<String>(
                      initialValue: role,
                      decoration: const InputDecoration(labelText: 'Corporate role'),
                      items: const [
                        DropdownMenuItem(value: 'Viewer', child: Text('Corporate Employee')),
                        DropdownMenuItem(value: 'HR', child: Text('HR Admin')),
                        DropdownMenuItem(value: 'Admin', child: Text('Company Admin')),
                      ],
                      onChanged: (v) => update(() => role = v ?? role),
                    ),
                    DropdownButtonFormField<String>(
                      initialValue: status,
                      decoration: const InputDecoration(labelText: 'Status'),
                      items: const [
                        DropdownMenuItem(value: 'Active', child: Text('Active')),
                        DropdownMenuItem(value: 'Inactive', child: Text('Inactive')),
                      ],
                      onChanged: (v) => update(() => status = v ?? status),
                    ),
                    DropdownButtonFormField<String>(
                      initialValue: classification,
                      decoration: const InputDecoration(labelText: 'Workforce classification'),
                      items: const [
                        DropdownMenuItem(value: 'Unknown', child: Text('Unknown')),
                        DropdownMenuItem(value: 'Local', child: Text('Local')),
                        DropdownMenuItem(value: 'Foreign', child: Text('Foreign')),
                      ],
                      onChanged: (v) => update(() => classification = v ?? classification),
                    ),
                    TextField(
                      controller: nationality,
                      maxLength: 100,
                      decoration: const InputDecoration(labelText: 'Nationality (optional)'),
                    ),
                    TextField(
                      controller: employmentCountry,
                      maxLength: 100,
                      decoration: const InputDecoration(labelText: 'Employment country (optional)'),
                    ),
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      title: const Text('Employment start date'),
                      subtitle: Text(startDate == null
                          ? 'Not set'
                          : '${startDate!.year}-${startDate!.month.toString().padLeft(2, '0')}-${startDate!.day.toString().padLeft(2, '0')}'),
                      trailing: IconButton(
                        icon: const Icon(Icons.calendar_month_outlined),
                        onPressed: () async {
                          final selected = await showDatePicker(
                            context: ctx,
                            initialDate: startDate ?? DateTime.now(),
                            firstDate: DateTime(1900),
                            lastDate: DateTime(2200),
                          );
                          if (selected != null) update(() => startDate = selected);
                        },
                      ),
                    ),
                    TextButton(
                      onPressed: () => update(() => startDate = null),
                      child: const Text('Clear start date'),
                    ),
                    const Text(
                      'Inactive preserves membership and case records for audit.',
                      style: TextStyle(fontSize: 12),
                    ),
                  ],
                ),
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(ctx, false),
                child: const Text('Cancel'),
              ),
              FilledButton(
                onPressed: () => Navigator.pop(ctx, true),
                child: const Text('Save'),
              ),
            ],
          ),
        ),
      );
      if (ok != true) return;
      final id = (e['membershipId'] ?? e['MembershipId'])?.toString().trim();
      if (id == null || id.isEmpty || id == 'null') {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Membership ID missing. Refresh and try again.')),
        );
        return;
      }
      final previousDate = DateTime.tryParse(e['employmentStartDate']?.toString() ?? '');
      final workforceChanged =
          classification != (e['workforceClassification'] ?? 'Unknown') ||
          nationality.text.trim() != (e['nationality'] ?? '') ||
          employmentCountry.text.trim() != (e['employmentCountry'] ?? '') ||
          startDate?.year != previousDate?.year ||
          startDate?.month != previousDate?.month ||
          startDate?.day != previousDate?.day;
      if (role == e['role'] && status == e['status'] && !workforceChanged) return;
      await action(() async {
        if (role != e['role']) {
          await widget.service.memberRole(companyId, id, role);
        }
        if (status != e['status']) {
          await widget.service.memberStatus(companyId, id, status);
        }
        if (workforceChanged) {
          await widget.service.updateWorkforce(
            companyId, id,
            classification: classification,
            nationality: nationality.text.trim(),
            employmentCountry: employmentCountry.text.trim(),
            employmentStartDate: startDate,
          );
        }
      });
    } finally {
      nationality.dispose();
      employmentCountry.dispose();
    }
  }

  // Lists only applications owned by the signed-in user. The backend also
  // validates visa ownership and corporate task assignment when linking.
  Future<void> selectAndLinkVisa(Map<String, dynamic> corporateCase) async {
    final applications = await widget.service.myVisaApplications();
    if (!mounted) return;
    if (applications.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'No visa applications found. Save one in Application Assistant first.',
          ),
        ),
      );
      return;
    }
    final catalog = await widget.service.catalog();
    if (!mounted) return;
    final templates = (catalog['templates'] as List? ?? [])
        .whereType<Map>()
        .toList();
    final countries = (catalog['countries'] as List? ?? [])
        .whereType<Map>()
        .toList();

    String labelFor(Map<String, dynamic> app) {
      final templateId = app['templateId']?.toString() ?? '';
      Map? template;
      for (final t in templates) {
        if (t['id']?.toString() == templateId) {
          template = t;
          break;
        }
      }
      final code = template?['countryCode']?.toString() ?? '';
      String country = code;
      for (final c in countries) {
        if (c['code']?.toString() == code) {
          country = c['name']?.toString() ?? code;
          break;
        }
      }
      final purpose = template?['purpose']?.toString() ?? templateId;
      final status = app['status']?.toString() ?? 'Saved';
      final updated = app['updatedAt']?.toString() ?? '';
      final date = updated.length >= 10 ? updated.substring(0, 10) : updated;
      return '${country.isEmpty ? "Visa" : country} · $purpose · $status${date.isEmpty ? "" : " · $date"}';
    }

    final current = corporateCase['visaApplicationId']?.toString();
    String? chosen = applications.any((a) => a['id']?.toString() == current)
        ? current
        : null;
    final selectedId = await showDialog<String>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, update) => AlertDialog(
          title: const Text('Link My Visa Application'),
          content: SizedBox(
            width: 540,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Select one of your saved or submitted visa applications.',
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  isExpanded: true,
                  initialValue: chosen,
                  decoration: const InputDecoration(
                    labelText: 'My Visa Applications',
                    border: OutlineInputBorder(),
                  ),
                  items: applications
                      .map(
                        (a) => DropdownMenuItem<String>(
                          value: a['id'].toString(),
                          child: Text(
                            labelFor(a),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      )
                      .toList(),
                  onChanged: (value) => update(() => chosen = value),
                ),
                const SizedBox(height: 10),
                const Text(
                  'HR can view the latest saved/submitted application and its linked documents read-only.',
                  style: TextStyle(fontSize: 12),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: chosen == null
                  ? null
                  : () => Navigator.pop(ctx, chosen),
              child: const Text('Link application'),
            ),
          ],
        ),
      ),
    );
    if (selectedId != null) {
      await widget.service.linkVisa(
        companyId,
        corporateCase['id'].toString(),
        selectedId,
      );
    }
  }

  Future<void> viewVisa(Map<String, dynamic> c) async {
    try {
      final v = await widget.service.corporateVisa(
        companyId,
        c['id'].toString(),
      );
      if (!mounted) return;
      final values = v['values'] is Map
          ? (v['values'] as Map).entries.toList()
          : <MapEntry<dynamic, dynamic>>[];
      final docs = v['documents'] is List ? v['documents'] as List : [];
      await showDialog<void>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Latest visa application · Read only'),
          content: SizedBox(
            width: 540,
            child: ListView(
              shrinkWrap: true,
              children: [
                Text('Template: ${v['templateId']}'),
                Text('Submitted: ${v['submitted'] == true ? 'Yes' : 'No'}'),
                Text('Last saved: ${v['updatedAt'] ?? ''}'),
                const Divider(),
                const Text(
                  'Saved form values',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
                ...values.map(
                  (x) => ListTile(
                    dense: true,
                    title: Text(x.key.toString()),
                    subtitle: Text(x.value?.toString() ?? ''),
                  ),
                ),
                const Divider(),
                const Text(
                  'Linked supporting documents',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
                if (docs.isEmpty) const Text('No documents attached'),
                ...docs.map(
                  (d) => ListTile(
                    dense: true,
                    leading: const Icon(Icons.insert_drive_file_outlined),
                    title: Text(d['name']?.toString() ?? 'Document'),
                    subtitle: Text(d['role']?.toString() ?? ''),
                    trailing: const Icon(Icons.open_in_new),
                    onTap: () async {
                      try {
                        final result = await widget.service
                            .corporateDocumentUrl(
                              companyId,
                              c['id'].toString(),
                              d['id'].toString(),
                            );
                        final uri = Uri.tryParse(
                          result['url']?.toString() ?? '',
                        );
                        if (uri != null &&
                            (uri.scheme == 'https' || uri.scheme == 'http'))
                          await launchUrl(
                            uri,
                            mode: LaunchMode.externalApplication,
                          );
                      } catch (e) {
                        if (mounted)
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(content: Text('Document unavailable: $e')),
                          );
                      }
                    },
                  ),
                ),
                const Text(
                  'Documents open using short-lived authorized links. HR cannot edit or delete them.',
                  style: TextStyle(fontSize: 12),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('Close'),
            ),
          ],
        ),
      );
    } catch (e) {
      if (mounted)
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text('Visa viewer: $e')));
    }
  }

  Future<void> createCase() async => editCase(null);
  Future<void> editCase(Map<String, dynamic>? existing) async {
    final isNew = existing == null;
    final active = employees
        .where(
          (e) =>
              (e['role'] == 'Employee' || e['role'] == 'Viewer') &&
              e['status'] == 'Active',
        )
        .toList();
    if (isNew && active.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Invite a corporate employee first.')),
      );
      return;
    }
    String? employeeId = isNew
        ? active.first['id']?.toString()
        : existing['employeeUserId']?.toString();
    final title = TextEditingController(
      text: existing?['title']?.toString() ?? '',
    );
    final notes = TextEditingController(
      text: existing?['notes']?.toString() ?? '',
    );
    String status = existing?['status']?.toString() ?? 'Open';
    String priority = existing?['priority']?.toString() ?? 'Normal';
    String? country = existing?['countryCode']?.toString(),
        purpose = existing?['purpose']?.toString();
    String? hr = existing?['assignedHrUserId']?.toString();
    DateTime? due = DateTime.tryParse(existing?['dueAt']?.toString() ?? '');
    try {
      final catalog = await widget.service.catalog();
      final templates = (catalog['templates'] as List? ?? [])
          .whereType<Map>()
          .toList();
      final countries = (catalog['countries'] as List? ?? [])
          .whereType<Map>()
          .where((x) => x['enabled'] == true)
          .toList();
      final hrs = employees
          .where(
            (e) =>
                e['status'] == 'Active' &&
                ['Admin', 'Owner', 'HR'].contains(e['role']),
          )
          .toList();
      if (!mounted) return;
      final ok = await showDialog<bool>(
        context: context,
        builder: (ctx) => StatefulBuilder(
          builder: (ctx, update) {
            final purposes = templates
                .where((t) => t['countryCode'] == country)
                .map((t) => t['purpose']?.toString() ?? '')
                .where((v) => v.isNotEmpty)
                .toSet()
                .toList();
            return AlertDialog(
              title: Text(
                isNew
                    ? 'Create Corporate Application'
                    : 'Edit Corporate Application',
              ),
              content: SizedBox(
                width: 520,
                child: SingleChildScrollView(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      if (isNew)
                        DropdownButtonFormField<String>(
                          initialValue: employeeId,
                          decoration: const InputDecoration(
                            labelText: 'Employee',
                          ),
                          items: active
                              .map(
                                (e) => DropdownMenuItem(
                                  value: e['id'].toString(),
                                  child: Text(
                                    e['displayName']?.toString() ??
                                        e['email'].toString(),
                                  ),
                                ),
                              )
                              .toList(),
                          onChanged: (v) => update(() => employeeId = v),
                        )
                      else
                        ListTile(
                          title: const Text('Employee (locked)'),
                          subtitle: Text(employeeId ?? ''),
                        ),
                      ListTile(
                        title: const Text('Company (locked)'),
                        subtitle: Text(selected?['name']?.toString() ?? ''),
                      ),
                      TextField(
                        controller: title,
                        decoration: const InputDecoration(
                          labelText: 'Application title',
                        ),
                      ),
                      DropdownButtonFormField<String>(
                        initialValue: countries.any((x) => x['code'] == country)
                            ? country
                            : null,
                        decoration: const InputDecoration(
                          labelText: 'Destination country',
                        ),
                        items: countries
                            .map(
                              (e) => DropdownMenuItem(
                                value: e['code'].toString(),
                                child: Text(e['name'].toString()),
                              ),
                            )
                            .toList(),
                        onChanged: (v) => update(() {
                          country = v;
                          purpose = null;
                        }),
                      ),
                      DropdownButtonFormField<String>(
                        initialValue: purposes.contains(purpose)
                            ? purpose
                            : null,
                        decoration: const InputDecoration(labelText: 'Purpose'),
                        items: purposes
                            .map(
                              (e) => DropdownMenuItem(value: e, child: Text(e)),
                            )
                            .toList(),
                        onChanged: (v) => update(() => purpose = v),
                      ),
                      DropdownButtonFormField<String>(
                        initialValue: priority,
                        decoration: const InputDecoration(
                          labelText: 'Priority',
                        ),
                        items: ['Normal', 'High', 'Urgent']
                            .map(
                              (e) => DropdownMenuItem(value: e, child: Text(e)),
                            )
                            .toList(),
                        onChanged: (v) =>
                            update(() => priority = v ?? priority),
                      ),
                      DropdownButtonFormField<String>(
                        initialValue: hrs.any((e) => e['id'].toString() == hr)
                            ? hr
                            : null,
                        decoration: const InputDecoration(
                          labelText: 'Assigned HR officer',
                        ),
                        items: [
                          const DropdownMenuItem<String>(
                            value: null,
                            child: Text('Unassigned'),
                          ),
                          ...hrs.map(
                            (e) => DropdownMenuItem(
                              value: e['id'].toString(),
                              child: Text(
                                e['displayName']?.toString() ??
                                    e['email'].toString(),
                              ),
                            ),
                          ),
                        ],
                        onChanged: (v) => update(() => hr = v),
                      ),
                      DropdownButtonFormField<String>(
                        initialValue: status,
                        decoration: const InputDecoration(
                          labelText: 'Corporate task status',
                        ),
                        items: ['Open', 'InProgress', 'Blocked', 'Closed']
                            .map(
                              (e) => DropdownMenuItem(value: e, child: Text(e)),
                            )
                            .toList(),
                        onChanged: (v) => update(() => status = v ?? status),
                      ),
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              'Due: ${due?.toLocal().toString().split(' ').first ?? 'Not set'}',
                            ),
                          ),
                          TextButton(
                            onPressed: () async {
                              final picked = await showDatePicker(
                                context: ctx,
                                initialDate: due ?? DateTime.now(),
                                firstDate: DateTime(2020),
                                lastDate: DateTime(2100),
                              );
                              if (picked != null) update(() => due = picked);
                            },
                            child: const Text('Choose date'),
                          ),
                        ],
                      ),
                      TextField(
                        controller: notes,
                        maxLines: 3,
                        decoration: const InputDecoration(labelText: 'Notes'),
                      ),
                    ],
                  ),
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.pop(ctx, false),
                  child: const Text('Cancel'),
                ),
                FilledButton(
                  onPressed: () => Navigator.pop(ctx, true),
                  child: const Text('Save'),
                ),
              ],
            );
          },
        ),
      );
      if (ok == true && employeeId != null && title.text.trim().isNotEmpty) {
        final data = <String, dynamic>{
          'title': title.text.trim(),
          'status': status,
          'dueAt': due?.toUtc().toIso8601String(),
          'notes': notes.text,
          'countryCode': country,
          'purpose': purpose,
          'priority': priority,
          'assignedHrUserId': hr,
        };
        if (isNew) data['employeeUserId'] = employeeId;
        await action(() async {
          if (isNew) {
            await widget.service.createCase(companyId, data);
          } else {
            await widget.service.updateCase(
              companyId,
              existing['id'].toString(),
              data,
            );
          }
        });
      }
    } finally {
      title.dispose();
      notes.dispose();
    }
  }
}
