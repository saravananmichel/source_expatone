import 'package:flutter/material.dart';
import 'package:file_picker/file_picker.dart';
import '../../core/services/document_service.dart';
import '../../core/services/reminder_service.dart';
import '../../core/theme/app_theme.dart';
import '../../core/error/app_exception.dart';
import '../reminders/manage_reminders_screen.dart';
import 'document_analysis_screen.dart';
import 'package:url_launcher/url_launcher.dart';

class DocumentDetailScreen extends StatefulWidget {
  final DocumentItem document;
  final DocumentService documentService;
  final ReminderService? reminderService;

  const DocumentDetailScreen({
    super.key,
    required this.document,
    required this.documentService,
    this.reminderService,
  });

  @override
  State<DocumentDetailScreen> createState() => _DocumentDetailScreenState();
}

class _DocumentDetailScreenState extends State<DocumentDetailScreen> {
  bool _isOpening = false;
  bool _isDeleting = false;
  bool _isUploadingVersion = false;

  List<DocumentVersion>? _versions;
  List<DocumentShare>? _shares;
  List<DocumentAuditLog>? _auditLogs;

  @override
  void initState() {
    super.initState();
    _loadVersions();
    _loadShares();
    _loadAuditLogs();
  }

  Future<void> _loadVersions() async {
    try {
      final versions = await widget.documentService.getVersions(widget.document.id);
      if (mounted) setState(() => _versions = versions);
    } catch (_) {}
  }

  Future<void> _loadShares() async {
    try {
      final shares = await widget.documentService.getShares(widget.document.id);
      if (mounted) setState(() => _shares = shares);
    } catch (_) {}
  }

  Future<void> _loadAuditLogs() async {
    try {
      final logs = await widget.documentService.getAuditLogs(widget.document.id);
      if (mounted) setState(() => _auditLogs = logs);
    } catch (_) {}
  }

