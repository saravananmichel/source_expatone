import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import '../../core/services/document_service.dart';
import '../../core/theme/app_theme.dart';

class AddDocumentScreen extends StatefulWidget {
  final DocumentService documentService;

  const AddDocumentScreen({super.key, required this.documentService});

  @override
  State<AddDocumentScreen> createState() => _AddDocumentScreenState();
}

class _AddDocumentScreenState extends State<AddDocumentScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  List<DocumentType>? _documentTypes;
  DocumentType? _selectedType;
  DateTime? _expiryDate;
  PlatformFile? _pickedFile;
  int? _pickedFileSize;
  bool _isLoadingTypes = true;
  bool _isUploading = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadTypes();
  }

  @override
  void dispose() {
    _nameController.dispose();
    super.dispose();
  }

  Future<void> _loadTypes() async {
    try {
      final types = await widget.documentService.getDocumentTypes();
      if (mounted) {
        setState(() {
          _documentTypes = types;
          _isLoadingTypes = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _error = 'Unable to load document types';
          _isLoadingTypes = false;
        });
      }
    }
  }

  Future<void> _pickFile() async {
    final file = await FilePicker.pickFile(
      type: FileType.custom,
      allowedExtensions: ['pdf', 'jpg', 'jpeg', 'png'],
    );

    if (file != null) {
      final size = file.lengthSync() ?? await file.length();
      if (size > 10 * 1024 * 1024) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('File must be smaller than 10 MB')),
          );
        }
        return;
      }
      setState(() {
        _pickedFile = file;
        _pickedFileSize = size;
      });
      if (_nameController.text.isEmpty) {
        _nameController.text = file.name.replaceAll(RegExp(r'\.[^.]+$'), '');
      }
    }
  }

  Future<void> _pickExpiryDate() async {
    final date = await showDatePicker(
      context: context,
      initialDate: DateTime.now().add(const Duration(days: 365)),
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 365 * 30)),
    );
    if (date != null && mounted) {
      setState(() => _expiryDate = date);
    }
  }

  String _contentTypeFromExtension(String? ext) {
    return switch (ext?.toLowerCase()) {
      'pdf' => 'application/pdf',
      'jpg' || 'jpeg' => 'image/jpeg',
      'png' => 'image/png',
      _ => 'application/octet-stream',
    };
  }

  Future<void> _upload() async {
    if (!_formKey.currentState!.validate()) return;
    if (_selectedType == null || _pickedFile == null) return;

    setState(() {
      _isUploading = true;
      _error = null;
    });

    try {
      final contentType = _contentTypeFromExtension(_pickedFile!.extension);

      final uploadInfo = await widget.documentService.requestUploadUrl(
        documentTypeId: _selectedType!.id,
        documentName: _nameController.text.trim(),
        fileName: _pickedFile!.name,
        contentType: contentType,
        fileSizeBytes: _pickedFileSize ?? 0,
        expiryDate: _expiryDate,
      );

      final fileBytes = await _pickedFile!.readAsBytes();

      await widget.documentService.uploadFileToS3(
        uploadInfo.uploadUrl, fileBytes, contentType);

      await widget.documentService.completeUpload(uploadInfo.documentId);

      if (mounted) Navigator.pop(context, true);
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = 'Upload failed. Please try again.';
          _isUploading = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Add Document')),
      body: _isLoadingTypes
          ? const Center(child: CircularProgressIndicator())
          : SafeArea(
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(24),
                child: Form(
                  key: _formKey,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      if (_error != null) ...[
                        Container(
                          padding: const EdgeInsets.all(12),
                          decoration: BoxDecoration(
                            color: AppTheme.errorColor.withValues(alpha: 0.1),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Text(_error!,
                              style: TextStyle(color: AppTheme.errorColor, fontSize: 14),
                              textAlign: TextAlign.center),
                        ),
                        const SizedBox(height: 16),
                      ],
                      DropdownButtonFormField<DocumentType>(
                        decoration: const InputDecoration(
                          labelText: 'Document Type',
                          prefixIcon: Icon(Icons.category_outlined),
                        ),
                        items: _documentTypes?.map((t) => DropdownMenuItem(
                              value: t,
                              child: Text(t.name),
                            )).toList(),
                        onChanged: (t) => setState(() => _selectedType = t),
                        validator: (v) => v == null ? 'Please select a document type' : null,
                      ),
                      const SizedBox(height: 16),
                      InkWell(
                        onTap: _isUploading ? null : _pickFile,
                        borderRadius: BorderRadius.circular(8),
                        child: Container(
                          padding: const EdgeInsets.all(24),
                          decoration: BoxDecoration(
                            border: Border.all(
                              color: _pickedFile != null
                                  ? AppTheme.successColor
                                  : const Color(0xFFE8E8E8),
                              width: _pickedFile != null ? 2 : 1,
                            ),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: Column(
                            children: [
                              Icon(
                                _pickedFile != null ? Icons.check_circle : Icons.cloud_upload_outlined,
                                size: 40,
                                color: _pickedFile != null
                                    ? AppTheme.successColor
                                    : AppTheme.textSecondary,
                              ),
                              const SizedBox(height: 8),
                              Text(
                                _pickedFile != null
                                    ? _pickedFile!.name
                                    : 'Tap to choose a file',
                                style: TextStyle(
                                  color: _pickedFile != null
                                      ? AppTheme.textPrimary
                                      : AppTheme.textSecondary,
                                  fontWeight: _pickedFile != null
                                      ? FontWeight.w500
                                      : FontWeight.normal,
                                ),
                                textAlign: TextAlign.center,
                              ),
                              if (_pickedFile == null)
                                Text(
                                  'PDF, JPEG, or PNG (max 10 MB)',
                                  style: TextStyle(fontSize: 12, color: AppTheme.textSecondary),
                                ),
                              if (_pickedFile != null && _pickedFileSize != null)
                                Text(
                                  '${(_pickedFileSize! / 1024).toStringAsFixed(1)} KB',
                                  style: TextStyle(fontSize: 12, color: AppTheme.textSecondary),
                                ),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(height: 16),
                      TextFormField(
                        controller: _nameController,
                        decoration: const InputDecoration(
                          labelText: 'Document Name',
                          prefixIcon: Icon(Icons.drive_file_rename_outline),
                        ),
                        validator: (v) =>
                            v == null || v.trim().isEmpty ? 'Please enter a name' : null,
                      ),
                      const SizedBox(height: 16),
                      if (_selectedType?.hasExpiry == true) ...[
                        InkWell(
                          onTap: _isUploading ? null : _pickExpiryDate,
                          child: InputDecorator(
                            decoration: const InputDecoration(
                              labelText: 'Expiry Date (optional)',
                              prefixIcon: Icon(Icons.calendar_today_outlined),
                            ),
                            child: Text(
                              _expiryDate != null
                                  ? '${_expiryDate!.day}/${_expiryDate!.month}/${_expiryDate!.year}'
                                  : 'Tap to select',
                              style: TextStyle(
                                color: _expiryDate != null
                                    ? AppTheme.textPrimary
                                    : AppTheme.textSecondary,
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(height: 16),
                      ],
                      const SizedBox(height: 8),
                      SizedBox(
                        height: 50,
                        child: ElevatedButton(
                          onPressed: _isUploading || _pickedFile == null
                              ? null
                              : _upload,
                          child: _isUploading
                              ? const Row(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    SizedBox(
                                      width: 20, height: 20,
                                      child: CircularProgressIndicator(
                                          strokeWidth: 2, color: Colors.white),
                                    ),
                                    SizedBox(width: 12),
                                    Text('Uploading...'),
                                  ],
                                )
                              : const Text('Upload Document',
                                  style: TextStyle(fontSize: 16)),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
    );
  }
}
