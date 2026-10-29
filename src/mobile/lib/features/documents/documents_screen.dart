import 'package:flutter/material.dart';
import '../../core/services/document_service.dart';
import '../../core/services/reminder_service.dart';
import '../../core/theme/app_theme.dart';
import 'add_document_screen.dart';
import 'document_detail_screen.dart';
import '../family/family_screen.dart';
import '../applications/application_assistant_screen.dart';
import '../../core/services/family_service.dart';
import 'package:url_launcher/url_launcher.dart';

class DocumentsScreen extends StatefulWidget {
  final DocumentService documentService;
  final ReminderService? reminderService;

  const DocumentsScreen({super.key, required this.documentService, this.reminderService});

  @override
  State<DocumentsScreen> createState() => _DocumentsScreenState();
}

class _DocumentsScreenState extends State<DocumentsScreen> {
  final Set<String> _selected = {};
  List<DocumentItem>? _documents;
  bool _isLoading = true;
  String? _error;
  int _walletTab = 0;
  List<SharedDocument>? _sharedWithMe;
  List<FamilyRequest>? _familyRequests;
  String? _tabError;

  @override
  void initState() {
    super.initState();
    _loadDocuments();
    _loadWalletExtras();
  }

  Future<void> _loadWalletExtras() async {
    try {
      final shared = await widget.documentService.getSharedWithMe();
      final requests = await widget.documentService.familyService.requests();
      if (mounted) setState(() { _sharedWithMe = shared; _familyRequests = requests; _tabError = null; });
    } catch (_) {
      if (mounted) setState(() => _tabError = 'Unable to load sharing information. Pull to refresh or retry.');
    }
  }