  Future<void> _openDocument() async {
    setState(() => _isOpening = true);
    try {
      final url = await widget.documentService.getAccessUrl(widget.document.id);
      final uri = Uri.parse(url);
      if (await canLaunchUrl(uri)) {
        await launchUrl(uri, mode: LaunchMode.externalApplication);
      } else {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Unable to open document')),
          );
        }
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Unable to open document. Please try again.')),
        );
      }
    } finally {
      if (mounted) setState(() => _isOpening = false);
    }
  }

  Future<void> _deleteDocument() async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete document?'),
        content: Text(
          _versions != null && _versions!.isNotEmpty
              ? 'This document and all ${_versions!.length} version(s) will be permanently removed.'
              : 'This document will be permanently removed.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            style: TextButton.styleFrom(foregroundColor: AppTheme.errorColor),
            child: const Text('Delete'),
          ),
        ],
      ),
    );

    if (confirm != true) return;
    setState(() => _isDeleting = true);
    try {
      await widget.documentService.deleteDocument(widget.document.id);
      if (mounted) Navigator.pop(context, true);
    } catch (_) {
      if (mounted) {
        setState(() => _isDeleting = false);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Unable to delete document. Please try again.')),
        );
      }
    }
  }

  Future<void> _uploadNewVersion() async {
    final file = await FilePicker.pickFile(
      type: FileType.custom,
      allowedExtensions: ['pdf', 'jpg', 'jpeg', 'png'],
    );

    if (file == null) return;

    final size = file.lengthSync() ?? await file.length();
    final bytes = await file.readAsBytes();
    if (bytes.isEmpty) return;

    String contentType;
    final ext = file.extension?.toLowerCase() ?? '';
    if (ext == 'pdf') {
      contentType = 'application/pdf';
    } else if (ext == 'jpg' || ext == 'jpeg') {
      contentType = 'image/jpeg';
    } else if (ext == 'png') {
      contentType = 'image/png';
    } else {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Only PDF, JPEG, and PNG files are supported.')),
        );
      }
      return;
    }

    setState(() => _isUploadingVersion = true);
    try {
      final uploadResponse = await widget.documentService.requestVersionUploadUrl(
        documentId: widget.document.id,
        fileName: file.name,
        contentType: contentType,
        fileSizeBytes: size,
      );

      await widget.documentService.uploadFileToS3(
        uploadResponse.uploadUrl,
        bytes,
        contentType,
      );

      await widget.documentService.completeVersionUpload(
        widget.document.id,
        uploadResponse.versionId,
      );

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('New version uploaded successfully')),
        );
      }
      _loadVersions();
      _loadAuditLogs();
    } catch (e) {
      if (mounted) {
        final message = e is AppException ? e.message : 'Upload failed. Please try again.';
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(message)),
        );
      }
    } finally {
      if (mounted) setState(() => _isUploadingVersion = false);
    }
  }

  bool _canDeleteVersion(DocumentVersion version) {
    if (_versions == null || _versions!.length <= 1) return false;
    return true;
  }

  Future<void> _deleteVersion(DocumentVersion version) async {
    final fileName = version.originalFileName ?? 'Version ${version.versionNumber}';
    final confirm = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete version?'),
        content: Text('$fileName will be permanently removed.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            style: TextButton.styleFrom(foregroundColor: AppTheme.errorColor),
            child: const Text('Delete'),
          ),
        ],
      ),
    );

    if (confirm != true) return;
    try {
      await widget.documentService.deleteVersion(widget.document.id, version.id);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Version deleted')),
        );
      }
      _loadVersions();
      _loadAuditLogs();
    } catch (e) {
      if (mounted) {
        final message = e is AppException ? e.message : 'Unable to delete version. Please try again.';
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(message)),
        );
      }
    }
  }

  Future<void> _showShareDialog() async {
    final emailController = TextEditingController();
    final result = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Share Document'),
        content: TextField(
          controller: emailController,
          decoration: const InputDecoration(
            labelText: 'Email address',
            hintText: 'Enter recipient\'s email',
          ),
          keyboardType: TextInputType.emailAddress,
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, emailController.text.trim()),
            child: const Text('Share'),
          ),
        ],
      ),
    );

    if (result == null || result.isEmpty) return;

    try {
      await widget.documentService.shareDocument(widget.document.id, result);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Document shared with $result')),
        );
      }
      _loadShares();
      _loadAuditLogs();
    } catch (e) {
      if (mounted) {
        final message = e is AppException ? e.message : 'Failed to share document.';
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(message)),
        );
      }
    }
  }

  Future<void> _revokeShare(DocumentShare share) async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Revoke access?'),
        content: Text('${share.sharedWithEmail ?? 'This user'} will no longer be able to view this document.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            style: TextButton.styleFrom(foregroundColor: AppTheme.errorColor),
            child: const Text('Revoke'),
          ),
        ],
      ),
    );

    if (confirm != true) return;
    try {
      await widget.documentService.revokeShare(widget.document.id, share.id);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Access revoked')),
        );
      }
      _loadShares();
      _loadAuditLogs();
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Failed to revoke access.')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final doc = widget.document;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Document Details'),
        actions: [
          IconButton(
            icon: _isDeleting
                ? const SizedBox(
                    width: 20, height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2))
                : Icon(Icons.delete_outlined, color: AppTheme.errorColor),
            onPressed: _isDeleting ? null : _deleteDocument,
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Center(
            child: Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                color: AppTheme.primaryColor.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Icon(
                _iconForContentType(doc.contentType),
                size: 48,
                color: AppTheme.primaryColor,
              ),
            ),
          ),
          const SizedBox(height: 20),
          Text(doc.name,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.headlineMedium),
          const SizedBox(height: 4),
          Text(doc.documentType,
              textAlign: TextAlign.center,
              style: Theme.of(context)
                  .textTheme
                  .bodyLarge
                  ?.copyWith(color: AppTheme.textSecondary)),

          if (doc.isExpired || doc.isExpiringSoon) ...[
            const SizedBox(height: 12),
            _ExpiryBanner(document: doc),
          ],

          const SizedBox(height: 24),
          _InfoRow(label: 'File Name', value: doc.originalFileName ?? 'Unknown'),
          _InfoRow(label: 'File Type', value: doc.contentType ?? 'Unknown'),
          _InfoRow(label: 'File Size', value: doc.fileSizeFormatted),
          _InfoRow(label: 'Uploaded', value: _formatDateTime(doc.createdAt)),
          if (doc.expiryDate != null)
            _InfoRow(
              label: 'Expires',
              value: _formatDate(doc.expiryDate!),
              valueColor: doc.isExpired ? AppTheme.errorColor : doc.isExpiringSoon ? Colors.orange : null,
            ),
          _InfoRow(label: 'Status', value: doc.status),
          if (doc.versionCount > 0)
            _InfoRow(label: 'Versions', value: '${doc.versionCount}'),
          if (doc.activeShareCount > 0)
            _InfoRow(label: 'Shared with', value: '${doc.activeShareCount} user(s)'),

          // Action buttons
          if (doc.expiryDate != null && widget.reminderService != null) ...[
            const SizedBox(height: 16),
            OutlinedButton.icon(
              onPressed: () {
                Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) => ManageRemindersScreen(
                      document: doc,
                      reminderService: widget.reminderService!,
                    ),
                  ),
                );
              },
              icon: const Icon(Icons.notifications_outlined),
              label: const Text('Manage Reminders'),
            ),
          ],
          const SizedBox(height: 16),
          SizedBox(
            height: 50,
            child: OutlinedButton.icon(
              onPressed: () {
                Navigator.push(
                  context,
                  MaterialPageRoute(
                    builder: (_) => DocumentAnalysisScreen(
                      document: doc,
                      documentService: widget.documentService,
                    ),
                  ),
                );
              },
              icon: Icon(
                doc.isAnalyzed ? Icons.analytics_outlined : Icons.auto_awesome_outlined,
                color: AppTheme.secondaryColor,
              ),
              label: Text(doc.isAnalyzed ? 'View Analysis' : 'Analyze Document'),
            ),
          ),
          const SizedBox(height: 24),
          SizedBox(
            height: 50,
            child: ElevatedButton.icon(
              onPressed: _isOpening ? null : _openDocument,
              icon: _isOpening
                  ? const SizedBox(
                      width: 20, height: 20,
                      child: CircularProgressIndicator(
                          strokeWidth: 2, color: Colors.white))
                  : const Icon(Icons.open_in_new),
              label: Text(_isOpening ? 'Opening...' : 'View Document'),
            ),
          ),

          // Version history section
          const SizedBox(height: 32),
          _SectionHeader(
            title: 'Version History',
            action: TextButton.icon(
              onPressed: _isUploadingVersion ? null : _uploadNewVersion,
              icon: _isUploadingVersion
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Icon(Icons.upload_file, size: 18),
              label: Text(_isUploadingVersion ? 'Uploading...' : 'Upload New Version'),
            ),
          ),
          if (_versions == null)
            const Center(child: Padding(padding: EdgeInsets.all(8), child: CircularProgressIndicator()))
          else if (_versions!.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Text('No versions yet. Upload a new version to start tracking changes.',
                  style: TextStyle(color: AppTheme.textSecondary)),
            )
          else
            ..._versions!.map((v) => _VersionTile(
                  version: v,
                  documentService: widget.documentService,
                  documentId: widget.document.id,
                  onDelete: _canDeleteVersion(v) ? () => _deleteVersion(v) : null,
                )),

          // Sharing section
          const SizedBox(height: 32),
          _SectionHeader(
            title: 'Sharing',
            action: TextButton.icon(
              onPressed: _showShareDialog,
              icon: const Icon(Icons.person_add_outlined, size: 18),
              label: const Text('Share'),
            ),
          ),
          if (_shares == null)
            const Center(child: Padding(padding: EdgeInsets.all(8), child: CircularProgressIndicator()))
          else if (_shares!.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Text('Not shared with anyone.',
                  style: TextStyle(color: AppTheme.textSecondary)),
            )
          else
            ..._shares!.map((s) => _ShareTile(
                  share: s,
                  onRevoke: s.isActive ? () => _revokeShare(s) : null,
                )),

          // Audit log section
          const SizedBox(height: 32),
          const _SectionHeader(title: 'Activity Log'),
          if (_auditLogs == null)
            const Center(child: Padding(padding: EdgeInsets.all(8), child: CircularProgressIndicator()))
          else if (_auditLogs!.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Text('No activity recorded.',
                  style: TextStyle(color: AppTheme.textSecondary)),
            )
          else
            ..._auditLogs!.take(10).map((log) => _AuditLogTile(log: log)),

          const SizedBox(height: 32),
        ],
      ),
    );
  }

  IconData _iconForContentType(String? contentType) {
    if (contentType == null) return Icons.description_outlined;
    if (contentType.contains('pdf')) return Icons.picture_as_pdf_outlined;
    if (contentType.contains('image')) return Icons.image_outlined;
    return Icons.description_outlined;
  }

  String _formatDate(DateTime date) {
    const months = [
      'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
    ];
    return '${date.day} ${months[date.month - 1]} ${date.year}';
  }

  String _formatDateTime(DateTime date) {
    return '${_formatDate(date)} at ${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
  }
}

