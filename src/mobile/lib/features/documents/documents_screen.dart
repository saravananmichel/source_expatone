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
  final void Function(Widget screen)? onOpenNested;

  const DocumentsScreen({super.key, required this.documentService, this.reminderService, this.onOpenNested});

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
  List<FamilyDashboardDocument>? _familyDashboardDocs;
  String? _tabError;
  String? _sharedTabError;
  final Map<String, Future<List<DocumentShare>>> _shareRecipients = {};

  Future<List<DocumentShare>> _recipientsFor(String documentId) =>
      _shareRecipients.putIfAbsent(documentId, () => widget.documentService.getShares(documentId));

  @override
  void initState() {
    super.initState();
    _loadDocuments();
    _loadWalletExtras();
  }

  Future<void> _loadWalletExtras() async {
    // Family dashboard and direct document sharing are independent sources.
    // Load concurrently and preserve whichever feed succeeds.
    final familyFuture = widget.documentService.familyService.dashboard();
    final directFuture = widget.documentService.getSharedWithMe();
    final requestsFuture = widget.documentService.familyService.requests();
    try {
      final family = await familyFuture;
      if (mounted) setState(() => _familyDashboardDocs = family);
    } catch (_) {
      if (mounted) setState(() => _familyDashboardDocs = []);
    }
    try {
      final direct = await directFuture;
      if (mounted) setState(() { _sharedWithMe = direct; _sharedTabError = null; });
    } catch (_) {
      if (mounted) setState(() {
        _sharedWithMe = [];
        _sharedTabError = 'Unable to load direct shares. Retry to refresh.';
      });
    }
    try {
      final requests = await requestsFuture;
      if (mounted) setState(() { _familyRequests = requests; _tabError = null; });
    } catch (_) {
      if (mounted) setState(() {
        _familyRequests = [];
        _tabError = 'Unable to load family requests. Retry to refresh.';
      });
    }
  }

  Future<void> _loadDocuments() async {
    setState(() {
      _isLoading = true;
      _error = null;
      _shareRecipients.clear();
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
          PopupMenuButton<String>(
            tooltip: 'Wallet actions',
            icon: const Icon(Icons.more_vert),
            onSelected: (action) async {
              switch (action) {
                case 'application':
                  if (_selected.isEmpty || _isLoading || _error != null) return;
                  final application = ApplicationAssistantScreen(documentService: widget.documentService,
                    selectedDocumentIds: _selected.toList());
                  if (widget.onOpenNested != null) {
                    widget.onOpenNested!(application);
                  } else {
                    await Navigator.push(context, MaterialPageRoute(builder: (_) => application));
                  }
                  if (mounted) _loadDocuments();
                case 'family':
                  final family = FamilyScreen(documentService: widget.documentService,
                    onOpenNested: widget.onOpenNested);
                  if (widget.onOpenNested != null) {
                    widget.onOpenNested!(family);
                  } else {
                    await Navigator.push(context, MaterialPageRoute(builder: (_) => family));
                  }
                  if (mounted) { _loadDocuments(); _loadWalletExtras(); }
                case 'refresh':
                  _loadDocuments();
                  _loadWalletExtras();
                case 'add':
                  _navigateToAdd();
              }
            },
            itemBuilder: (_) => [
              PopupMenuItem(value: 'application', enabled: _selected.isNotEmpty && !_isLoading && _error == null,
                child: const Text('Prepare application')),
              const PopupMenuItem(value: 'family', child: Text('Friends & Family')),
              const PopupMenuItem(value: 'refresh', child: Text('Refresh wallet')),
              const PopupMenuItem(value: 'add', child: Text('Add document')),
            ],
          ),
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
    if (_walletTab == 1 && _sharedTabError != null && (_familyDashboardDocs ?? []).isEmpty) {
      return Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
        Text(_sharedTabError!),
        TextButton(onPressed: _loadWalletExtras, child: const Text('Retry')),
      ]));
    }
    if (_walletTab == 3 && _tabError != null) return Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
      Text(_tabError!), TextButton(onPressed: _loadWalletExtras, child: const Text('Retry')),
    ]));
    if ((_walletTab == 1 && _sharedWithMe == null && _familyDashboardDocs == null) ||
        (_walletTab == 2 && _isLoading) ||
        (_walletTab == 3 && _familyRequests == null)) {
      return const Center(child: CircularProgressIndicator());
    }
    if (_walletTab == 1) {
      final direct = _sharedWithMe ?? [];
      final directIds = direct.map((d) => d.documentId).toSet();
      final family = (_familyDashboardDocs ?? [])
          .where((d) => !d.isOwned && !directIds.contains(d.id)).toList();
      if (direct.isEmpty && family.isEmpty) {
        return const Center(child: Text('No documents shared with you yet.'));
      }
      return ListView(padding: const EdgeInsets.all(16), children: [
        for (final ownerId in family.map((d) => d.owner.id).toSet())
          Card(child: ExpansionTile(
          leading: const CircleAvatar(child: Icon(Icons.person_outline)),
          title: Text('${family.firstWhere((d) => d.owner.id == ownerId).owner.displayName ?? family.firstWhere((d) => d.owner.id == ownerId).owner.email} (${family.where((d) => d.owner.id == ownerId).length})'),
          subtitle: Text(family.firstWhere((d) => d.owner.id == ownerId).owner.email),
          children: [SingleChildScrollView(scrollDirection: Axis.horizontal,
            child: DataTable(columns: const [
              DataColumn(label: Text('Document')),
              DataColumn(label: Text('Type')),
              DataColumn(label: Text('Expiry Date')),
              DataColumn(label: Text('Sharing')),
              DataColumn(label: Text('Actions')),
            ], rows: family.where((d) => d.owner.id == ownerId).map((d) => DataRow(cells: [
              DataCell(Text(d.name)),
              DataCell(Text(d.documentType)),
              DataCell(Text(d.expiryDate == null ? '—' : _formatWalletDate(d.expiryDate!))),
              const DataCell(Text('Family access')),
              DataCell(TextButton(onPressed: () async {
                try {
                  final document = await widget.documentService.getDocument(d.id);
                  if (!context.mounted) return;
                  await Navigator.push(context, MaterialPageRoute(builder: (_) =>
                    DocumentDetailScreen(document: document, documentService: widget.documentService,
                      reminderService: widget.reminderService)));
                } catch (_) {
                  if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('Unable to open family document.')));
                }
              }, child: const Text('View'))),
            ])).toList()))],
        )),
        for (final owner in direct.map((d) => d.ownerEmail ?? 'Shared contact').toSet())
          Card(child: ExpansionTile(
            initiallyExpanded: true,
            leading: const CircleAvatar(child: Icon(Icons.person_outline)),
            title: Text('${owner} (${direct.where((d) => (d.ownerEmail ?? 'Shared contact') == owner).length})'),
            subtitle: const Text('Direct document shares'),
            children: [SingleChildScrollView(scrollDirection: Axis.horizontal,
              child: DataTable(columns: const [
                DataColumn(label: Text('Document')),
                DataColumn(label: Text('Type')),
                DataColumn(label: Text('Expiry Date')),
                DataColumn(label: Text('Sharing')),
                DataColumn(label: Text('Actions')),
              ], rows: direct.where((d) => (d.ownerEmail ?? 'Shared contact') == owner)
                .map((d) => DataRow(cells: [
                  DataCell(Text(d.documentName)),
                  DataCell(Text(d.documentType)),
                  DataCell(Text(d.expiryDate == null ? '—' : _formatWalletDate(d.expiryDate!))),
                  DataCell(Text(d.permission)),
                  DataCell(TextButton(onPressed: () => _openShared(d), child: const Text('View'))),
                ])).toList()))],
          )),
      ]);
    }
    if (_walletTab == 2) {
      // Incoming requests are accepted by this account and grant access to its documents.
      // Outgoing requests describe documents owned by the other account.
      final activeFamily = (_familyRequests ?? [])
          .where((r) => r.status == 'Accepted' && !r.isExpired && r.isIncoming).toList();
      final familyDocumentIds = <String>{
        for (final r in activeFamily) ...r.documentIds,
      };
      final docs = (_documents ?? []).where((d) => !d.isSharedWithMe &&
          (d.activeShareCount > 0 || familyDocumentIds.contains(d.id))).toList();
      if (docs.isEmpty) return const Center(child: Text('No documents shared by you yet.'));
      return ListView(padding: const EdgeInsets.all(16), children: [
        Card(clipBehavior: Clip.antiAlias, child: SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: DataTable(columns: const [
            DataColumn(label: Text('Document')),
            DataColumn(label: Text('Type')),
            DataColumn(label: Text('Expiry Date')),
            DataColumn(label: Text('Sharing')),
            DataColumn(label: Text('Actions')),
          ], rows: docs.map((doc) => DataRow(cells: [
            DataCell(Text(doc.name)),
            DataCell(Text(doc.documentType)),
            DataCell(Text(doc.expiryDate == null ? '—' : _formatWalletDate(doc.expiryDate!))),
            DataCell(Text(doc.activeShareCount > 0 ? '${doc.activeShareCount} direct' : 'Family')),
            DataCell(TextButton(onPressed: () => _navigateToDetail(doc), child: const Text('View'))),
          ])).toList()))),
        for (final doc in docs) Card(child: ExpansionTile(
          leading: const Icon(Icons.folder_shared_outlined),
          title: Text(doc.name),
          subtitle: const Text('View sharing recipients'),
          children: [
            for (final request in activeFamily.where((r) => r.documentIds.contains(doc.id)))
              ListTile(
                leading: const Icon(Icons.family_restroom_outlined),
                title: Text(request.isIncoming
                    ? (request.requester.displayName ?? request.requester.email)
                    : (request.recipient.displayName ?? request.recipient.email)),
                subtitle: Text('${request.relationship} • Family access'),
              ),
            if (doc.activeShareCount > 0) FutureBuilder<List<DocumentShare>>(
              future: _recipientsFor(doc.id),
              builder: (context, snapshot) {
                if (snapshot.hasError) return const ListTile(title: Text('Unable to load direct recipients'));
                if (!snapshot.hasData) return const ListTile(title: Text('Loading recipients...'));
                return Column(children: [for (final share in snapshot.data!.where((x) => x.isActive))
                  ListTile(title: Text(share.sharedWithEmail ?? share.sharedWithUserId),
                    subtitle: Text(share.permission))]);
              },
            ),
          ],
        )),
      ]);
    }
    final requests = _familyRequests ?? [];
    final filtered = requests.where((r) => r.status == 'Pending').toList();
    if (filtered.isEmpty) return Center(child: Text(
      'No pending connection requests.'));
    return ListView(padding: const EdgeInsets.all(16), children: [
      for (final r in filtered) Card(child: ListTile(
        leading: Icon(Icons.mark_email_unread_outlined,
          color: const Color(0xFF2563EB)),
        title: Text(r.isIncoming ? (r.requester.displayName ?? r.requester.email)
          : (r.recipient.displayName ?? r.recipient.email)),
        subtitle: Text('${r.relationship} • ${r.status} • ${r.documentIds.length} document(s)'),
        trailing: const Icon(Icons.chevron_right),
        onTap: () async {
          final family = FamilyScreen(documentService: widget.documentService,
            onOpenNested: widget.onOpenNested);
          if (widget.onOpenNested != null) {
            widget.onOpenNested!(family);
          } else {
            await Navigator.push(context, MaterialPageRoute(builder: (_) => family));
          }
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