  Future<void> _loadDocuments() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    try {
      final docs = await widget.documentService.getDocuments();
      if (mounted) {
        setState(() {
          _documents = docs;
          _selected.removeWhere((id) => !docs.any((d) => d.id == id));
          _isLoading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = 'Unable to load documents';
          _isLoading = false;
        });
      }
    }
  }

  void _navigateToAdd() async {
    final result = await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => AddDocumentScreen(documentService: widget.documentService),
      ),
    );
    if (result == true) _loadDocuments();
  }

  void _navigateToDetail(DocumentItem doc) async {
    final deleted = await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => DocumentDetailScreen(
          document: doc,
          documentService: widget.documentService,
          reminderService: widget.reminderService,
        ),
      ),
    );
    if (deleted == true) _loadDocuments();
  }

  @override
  Widget build(BuildContext context) {
    const labels = ['My Documents', 'Shared With Me', 'Shared By Me', 'Requests'];
    return Scaffold(
      appBar: AppBar(
        title: Text(_selected.isEmpty ? 'Document Wallet' : '${_selected.length} selected'),
        actions: [
          IconButton(icon: const Icon(Icons.assignment_outlined), tooltip: 'Prepare application',
            onPressed: _selected.isEmpty || _isLoading || _error != null ? null : () async {
              await Navigator.push(context, MaterialPageRoute(builder: (_) =>
                ApplicationAssistantScreen(documentService: widget.documentService, selectedDocumentIds: _selected.toList())));
              if (mounted) _loadDocuments();
            }),
          IconButton(icon: const Icon(Icons.family_restroom_outlined), tooltip: 'Friends & Family',
            onPressed: () async {
              await Navigator.push(context, MaterialPageRoute(builder: (_) =>
                FamilyScreen(documentService: widget.documentService)));
              if (mounted) { _loadDocuments(); _loadWalletExtras(); }
            }),
          IconButton(icon: const Icon(Icons.refresh), tooltip: 'Refresh wallet',
            onPressed: () { _loadDocuments(); _loadWalletExtras(); }),
          IconButton(icon: const Icon(Icons.add_circle_outline), tooltip: 'Add document',
            onPressed: _navigateToAdd),
        ],
      ),
      body: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Padding(padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: Text('Secure documents and family sharing',
            style: Theme.of(context).textTheme.bodyMedium)),
        SingleChildScrollView(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          scrollDirection: Axis.horizontal,
          child: Row(children: [
            for (var i = 0; i < labels.length; i++)
              Padding(padding: const EdgeInsets.only(right: 8),
                child: ChoiceChip(label: Text(labels[i]), selected: _walletTab == i,
                  onSelected: (_) => setState(() => _walletTab = i))),
          ]),
        ),
        Expanded(child: _walletTab == 0 ? _myDocumentsBody() : _sharingBody()),
      ]),
    );
  }

  Widget _myDocumentsBody() {
    if (_isLoading) return const Center(child: CircularProgressIndicator());
    if (_error != null) return _buildErrorState();
    if (_documents == null || _documents!.isEmpty) return _buildEmptyState();
    return LayoutBuilder(builder: (context, constraints) {
      if (constraints.maxWidth < 650) return _buildDocumentList();
      final docs = _documents!.where((d) => !d.isSharedWithMe).toList();
      return RefreshIndicator(onRefresh: _loadDocuments,
        child: ListView(padding: const EdgeInsets.all(16), children: [
          Card(clipBehavior: Clip.antiAlias, child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: DataTable(columns: const [
              DataColumn(label: Text('Document')),
              DataColumn(label: Text('Type')),
              DataColumn(label: Text('Expiry Date')),
              DataColumn(label: Text('Sharing')),
              DataColumn(label: Text('Actions')),
            ], rows: docs.map((doc) => DataRow(cells: [
              DataCell(Row(children: [
                Checkbox(value: _selected.contains(doc.id), onChanged: (v) =>
                  setState(() { if (v == true) { _selected.add(doc.id); } else { _selected.remove(doc.id); } })),
                const Icon(Icons.description_outlined, color: Color(0xFF2563EB)),
                const SizedBox(width: 8),
                SizedBox(width: 180, child: Text(doc.name, overflow: TextOverflow.ellipsis)),
              ])),
              DataCell(Text(doc.documentType)),
              DataCell(Text(doc.expiryDate == null ? '—' : _formatWalletDate(doc.expiryDate!))),
              DataCell(Text(doc.activeShareCount > 0 ? 'Shared' : 'Private')),
              DataCell(TextButton(onPressed: () => _navigateToDetail(doc), child: const Text('View'))),
            ])).toList()),
          )),
        ]));
    });
  }

  String _formatWalletDate(DateTime date) =>
    '${date.day.toString().padLeft(2, '0')}/${date.month.toString().padLeft(2, '0')}/${date.year}';

  Widget _sharingBody() {
    if (_tabError != null) return Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
      Text(_tabError!), TextButton(onPressed: _loadWalletExtras, child: const Text('Retry')),
    ]));
    if (_sharedWithMe == null || _familyRequests == null) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_walletTab == 1) {
      final docs = _sharedWithMe!;
      if (docs.isEmpty) return const Center(child: Text('No documents shared with you yet.'));
      return ListView(padding: const EdgeInsets.all(16), children: [
        Card(clipBehavior: Clip.antiAlias, child: SingleChildScrollView(scrollDirection: Axis.horizontal,
          child: DataTable(columns: const [
            DataColumn(label: Text('Document')),
            DataColumn(label: Text('Shared By')),
            DataColumn(label: Text('Expiry Date')),
            DataColumn(label: Text('Use in Visa')),
            DataColumn(label: Text('Actions')),
          ], rows: docs.map((doc) => DataRow(cells: [
            DataCell(Text(doc.documentName)),
            DataCell(Text(doc.ownerEmail ?? 'Family connection')),
            DataCell(Text(doc.expiryDate == null ? '—' : _formatWalletDate(doc.expiryDate!))),
            DataCell(Text(doc.permission)),
            DataCell(TextButton(onPressed: () => _openShared(doc), child: const Text('View'))),
          ])).toList()))),
      ]);
    }
    final requests = _familyRequests!;
    final filtered = _walletTab == 2
        ? requests.where((r) => r.status == 'Accepted' && !r.isExpired).toList()
        : requests.where((r) => r.status == 'Pending').toList();
    if (filtered.isEmpty) return Center(child: Text(
      _walletTab == 2 ? 'No active family document sharing.' : 'No pending connection requests.'));
    return ListView(padding: const EdgeInsets.all(16), children: [
      for (final r in filtered) Card(child: ListTile(
        leading: Icon(_walletTab == 2 ? Icons.folder_shared_outlined : Icons.mark_email_unread_outlined,
          color: const Color(0xFF2563EB)),
        title: Text(r.isIncoming ? (r.requester.displayName ?? r.requester.email)
          : (r.recipient.displayName ?? r.recipient.email)),
        subtitle: Text('${r.relationship} • ${r.status} • ${r.documentIds.length} document(s)'),
        trailing: const Icon(Icons.chevron_right),
        onTap: () async {
          await Navigator.push(context, MaterialPageRoute(builder: (_) =>
            FamilyScreen(documentService: widget.documentService)));
          if (mounted) _loadWalletExtras();
        },
      )),
    ]);
  }

  Future<void> _openShared(SharedDocument doc) async {
    try {
      final url = await widget.documentService.getSharedDocumentAccessUrl(doc.documentId);
      final uri = Uri.parse(url);
      if (!await launchUrl(uri, mode: LaunchMode.externalApplication)) {
        throw StateError('Unable to launch document');
      }
    } catch (_) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Unable to open shared document.')));
    }
  }

  Widget _buildErrorState() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(Icons.error_outline, size: 48, color: AppTheme.errorColor),
          const SizedBox(height: 16),
          Text(_error!, style: TextStyle(color: AppTheme.errorColor)),
          const SizedBox(height: 12),
          ElevatedButton(onPressed: _loadDocuments, child: const Text('Retry')),
        ],
      ),
    );
  }

  Widget _buildEmptyState() {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(Icons.folder_open_outlined, size: 72, color: AppTheme.textSecondary.withValues(alpha: 0.5)),
            const SizedBox(height: 24),
            Text(
              'No documents yet',
              style: Theme.of(context).textTheme.headlineMedium,
            ),
            const SizedBox(height: 8),
            Text(
              'Keep your important documents safely\nin one place.',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                    color: AppTheme.textSecondary,
                  ),
            ),
            const SizedBox(height: 24),
            ElevatedButton.icon(
              onPressed: _navigateToAdd,
              icon: const Icon(Icons.add),
              label: const Text('Add your first document'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildDocumentList() {
    return RefreshIndicator(
      onRefresh: _loadDocuments,
      child: ListView.builder(
        padding: const EdgeInsets.all(16),
        itemCount: _documents!.length,
        itemBuilder: (context, index) {
          final doc = _documents![index];
          return Row(children: [
            Checkbox(semanticLabel: 'Select ${doc.name} for an application', value: _selected.contains(doc.id), onChanged: (value) => setState(() { if (value == true) { _selected.add(doc.id); } else { _selected.remove(doc.id); } })),
            Expanded(child: _DocumentCard(document: doc, onTap: () => _navigateToDetail(doc))),
          ]);
        },
      ),
    );
  }
}

class _DocumentCard extends StatelessWidget {
  final DocumentItem document;
  final VoidCallback onTap;

  const _DocumentCard({required this.document, required this.onTap});

  IconData _iconForType(String type) {
    return switch (type) {
      'Passport' => Icons.menu_book_outlined,
      'Visa' => Icons.flight_outlined,
      'Employment Pass' => Icons.badge_outlined,
      'Driving Licence' => Icons.drive_eta_outlined,
      'Insurance' => Icons.shield_outlined,
      'Medical Card' => Icons.medical_services_outlined,
      'Work Permit' => Icons.work_outlined,
      'Government Letter' => Icons.mail_outlined,
      'Immigration Document' => Icons.public_outlined,
      'Employment Contract' => Icons.handshake_outlined,
      'Rental Agreement' => Icons.home_outlined,
      'Tax Document' => Icons.receipt_long_outlined,
      'Government Correspondence' => Icons.account_balance_outlined,
      'General Correspondence' => Icons.email_outlined,
      _ => Icons.description_outlined,
    };
  }

  @override
  Widget build(BuildContext context) {
    final isExpired = document.isExpired;
    final isExpiringSoon = document.isExpiringSoon;

    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      child: InkWell(
        borderRadius: BorderRadius.circular(12),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: AppTheme.primaryColor.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Icon(_iconForType(document.documentType),
                    color: AppTheme.primaryColor, size: 22),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            document.name,
                            style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15),
                          ),
                        ),
                        if (isExpired)
                          _StatusBadge(label: 'EXPIRED', color: AppTheme.errorColor)
                        else if (isExpiringSoon)
                          _StatusBadge(label: 'EXPIRING', color: Colors.orange),
                      ],
                    ),
                    const SizedBox(height: 2),
                    Text(
                      document.documentType,
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                    if (document.isSharedWithMe)
                      Text('Family • ${document.ownerDisplayName ?? document.ownerEmail ?? "Shared"} • Read-only',
                        style: const TextStyle(fontSize: 12)),
                    if (document.expiryDate != null) ...[
                      const SizedBox(height: 2),
                      Text(
                        'Expires: ${_formatDate(document.expiryDate!)}',
                        style: TextStyle(
                          fontSize: 12,
                          color: isExpired ? AppTheme.errorColor : isExpiringSoon ? Colors.orange : AppTheme.textSecondary,
                          fontWeight: (isExpired || isExpiringSoon) ? FontWeight.w600 : FontWeight.normal,
                        ),
                      ),
                    ],
                    if (document.versionCount > 1 || document.activeShareCount > 0) ...[
                      const SizedBox(height: 4),
                      Row(
                        children: [
                          if (document.versionCount > 1) ...[
                            Icon(Icons.history, size: 12, color: AppTheme.textSecondary),
                            const SizedBox(width: 3),
                            Text('v${document.versionCount}', style: const TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
                            const SizedBox(width: 8),
                          ],
                          if (document.activeShareCount > 0) ...[
                            Icon(Icons.people_outline, size: 12, color: AppTheme.textSecondary),
                            const SizedBox(width: 3),
                            Text('Shared', style: const TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
                          ],
                        ],
                      ),
                    ],
                  ],
                ),
              ),
              Icon(Icons.chevron_right, color: AppTheme.textSecondary),
            ],
          ),
        ),
      ),
    );
  }

  String _formatDate(DateTime date) {
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
        'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    return '${date.day} ${months[date.month - 1]} ${date.year}';
  }
}

class _StatusBadge extends StatelessWidget {
  final String label;
  final Color color;

  const _StatusBadge({required this.label, required this.color});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        label,
        style: TextStyle(color: color, fontSize: 10, fontWeight: FontWeight.bold),
      ),
    );
  }
}