class _ExpiryBanner extends StatelessWidget {
  final DocumentItem document;

  const _ExpiryBanner({required this.document});

  @override
  Widget build(BuildContext context) {
    final isExpired = document.isExpired;
    final color = isExpired ? AppTheme.errorColor : Colors.orange;
    final label = isExpired ? 'EXPIRED' : 'EXPIRING SOON';
    final daysText = isExpired
        ? 'Expired ${DateTime.now().difference(document.expiryDate!).inDays} days ago'
        : 'Expires in ${document.expiryDate!.difference(DateTime.now()).inDays} days';

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Row(
        children: [
          Icon(isExpired ? Icons.error_outline : Icons.warning_amber_rounded, color: color, size: 20),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: TextStyle(color: color, fontWeight: FontWeight.bold, fontSize: 12)),
                Text(daysText, style: TextStyle(color: color, fontSize: 13)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  final String title;
  final Widget? action;

  const _SectionHeader({required this.title, this.action});

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Text(title, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold)),
        const Spacer(),
        ?action,
      ],
    );
  }
}

class _VersionTile extends StatelessWidget {
  final DocumentVersion version;
  final DocumentService documentService;
  final String documentId;
  final VoidCallback? onDelete;

  const _VersionTile({
    required this.version,
    required this.documentService,
    required this.documentId,
    this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    final date = version.createdAt;
    final dateStr = '${date.day} ${months[date.month - 1]} ${date.year}';

    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: CircleAvatar(
        backgroundColor: version.isCurrent
            ? AppTheme.primaryColor.withValues(alpha: 0.1)
            : Colors.grey.withValues(alpha: 0.1),
        child: Text('v${version.versionNumber}',
            style: TextStyle(
              color: version.isCurrent ? AppTheme.primaryColor : Colors.grey,
              fontWeight: FontWeight.bold,
              fontSize: 12,
            )),
      ),
      title: Text(version.originalFileName ?? 'Version ${version.versionNumber}'),
      subtitle: Text('$dateStr  ${version.fileSizeFormatted}${version.isCurrent ? '  (Current)' : ''}',
          style: const TextStyle(fontSize: 12)),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          IconButton(
            icon: const Icon(Icons.open_in_new, size: 18),
            onPressed: () async {
              try {
                final url = await documentService.getVersionAccessUrl(documentId, version.id);
                final uri = Uri.parse(url);
                if (await canLaunchUrl(uri)) {
                  await launchUrl(uri, mode: LaunchMode.externalApplication);
                }
              } catch (_) {
                if (context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('Unable to open version')),
                  );
                }
              }
            },
          ),
          if (onDelete != null)
            IconButton(
              icon: Icon(Icons.delete_outline, size: 18, color: AppTheme.errorColor),
              onPressed: onDelete,
            ),
        ],
      ),
    );
  }
}

