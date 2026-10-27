import 'package:flutter/material.dart';
import '../../core/services/corporate_service.dart';

/// HR view of explicitly recorded issued permits, not visa application drafts.
class CorporatePermitsDialog extends StatefulWidget {
  final CorporateService service;
  final String companyId;
  final List<Map<String, dynamic>> employees;
  const CorporatePermitsDialog({
    super.key, required this.service, required this.companyId,
    required this.employees,
  });

  @override
  State<CorporatePermitsDialog> createState() => _CorporatePermitsDialogState();
}

class _CorporatePermitsDialogState extends State<CorporatePermitsDialog> {
  List<Map<String, dynamic>> permits = [];
  bool loading = true;
  String? error;
  @override
  void initState() { super.initState(); refresh(); }

  Future<void> refresh() async {
    setState(() { loading = true; error = null; });
    try {
      final result = await widget.service.issuedPermits(widget.companyId);
      if (mounted) setState(() => permits = result);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> addPermit() async {
    final country = TextEditingController();
    final type = TextEditingController();
    final reference = TextEditingController();
    String? employeeId;
    DateTime? expiry;
    try {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (ctx) => StatefulBuilder(builder: (ctx, update) => AlertDialog(
          title: const Text('Record issued visa or pass'),
          content: SizedBox(width: 420, child: SingleChildScrollView(child: Column(
            mainAxisSize: MainAxisSize.min, children: [
              const Text('Only record an issued permit with a verified expiry date. '
                  'Visa applications are not proof of issuance.'),
              DropdownButtonFormField<String>(
                decoration: const InputDecoration(labelText: 'Employee'),
                items: widget.employees.where((e) =>
                    e['status'] == 'Active' &&
                    (e['role'] == 'Employee' || e['role'] == 'Viewer'))
                  .map((e) => DropdownMenuItem(
                    value: e['id'].toString(),
                    child: Text(e['displayName']?.toString() ??
                        e['email']?.toString() ?? 'Employee'),
                  )).toList(),
                onChanged: (v) => update(() => employeeId = v),
              ),
              TextField(controller: country,
                  decoration: const InputDecoration(labelText: 'Country code (e.g. MY)')),
              TextField(controller: type,
                  decoration: const InputDecoration(labelText: 'Permit type')),
              TextField(controller: reference,
                  decoration: const InputDecoration(labelText: 'Reference (optional)')),
              const SizedBox(height: 12),
              Text('Expiry: ${expiry == null ? "Not selected" : expiry!.toIso8601String().split("T").first}'),
              TextButton(
                onPressed: () async {
                  final d = await showDatePicker(
                    context: ctx, initialDate: DateTime.now(),
                    firstDate: DateTime(2000), lastDate: DateTime(2100));
                  if (d != null) update(() => expiry = d);
                },
                child: const Text('Choose verified expiry date'),
              ),
            ],
          ))),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false),
                child: const Text('Cancel')),
            FilledButton(
              onPressed: employeeId == null || expiry == null ||
                  country.text.trim().length != 2 || type.text.trim().isEmpty
                  ? null : () => Navigator.pop(ctx, true),
              child: const Text('Record permit'),
            ),
          ],
        )),
      );
      if (confirmed != true || !mounted) return;
      await widget.service.recordIssuedPermit(widget.companyId, {
        'employeeUserId': employeeId,
        'countryCode': country.text.trim().toUpperCase(),
        'permitType': type.text.trim(),
        'reference': reference.text.trim(),
        'expiresOn': expiry!.toIso8601String(),
      });
      await refresh();
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Unable to record permit: $e')));
    } finally {
      country.dispose(); type.dispose(); reference.dispose();
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Issued visas & passes'),
    content: SizedBox(width: 580, height: 420, child:
      loading ? const Center(child: CircularProgressIndicator()) :
      error != null ? Text('Unable to load permits: $error') :
      permits.isEmpty ? const Text('No issued permits recorded.') :
      ListView.builder(
        itemCount: permits.length,
        itemBuilder: (ctx, i) {
          final p = permits[i];
          final category = p['expiryCategory']?.toString() ?? 'Unknown';
          final employee = widget.employees.where((e) =>
              e['id'].toString() == p['employeeUserId'].toString());
          final name = employee.isEmpty ? 'Employee' :
              (employee.first['displayName']?.toString() ??
               employee.first['email']?.toString() ?? 'Employee');
          return ListTile(
            title: Text('$name • ${p["permitType"]}'),
            subtitle: Text('${p["countryCode"]} • Expires ${p["expiresOn"]?.toString().split("T").first}'),
            trailing: Text(category),
          );
        },
      )),
    actions: [
      TextButton(onPressed: loading ? null : refresh, child: const Text('Refresh')),
      FilledButton.icon(onPressed: addPermit, icon: const Icon(Icons.add),
          label: const Text('Record permit')),
      TextButton(onPressed: () => Navigator.pop(context), child: const Text('Close')),
    ],
  );
}
