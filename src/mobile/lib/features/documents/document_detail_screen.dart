import 'package:flutter/material.dart';
import '../../core/services/document_service.dart';
import '../../core/theme/app_theme.dart';
import 'package:url_launcher/url_launcher.dart';

class DocumentDetailScreen extends StatefulWidget {
  final DocumentItem document;
  final DocumentService documentService;

  const DocumentDetailScreen({
    super.key,
    required this.document,
    required this.documentService,
  });

  @override
  State<DocumentDetailScreen> createState() => _DocumentDetailScreenState();
}

class _DocumentDetailScreenState extends State<DocumentDetailScreen> {
  bool _isOpening = false;
  bool _isDeleting = false;

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
        content: const Text('This document will be permanently removed.'),
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

  @override
  Widget build(BuildContext context) {
    final doc = widget.document;
    final isExpired = doc.expiryDate != null && doc.expiryDate!.isBefore(DateTime.now());

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
          const SizedBox(height: 24),
          _InfoRow(label: 'File Name', value: doc.originalFileName ?? 'Unknown'),
          _InfoRow(label: 'File Type', value: doc.contentType ?? 'Unknown'),
          _InfoRow(label: 'File Size', value: doc.fileSizeFormatted),
          _InfoRow(label: 'Uploaded', value: _formatDateTime(doc.createdAt)),
          if (doc.expiryDate != null)
            _InfoRow(
              label: 'Expires',
              value: _formatDate(doc.expiryDate!),
              valueColor: isExpired ? AppTheme.errorColor : null,
            ),
          _InfoRow(label: 'Status', value: doc.status),
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