class _ShareTile extends StatelessWidget {
  final DocumentShare share;
  final VoidCallback? onRevoke;

  const _ShareTile({required this.share, this.onRevoke});

  @override
  Widget build(BuildContext context) {
    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: CircleAvatar(
        backgroundColor: share.isActive
            ? AppTheme.primaryColor.withValues(alpha: 0.1)
            : Colors.grey.withValues(alpha: 0.1),
        child: Icon(
          share.isActive ? Icons.person : Icons.person_off,
          color: share.isActive ? AppTheme.primaryColor : Colors.grey,
          size: 20,
        ),
      ),
      title: Text(share.sharedWithEmail ?? 'Unknown user'),
      subtitle: Text(
        share.isActive ? 'Read access' : 'Access revoked',
        style: TextStyle(
          fontSize: 12,
          color: share.isActive ? AppTheme.textSecondary : AppTheme.errorColor,
        ),
      ),
      trailing: share.isActive
          ? IconButton(
              icon: Icon(Icons.close, size: 18, color: AppTheme.errorColor),
              onPressed: onRevoke,
            )
          : null,
    );
  }
}

class _AuditLogTile extends StatelessWidget {
  final DocumentAuditLog log;

  const _AuditLogTile({required this.log});

  @override
  Widget build(BuildContext context) {
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    final date = log.createdAt;
    final dateStr = '${date.day} ${months[date.month - 1]} ${date.year} ${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        children: [
          Icon(_iconForAction(log.action), size: 16, color: AppTheme.textSecondary),
          const SizedBox(width: 12),
          Expanded(child: Text(log.actionLabel, style: const TextStyle(fontSize: 13))),
          Text(dateStr, style: const TextStyle(fontSize: 11, color: AppTheme.textSecondary)),
        ],
      ),
    );
  }

  IconData _iconForAction(String action) {
    switch (action) {
      case 'document_uploaded':
      case 'version_uploaded':
        return Icons.upload;
      case 'document_accessed':
      case 'version_accessed':
      case 'shared_document_accessed':
        return Icons.visibility;
      case 'document_deleted':
      case 'version_deleted':
        return Icons.delete_outline;
      case 'share_created':
        return Icons.person_add;
      case 'share_revoked':
        return Icons.person_remove;
      default:
        return Icons.history;
    }
  }
}

class _InfoRow extends StatelessWidget {
  final String label;
  final String value;
  final Color? valueColor;

  const _InfoRow({required this.label, required this.value, this.valueColor});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          Text(label, style: TextStyle(color: AppTheme.textSecondary, fontSize: 14)),
          const Spacer(),
          Flexible(
            child: Text(
              value,
              style: TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w500,
                color: valueColor,
              ),
              textAlign: TextAlign.end,
            ),
          ),
        ],
      ),
    );
  }
}
