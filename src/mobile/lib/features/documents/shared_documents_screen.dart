import 'package:flutter/material.dart';
import '../../core/services/document_service.dart';
import '../../core/theme/app_theme.dart';
import 'package:url_launcher/url_launcher.dart';

class SharedDocumentsScreen extends StatefulWidget {
  final DocumentService documentService;

  const SharedDocumentsScreen({super.key, required this.documentService});

  @override
  State<SharedDocumentsScreen> createState() => _SharedDocumentsScreenState();
}

class _SharedDocumentsScreenState extends State<SharedDocumentsScreen> {
  List<SharedDocument>? _documents;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final docs = await widget.documentService.getSharedWithMe();
      if (mounted) setState(() => _documents = docs);
    } catch (e) {
      if (mounted) setState(() => _error = 'Failed to load shared documents.');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Shared With Me')),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_error != null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(_error!, style: const TextStyle(color: AppTheme.textSecondary)),
            const SizedBox(height: 16),
            ElevatedButton(onPressed: _load, child: const Text('Retry')),
          ],
        ),
      );
    }

    if (_documents == null) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_documents!.isEmpty) {
      return const Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.folder_shared_outlined, size: 64, color: AppTheme.textSecondary),
            SizedBox(height: 16),
            Text('No documents shared with you yet',
                style: TextStyle(color: AppTheme.textSecondary)),
          ],
        ),
      );
    }

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView.builder(
        padding: const EdgeInsets.all(16),
        itemCount: _documents!.length,
        itemBuilder: (context, index) {
          final doc = _documents![index];
          return _SharedDocTile(
            document: doc,
            onTap: () => _openSharedDocument(doc),
          );
        },
      ),
    );
  }

  Future<void> _openSharedDocument(SharedDocument doc) async {
    try {
      final url = await widget.documentService.getSharedDocumentAccessUrl(doc.documentId);
      final uri = Uri.parse(url);
      if (await canLaunchUrl(uri)) {
        await launchUrl(uri, mode: LaunchMode.externalApplication);
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Unable to open document.')),
        );
      }
    }
  }
}

class _SharedDocTile extends StatelessWidget {
  final SharedDocument document;
  final VoidCallback onTap;

  const _SharedDocTile({required this.document, required this.onTap});

  @override
  Widget build(BuildContext context) {
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    final date = document.sharedAt;
    final dateStr = '${date.day} ${months[date.month - 1]} ${date.year}';

    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: ListTile(
        leading: CircleAvatar(
          backgroundColor: AppTheme.primaryColor.withValues(alpha: 0.1),
          child: const Icon(Icons.description_outlined, color: AppTheme.primaryColor),
        ),
        title: Text(document.documentName),
        subtitle: Text(
          '${document.documentType}  |  Shared by ${document.ownerEmail ?? 'unknown'}  |  $dateStr',
          style: const TextStyle(fontSize: 12),
        ),
        trailing: const Icon(Icons.open_in_new, size: 18),
        onTap: onTap,
      ),
    );
  }
}
