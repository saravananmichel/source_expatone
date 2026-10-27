import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../core/services/corporate_service.dart';

/// Employee-only list of issued offers. The document itself is read via the
/// existing authenticated Document Wallet access-url endpoint.
class EmployeeOffersDialog extends StatefulWidget {
  final CorporateService service;
  const EmployeeOffersDialog({super.key, required this.service});

  @override
  State<EmployeeOffersDialog> createState() => _EmployeeOffersDialogState();
}

class _EmployeeOffersDialogState extends State<EmployeeOffersDialog> {
  bool loading = true;
  String? error;
  String? respondingId;
  List<Map<String, dynamic>> offers = [];

  @override
  void initState() {
    super.initState();
    refresh();
  }

  Future<void> refresh() async {
    setState(() { loading = true; error = null; });
    try {
      final result = await widget.service.myOffers();
      if (mounted) setState(() => offers = result);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => loading = false);
    }
  }

  Future<void> openPdf(String documentId) async {
    try {
      final response = await widget.service.offerDocumentUrl(documentId);
      final url = Uri.tryParse(response['url']?.toString() ?? '');
      if (url == null || url.scheme != 'https') {
        throw StateError('Secure PDF link unavailable.');
      }
      if (!await launchUrl(url, mode: LaunchMode.externalApplication)) {
        throw StateError('Unable to open PDF.');
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Unable to open offer: $e')));
      }
    }
  }

  Future<void> respond(String id, String response) async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('$response offer?'),
        content: const Text('Your response will be recorded and cannot be changed here. '
            'This is an acknowledgement, not an electronic signature.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false),
              child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.pop(ctx, true),
              child: Text(response)),
        ],
      ),
    );
    if (confirm != true || !mounted) return;
    setState(() => respondingId = id);
    try {
      await widget.service.respondToOffer(id, response);
      await refresh();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Could not record response: $e')));
      }
    } finally {
      if (mounted) setState(() => respondingId = null);
    }
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('My employment offers'),
    content: SizedBox(
      width: 540,
      child: loading
          ? const Center(child: CircularProgressIndicator())
          : error != null
              ? Text('Could not load offers: $error')
              : offers.isEmpty
                  ? const Text('No issued employment offers yet.')
                  : ListView.builder(
                      shrinkWrap: true,
                      itemCount: offers.length,
                      itemBuilder: (context, index) {
                        final offer = offers[index];
                        final id = offer['id']?.toString() ?? '';
                        final documentId = offer['documentId']?.toString() ?? '';
                        final status = offer['status']?.toString() ?? 'Unknown';
                        final issuedAt = offer['issuedAt']?.toString().split('T').first ?? '';
                        final busy = respondingId != null;
                        return Card(child: Padding(
                          padding: const EdgeInsets.all(12),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Employment offer v${offer['version'] ?? 1} • $status',
                                  style: Theme.of(context).textTheme.titleSmall),
                              Text('Issued: $issuedAt'),
                              Wrap(spacing: 8, children: [
                                OutlinedButton.icon(
                                  onPressed: documentId.isEmpty ? null : () => openPdf(documentId),
                                  icon: const Icon(Icons.picture_as_pdf_outlined),
                                  label: const Text('View PDF'),
                                ),
                                if (status == 'Issued') ...[
                                  FilledButton(
                                    onPressed: busy ? null : () => respond(id, 'Accepted'),
                                    child: const Text('Accept'),
                                  ),
                                  OutlinedButton(
                                    onPressed: busy ? null : () => respond(id, 'Declined'),
                                    child: const Text('Decline'),
                                  ),
                                ],
                              ]),
                            ],
                          ),
                        ));
                      },
                    ),
    ),
    actions: [
      TextButton(onPressed: loading ? null : refresh, child: const Text('Refresh')),
      TextButton(onPressed: () => Navigator.pop(context), child: const Text('Close')),
    ],
  );
}
