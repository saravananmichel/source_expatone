import 'package:flutter/material.dart';
import '../../core/services/document_service.dart';
import '../../core/services/reminder_service.dart';
import '../../core/theme/app_theme.dart';
import 'add_document_screen.dart';
import 'document_detail_screen.dart';
import 'shared_documents_screen.dart';

class DocumentsScreen extends StatefulWidget {
  final DocumentService documentService;
  final ReminderService? reminderService;

  const DocumentsScreen({super.key, required this.documentService, this.reminderService});

  @override
  State<DocumentsScreen> createState() => _DocumentsScreenState();
}

class _DocumentsScreenState extends State<DocumentsScreen> {
  List<DocumentItem>? _documents;
  bool _isLoading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadDocuments();
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
    return Scaffold(
      appBar: AppBar(
        title: const Text('My Documents'),
        actions: [
          IconButton(
            icon: const Icon(Icons.folder_shared_outlined),
            tooltip: 'Shared With Me',
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute(
                builder: (_) => SharedDocumentsScreen(documentService: widget.documentService),
              ),
            ),
          ),
          IconButton(
            icon: const Icon(Icons.add),
            onPressed: _navigateToAdd,
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? _buildErrorState()
              : _documents == null || _documents!.isEmpty
                  ? _buildEmptyState()
                  : _buildDocumentList(),
    );
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
          return _DocumentCard(
            document: doc,
            onTap: () => _navigateToDetail(doc),
          );
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
