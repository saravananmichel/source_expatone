import 'dart:async';

import 'package:url_launcher/url_launcher.dart';
import 'package:flutter/material.dart';
import 'package:flutter/foundation.dart';

import '../../core/error/app_exception.dart';
import '../../core/services/document_service.dart';
import '../../core/theme/app_theme.dart';
import 'document_qa_screen.dart';

class DocumentAnalysisScreen extends StatefulWidget {
  final DocumentItem document;
  final DocumentService documentService;

  const DocumentAnalysisScreen({
    super.key,
    required this.document,
    required this.documentService,
  });

  @override
  State<DocumentAnalysisScreen> createState() => _DocumentAnalysisScreenState();
}

class _DocumentAnalysisScreenState extends State<DocumentAnalysisScreen> {
  Map<String, dynamic>? _analysis;
  Map<String, dynamic>? _extraction;
  bool _isLoading = true;
  bool _isAnalyzing = false;
  String? _error;
  String? _extractionError;
  String _extractionStatus = 'LOADING';
  String _processingStage = 'Queued';
  StreamSubscription<Map<String, String>>? _progressSubscription;

  @override
  void initState() {
    super.initState();
    _progressSubscription = widget.documentService.analysisProgress.listen((
      event,
    ) {
      if (mounted && event['documentId'] == widget.document.id) {
        setState(
          () => _processingStage = event['stage'] ?? 'Understanding document',
        );
      }
    });
    // Text extraction is independent from AI analysis. Load it separately so
    // the user can read the extracted text even if analysis is slow or fails.
    _loadExtractedText();

    if (widget.document.isAnalyzed) {
      _loadAnalysis();
    } else {
      _analyzeDocument();
    }
  }

  Future<void> _loadExtractedText() async {
    const maxAttempts = 240; // Match the existing 20-minute extraction window.

    for (var attempt = 0; attempt < maxAttempts; attempt++) {
      try {
        final result = await widget.documentService.getExtractedText(
          widget.document.id,
        );
        if (!mounted) return;

        final status = result['status'] as String? ?? 'NOT_REQUESTED';
        setState(() {
          _extraction = result;
          _extractionStatus = status;
          _extractionError = null;
        });

        if (status == 'COMPLETED' || status == 'FAILED') return;
        if (status != 'PENDING' && status != 'PROCESSING') return;
      } catch (_) {
        if (!mounted) return;
        setState(() {
          _extractionError = 'Unable to load extracted text.';
        });
        return;
      }

      if (attempt < maxAttempts - 1) {
        await Future<void>.delayed(const Duration(seconds: 5));
      }
    }

    if (mounted) {
      setState(() {
        _extractionError = 'Text extraction is taking longer than expected. Try refreshing this screen.';
      });
    }
  }

  Future<void> _loadAnalysis() async {
    setState(() {
      _isLoading = true;
      _error = null;
    });

    try {
      final result = await widget.documentService.getDocumentAnalysis(
        widget.document.id,
      );
      if (mounted) {
        setState(() {
          _analysis = result;
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _error = 'Unable to load analysis. Please try again.';
          _isLoading = false;
        });
      }
    }
  }

  Future<void> _analyzeDocument({bool forceReanalyze = false}) async {
    setState(() {
      _isAnalyzing = true;
      _isLoading = true;
      _error = null;
    });

    try {
      final result = await widget.documentService.analyzeDocument(
        widget.document.id,
        forceReanalyze: forceReanalyze,
      );
      if (mounted) {
        setState(() {
          _analysis = result;
          _isLoading = false;
          _isAnalyzing = false;
        });
      }
    } catch (error) {
      if (mounted) {
        setState(() {
          _error = error is AppException
              ? error.message
              : 'Unable to analyze document. Please try again.';
          _isLoading = false;
          _isAnalyzing = false;
        });
      }
    }
  }

  @override
  void dispose() {
    _progressSubscription?.cancel();
    super.dispose();
  }

  Future<void> _showHistory() async {
    try {
      final history = await widget.documentService.getAnalysisHistory(
        widget.document.id,
      );
      if (!mounted) return;
      await showModalBottomSheet<void>(
        context: context,
        builder: (context) => SafeArea(
          child: ListView(
            shrinkWrap: true,
            children: [
              const ListTile(title: Text('Analysis history')),
              ...history.asMap().entries.map((entry) {
                final job = entry.value;
                return ListTile(
                  title: Text(
                    'Analysis v${history.length - entry.key} · ${job['status']}',
                  ),
                  subtitle: Text(job['createdAt'] as String? ?? ''),
                  enabled: job['analysis'] is Map,
                  onTap: () {
                    setState(
                      () => _analysis = Map<String, dynamic>.from(
                        job['analysis'] as Map,
                      ),
                    );
                    Navigator.pop(context);
                  },
                );
              }),
            ],
          ),
        ),
      );
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Unable to load analysis history.')),
        );
      }
    }
  }

  Future<void> _openSource(Map source) async {
    try {
      final jobId = _analysis?['documentVersionId'];
      final url = jobId is String
          ? await widget.documentService.getVersionAccessUrl(
              widget.document.id,
              jobId,
            )
          : await widget.documentService.getAccessUrl(widget.document.id);
      final uri = Uri.parse(url).replace(fragment: 'page=${source['page']}');
      if (!await launchUrl(uri, mode: LaunchMode.externalApplication)) {
        throw StateError('Unable to open');
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Unable to open the source document.')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Document Analysis'),
        actions: [
          IconButton(
            icon: const Icon(Icons.history),
            tooltip: 'Analysis history',
            onPressed: _showHistory,
          ),
          if (_analysis != null)
            IconButton(
              icon: const Icon(Icons.refresh),
              onPressed: _isAnalyzing
                  ? null
                  : () => _analyzeDocument(forceReanalyze: true),
              tooltip: 'Re-analyze',
            ),
        ],
      ),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    final analysis = _analysis;

    if (analysis == null) {
      return ListView(
        padding: const EdgeInsets.all(20),
        children: [
          _buildExtractedTextSection(),
          const SizedBox(height: 12),
          if (_error != null) _buildAnalysisError(),
          if (_error == null && (_isLoading || _isAnalyzing))
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            _isAnalyzing
                                ? 'AI analysis in progress'
                                : 'Loading analysis',
                            style: const TextStyle(fontWeight: FontWeight.w600),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            '$_processingStage. Your extracted text remains available above.',
                            style: TextStyle(
                              color: AppTheme.textSecondary,
                              fontSize: 13,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          if (_error == null && !_isLoading && !_isAnalyzing)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 16),
              child: Text('No analysis available yet.'),
            ),
        ],
      );
    }

    return ListView(
      padding: const EdgeInsets.all(20),
      children: [
        _buildExtractedTextSection(),
        if (_error != null) ...[
          const SizedBox(height: 8),
          _buildAnalysisError(),
        ],
        _buildReviewNotice(),
        _buildSummarySection(),
        _buildGroundedStatements(),
        if ((analysis['statements'] as List? ?? []).isEmpty) ...[
          _buildStructuredFieldsSection(),
          _buildKeyInformationSection(),
          _buildImportantDatesSection(),
          _buildRequiredActionsSection(),
          _buildDeadlinesSection(),
          _buildWarningsSection(),
          _buildTerminologySection(),
          _buildExplanationSection(),
        ],
        _buildMissingInformation(),
        _buildConfidenceBadge(),
        if (kDebugMode) _buildQualityPanel(),
        const SizedBox(height: 16),
        _buildAskButton(),
        const SizedBox(height: 12),
        _buildDisclaimerBanner(),
        const SizedBox(height: 24),
      ],
    );
  }

  Widget _buildExtractedTextSection() {
    final result = _extraction;
    final text =
        result?['extractedText'] as String? ?? result?['text'] as String? ?? '';
    final isPassport = '${widget.document.documentType} ${widget.document.name}'
        .toLowerCase()
        .contains('passport');
    final fields = isPassport ? _parsePassportFields(text) : <String, String>{};

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.text_snippet_outlined),
                const SizedBox(width: 8),
                const Expanded(
                  child: Text(
                    'Extracted Text',
                    style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
                  ),
                ),
                if (_extractionStatus == 'COMPLETED')
                  const Icon(Icons.check_circle_outline, color: Colors.green),
              ],
            ),
            const SizedBox(height: 10),
            if (text.trim().isNotEmpty && fields.isNotEmpty) ...[
              Text(
                'Passport information',
                style: TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w600,
                  color: AppTheme.textSecondary,
                ),
              ),
              const SizedBox(height: 12),
              Wrap(
                spacing: 16,
                runSpacing: 16,
                children: fields.entries.map((entry) {
                  return SizedBox(
                    width: 140,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          entry.key,
                          style: TextStyle(
                            fontSize: 12,
                            color: AppTheme.textSecondary,
                          ),
                        ),
                        const SizedBox(height: 4),
                        SelectableText(
                          entry.value,
                          style: const TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ),
                  );
                }).toList(),
              ),
              const SizedBox(height: 8),
              const Divider(),
              Text(
                'Readable extracted text',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: AppTheme.textSecondary,
                ),
              ),
              const SizedBox(height: 8),
              SelectableText(
                _readableExtractedText(text).isEmpty
                    ? 'No readable text could be separated from the OCR output.'
                    : _readableExtractedText(text),
                style: const TextStyle(height: 1.5),
              ),
              ExpansionTile(
                tilePadding: EdgeInsets.zero,
                title: const Text('View original extracted text'),
                children: [
                  Align(
                    alignment: Alignment.centerLeft,
                    child: SelectableText(
                      text,
                      style: const TextStyle(height: 1.5),
                    ),
                  ),
                ],
              ),
            ] else if (text.trim().isNotEmpty)
              SelectableText(text, style: const TextStyle(height: 1.5))
            else if (_extractionStatus == 'FAILED')
              const Text(
                'Text extraction failed. You can still retry AI analysis.',
              )
            else if (_extractionError != null)
              Text(
                _extractionError!,
                style: TextStyle(color: AppTheme.warningColor),
              )
            else
              Text(
                _extractionStatus == 'PENDING' ||
                        _extractionStatus == 'PROCESSING' ||
                        _extractionStatus == 'LOADING'
                    ? 'Extracting text from your document…'
                    : 'No extracted text is available yet.',
                style: TextStyle(color: AppTheme.textSecondary),
              ),
          ],
        ),
      ),
    );
  }

  String _readableExtractedText(String text) {
    final readableLines = text.split(RegExp(r'\r?\n')).where((line) {
      final trimmed = line.trim();
      if (trimmed.isEmpty) return false;

      final meaningful = trimmed.replaceAll(
        RegExp(r'[^a-zA-Z0-9]'),
        '',
      );

      // Filter obvious symbol-heavy OCR lines for display only.
      // The original extracted text remains unchanged.
      return meaningful.length / trimmed.length >= 0.40;
    });

    return readableLines.join('\n').trim();
  }

  bool _isUsablePassportValue(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) return false;

    // Reject symbol-only and symbol-heavy OCR fragments, while preserving
    // normal names, identifiers, dates and values containing punctuation.
    final characters = trimmed.replaceAll(RegExp(r'\s'), '');
    if (characters.isEmpty) return false;

    final meaningful = RegExp(r'[a-zA-Z0-9]').allMatches(characters).length;
    return meaningful / characters.length >= 0.6;
  }

  Map<String, String> _parsePassportFields(String text) {
    const labels = <String, List<String>>{
      'Passport number': ['passport number', 'passport no'],
      'Surname': ['surname', 'family name'],
      'Given names': ['given names', 'given name', 'first name'],
      'Nationality': ['nationality'],
      'Date of birth': ['date of birth', 'dob'],
      'Sex': ['sex', 'gender'],
      'Place of birth': ['place of birth'],
      'Place of issue': ['place of issue', 'issuing authority'],
      'Date of issue': ['date of issue', 'issue date', 'date issued'],
      'Date of expiry': ['date of expiry', 'expiry date', 'expiration date'],
      'Address': ['address', 'residential address'],
    };

    String normalize(String value) => value
        .toLowerCase()
        .replaceAll(RegExp(r'[^a-z0-9 ]'), '')
        .trim()
        .replaceAll(RegExp(r'\s+'), ' ');

    final labelLookup = <String, String>{};
    for (final entry in labels.entries) {
      for (final alias in entry.value) {
        labelLookup[normalize(alias)] = entry.key;
      }
    }

    final lines = text.split(RegExp(r'[\r\n]+'));
    final fields = <String, String>{};

    for (var i = 0; i < lines.length; i++) {
      final line = lines[i].trim();
      if (line.isEmpty) continue;

      final separator = line.indexOf(':');
      final rawKey = separator > 0 ? line.substring(0, separator) : line;
      final canonicalLabel = labelLookup[normalize(rawKey)];

      if (canonicalLabel == null || fields.containsKey(canonicalLabel)) {
        continue;
      }

      var value = separator > 0 ? line.substring(separator + 1).trim() : '';

      if (value.isEmpty) {
        var next = i + 1;
        while (next < lines.length && lines[next].trim().isEmpty) {
          next++;
        }
        if (next >= lines.length) continue;

        final candidate = lines[next].trim();
        final candidateKey = candidate.contains(':')
            ? candidate.substring(0, candidate.indexOf(':'))
            : candidate;

        if (labelLookup.containsKey(normalize(candidateKey)) ||
            candidate.startsWith('P<') ||
            candidate.contains('<<')) {
          continue;
        }

        value = candidate;
        i = next;
      }

      if (_isUsablePassportValue(value) &&
          !value.startsWith('P<') &&
          !value.contains('<<')) {
        fields[canonicalLabel] = value;
      }
    }

    return fields;
  }

  Widget _buildAnalysisError() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.error_outline, color: AppTheme.errorColor),
                const SizedBox(width: 8),
                const Expanded(
                  child: Text(
                    'AI analysis could not be completed',
                    style: TextStyle(fontWeight: FontWeight.w600),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(_error!, style: TextStyle(color: AppTheme.textSecondary)),
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerLeft,
              child: ElevatedButton(
                onPressed: _isAnalyzing ? null : () => _analyzeDocument(),
                child: const Text('Retry analysis'),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildReviewNotice() {
    if (_analysis?['requiresReview'] != true) return const SizedBox.shrink();
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Text(
          'Review recommended. Verify important interpretations against the original document.',
          style: TextStyle(color: AppTheme.textSecondary),
        ),
      ),
    );
  }

  Widget _buildGroundedStatements() {
    final statements = _analysis?['statements'] as List? ?? [];
    final evidence = _analysis?['evidence'] as List? ?? [];
    if (statements.isEmpty) return const SizedBox.shrink();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        ..._groupStatements(statements).entries.map(
          (group) => Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                group.key,
                style: const TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: 12),
              ...group.value.map((raw) {
                final statement = raw as Map;
                final ids = statement['evidenceIds'] as List? ?? [];
                final sources = evidence
                    .where((raw) => ids.contains((raw as Map)['id']))
                    .toList();
                final card = Card(
                  margin: const EdgeInsets.only(bottom: 12),
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          statement['supportStatus'] != null &&
                                  statement['supportStatus'] != 'SUPPORTED'
                              ? 'Source detail to verify'
                              : '${statement['kind'] ?? 'fact'} · ${statement['label'] ?? ''}',
                          style: const TextStyle(fontWeight: FontWeight.w600),
                        ),
                        const SizedBox(height: 8),
                        if (statement['supportStatus'] != null &&
                            statement['supportStatus'] != 'SUPPORTED') ...[
                          Text(
                            'Interpretation needs review',
                            style: TextStyle(color: AppTheme.warningColor),
                          ),
                          const SizedBox(height: 6),
                          SelectableText(
                            'Extracted value: ${statement['originalValue'] ?? ''}',
                            style: const TextStyle(height: 1.5),
                          ),
                        ] else
                          SelectableText(
                            statement['text'] as String? ?? '',
                            style: const TextStyle(height: 1.5),
                          ),
                        if (sources.isNotEmpty)
                          TextButton.icon(
                            icon: const Icon(Icons.find_in_page_outlined),
                            label: const Text('View source'),
                            onPressed: () => showModalBottomSheet<void>(
                              context: context,
                              isScrollControlled: true,
                              builder: (context) => SafeArea(
                                child: SingleChildScrollView(
                                  padding: const EdgeInsets.all(24),
                                  child: Column(
                                    mainAxisSize: MainAxisSize.min,
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      const Text(
                                        'Source evidence',
                                        style: TextStyle(
                                          fontSize: 20,
                                          fontWeight: FontWeight.w600,
                                        ),
                                      ),
                                      ...sources.map((raw) {
                                        final source = raw as Map;
                                        return Padding(
                                          padding: const EdgeInsets.symmetric(
                                            vertical: 12,
                                          ),
                                          child: Column(
                                            crossAxisAlignment:
                                                CrossAxisAlignment.start,
                                            children: [
                                              Text(
                                                'Page ${source['page']} · ${source['section'] ?? 'Document'}',
                                              ),
                                              const SizedBox(height: 8),
                                              SelectableText(
                                                source['sourceText']
                                                        as String? ??
                                                    '',
                                              ),
                                              TextButton(
                                                onPressed: () =>
                                                    _openSource(source),
                                                child: Text(
                                                  'Open page ${source['page']}',
                                                ),
                                              ),
                                            ],
                                          ),
                                        );
                                      }),
                                    ],
                                  ),
                                ),
                              ),
                            ),
                          ),
                      ],
                    ),
                  ),
                );
                final expandable =
                    [
                      'clause',
                      'condition',
                      'obligation',
                      'relationship',
                      'interpretation',
                    ].contains(statement['kind']) &&
                    (statement['supportStatus'] == null ||
                        statement['supportStatus'] == 'SUPPORTED');
                return expandable
                    ? ExpansionTile(
                        title: Text(
                          statement['label'] as String? ?? 'Document term',
                        ),
                        children: [card],
                      )
                    : card;
              }),
            ],
          ),
        ),
      ],
    );
  }

  Map<String, List<dynamic>> _groupStatements(List<dynamic> statements) {
    const titles = {
      'finding': 'What matters',
      'warning': 'What matters',
      'fact': 'Key details',
      'entity': 'Key details',
      'date': 'Dates and deadlines',
      'money': 'Financial details',
      'action': 'Suggested next steps',
    };
    final groups = <String, List<dynamic>>{};
    for (final raw in statements) {
      final item = raw as Map;
      final status = item['supportStatus'];
      final title = status != null && status != 'SUPPORTED'
          ? 'Details to verify'
          : titles[item['kind']] ?? 'Terms and conditions';
      groups.putIfAbsent(title, () => []).add(raw);
    }
    const order = [
      'What matters',
      'Key details',
      'Dates and deadlines',
      'Financial details',
      'Terms and conditions',
      'Suggested next steps',
      'Details to verify',
    ];
    return {
      for (final title in order)
        if (groups.containsKey(title)) title: groups[title]!,
    };
  }

  Widget _buildMissingInformation() {
    final items = _analysis?['missingInformation'] as List? ?? [];
    if (items.isEmpty) return const SizedBox.shrink();
    return _buildSection(
      title: 'Information to check',
      icon: Icons.help_outline,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (final item in items)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text('$item'),
            ),
        ],
      ),
    );
  }

  Widget _buildQualityPanel() {
    final quality = _analysis?['qualityDiagnostics'] as Map?;
    if (quality == null) return const SizedBox.shrink();
    const keys = [
      'pageCount',
      'ocrConfidence',
      'evidenceCoverage',
      'unsupportedClaims',
      'uncertainClaims',
      'findingsCount',
      'actionsCount',
      'extractionMs',
      'extractionInferenceMs',
      'reasoningMs',
      'supportValidationMs',
      'understandingMs',
      'storageMs',
      'workerMs',
      'supportMethod',
      'supportModel',
      'reasoningStatus',
      'confidenceCalibration',
    ];
    return ExpansionTile(
      title: const Text('Quality diagnostics · Debug'),
      childrenPadding: const EdgeInsets.all(16),
      children: [
        Text(
          'Provider: ${_analysis?['provider']}\nModel: ${_analysis?['modelVersion']}\nAnalyzer: ${_analysis?['analyzerVersion']}\nConfiguration: ${_analysis?['configurationVersion']}',
        ),
        for (final key in keys)
          if (quality.containsKey(key))
            ListTile(
              dense: true,
              title: Text(key),
              subtitle: Text('${quality[key]}'),
            ),
      ],
    );
  }

  Widget _buildSummarySection() {
    final category = _analysis!['documentCategory'] as String? ?? '';
    final summary = _analysis!['summary'] as String? ?? '';
    if (category.isEmpty && summary.isEmpty) return const SizedBox.shrink();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (category.isNotEmpty)
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            decoration: BoxDecoration(
              color: AppTheme.primaryColor.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(16),
            ),
            child: Text(
              category,
              style: TextStyle(
                color: AppTheme.primaryColor,
                fontSize: 13,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        if (category.isNotEmpty) const SizedBox(height: 12),
        if (summary.isNotEmpty)
          Text(summary, style: const TextStyle(fontSize: 15, height: 1.5)),
        const SizedBox(height: 24),
      ],
    );
  }

  Widget _buildStructuredFieldsSection() {
    final fields = <MapEntry<String, String>>[];

    void addIf(String label, String? key) {
      final value = _analysis![key] as String?;
      if (value != null && value.isNotEmpty) {
        fields.add(MapEntry(label, value));
      }
    }

    addIf('Title', 'title');
    addIf('Person Name', 'personName');
    addIf('Issuing Authority', 'issuingAuthority');
    addIf('Document Number', 'documentNumber');
    addIf('Issue Date', 'issueDate');
    addIf('Expiry Date', 'expiryDate');
    addIf('Effective Date', 'effectiveDate');
    addIf('Status', 'documentStatus');

    if (fields.isEmpty) return const SizedBox.shrink();

    return _buildSection(
      title: 'Document Details',
      icon: Icons.description_outlined,
      child: Column(
        children: fields
            .map(
              (f) => Padding(
                padding: const EdgeInsets.symmetric(vertical: 5),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      flex: 2,
                      child: Text(
                        f.key,
                        style: TextStyle(
                          color: AppTheme.textSecondary,
                          fontSize: 13,
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      flex: 3,
                      child: Text(
                        f.value,
                        style: const TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            )
            .toList(),
      ),
    );
  }

  Widget _buildKeyInformationSection() {
    final items = _analysis!['keyInformation'] as List<dynamic>? ?? [];
    if (items.isEmpty) return const SizedBox.shrink();

    return _buildSection(
      title: 'Key Information',
      icon: Icons.info_outlined,
      child: Column(
        children: items.map<Widget>((item) {
          final map = item as Map<String, dynamic>;
          final isExtracted = map['isExtracted'] as bool? ?? true;
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 6),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  flex: 2,
                  child: Text(
                    map['label'] as String? ?? '',
                    style: TextStyle(
                      color: AppTheme.textSecondary,
                      fontSize: 13,
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  flex: 3,
                  child: Row(
                    children: [
                      Flexible(
                        child: Text(
                          map['value'] as String? ?? '',
                          style: const TextStyle(
                            fontSize: 13,
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                      ),
                      if (!isExtracted) ...[
                        const SizedBox(width: 4),
                        _buildInferredBadge(),
                      ],
                    ],
                  ),
                ),
              ],
            ),
          );
        }).toList(),
      ),
    );
  }

  Widget _buildImportantDatesSection() {
    final items = _analysis!['importantDates'] as List<dynamic>? ?? [];
    final expiryDate = _analysis!['expiryDate'] as String?;
    if (items.isEmpty && expiryDate == null) return const SizedBox.shrink();

    return _buildSection(
      title: 'Important Dates',
      icon: Icons.calendar_today_outlined,
      child: Column(
        children: [
          if (expiryDate != null)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 6),
              child: Row(
                children: [
                  Icon(
                    Icons.warning_amber_outlined,
                    size: 16,
                    color: AppTheme.warningColor,
                  ),
                  const SizedBox(width: 8),
                  Text(
                    'Expiry: ',
                    style: TextStyle(
                      color: AppTheme.textSecondary,
                      fontSize: 13,
                    ),
                  ),
                  Text(
                    expiryDate,
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
              ),
            ),
          ...items.map<Widget>((item) {
            final map = item as Map<String, dynamic>;
            final isExtracted = map['isExtracted'] as bool? ?? true;
            return Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      map['label'] as String? ?? '',
                      style: TextStyle(
                        color: AppTheme.textSecondary,
                        fontSize: 13,
                      ),
                    ),
                  ),
                  Text(
                    map['date'] as String? ?? '',
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                  if (!isExtracted) ...[
                    const SizedBox(width: 4),
                    _buildInferredBadge(),
                  ],
                ],
              ),
            );
          }),
        ],
      ),
    );
  }

  Widget _buildRequiredActionsSection() {
    final items = _analysis!['requiredActions'] as List<dynamic>? ?? [];
    if (items.isEmpty) return const SizedBox.shrink();

    return _buildSection(
      title: 'Required Actions',
      icon: Icons.check_circle_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: items.map<Widget>((item) {
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(Icons.arrow_right, size: 18, color: AppTheme.primaryColor),
                const SizedBox(width: 4),
                Expanded(
                  child: Text(
                    item as String,
                    style: const TextStyle(fontSize: 13),
                  ),
                ),
              ],
            ),
          );
        }).toList(),
      ),
    );
  }

  Widget _buildDeadlinesSection() {
    final items = _analysis!['deadlines'] as List<dynamic>? ?? [];
    if (items.isEmpty) return const SizedBox.shrink();

    return _buildSection(
      title: 'Deadlines',
      icon: Icons.schedule_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: items.map<Widget>((item) {
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(Icons.schedule, size: 16, color: AppTheme.warningColor),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    item as String,
                    style: const TextStyle(fontSize: 13),
                  ),
                ),
              ],
            ),
          );
        }).toList(),
      ),
    );
  }

  Widget _buildWarningsSection() {
    final items = _analysis!['warnings'] as List<dynamic>? ?? [];
    if (items.isEmpty) return const SizedBox.shrink();

    return _buildSection(
      title: 'Warnings',
      icon: Icons.warning_outlined,
      iconColor: AppTheme.warningColor,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: items.map<Widget>((item) {
          return Container(
            margin: const EdgeInsets.symmetric(vertical: 4),
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(
              color: AppTheme.warningColor.withValues(alpha: 0.08),
              borderRadius: BorderRadius.circular(8),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(
                  Icons.warning_amber,
                  size: 16,
                  color: AppTheme.warningColor,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    item as String,
                    style: const TextStyle(fontSize: 13),
                  ),
                ),
              ],
            ),
          );
        }).toList(),
      ),
    );
  }

  Widget _buildTerminologySection() {
    final items = _analysis!['terminology'] as List<dynamic>? ?? [];
    if (items.isEmpty) return const SizedBox.shrink();

    return _buildSection(
      title: 'Terminology',
      icon: Icons.menu_book_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: items.map<Widget>((item) {
          final map = item as Map<String, dynamic>;
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 6),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  map['term'] as String? ?? '',
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  map['explanation'] as String? ?? '',
                  style: TextStyle(
                    fontSize: 13,
                    color: AppTheme.textSecondary,
                    height: 1.4,
                  ),
                ),
              ],
            ),
          );
        }).toList(),
      ),
    );
  }

  Widget _buildExplanationSection() {
    final explanation = _analysis!['plainLanguageExplanation'] as String?;
    if (explanation == null || explanation.isEmpty) {
      return const SizedBox.shrink();
    }

    return _buildSection(
      title: 'What This Means',
      icon: Icons.lightbulb_outlined,
      iconColor: AppTheme.secondaryColor,
      child: Text(
        explanation,
        style: const TextStyle(fontSize: 13, height: 1.5),
      ),
    );
  }

  Widget _buildConfidenceBadge() {
    final confidence = _analysis!['confidence'] as String?;
    if (confidence == null || confidence.isEmpty) {
      return const SizedBox.shrink();
    }

    final (color, icon) = switch (confidence.toLowerCase()) {
      'high' => (AppTheme.successColor, Icons.verified_outlined),
      'medium' => (AppTheme.warningColor, Icons.help_outline),
      _ => (AppTheme.errorColor, Icons.error_outline),
    };

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        children: [
          Icon(icon, size: 16, color: color),
          const SizedBox(width: 6),
          Text(
            'Extraction confidence: ${confidence[0].toUpperCase()}${confidence.substring(1)}',
            style: TextStyle(
              fontSize: 12,
              color: color,
              fontWeight: FontWeight.w500,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildAskButton() {
    return SizedBox(
      height: 48,
      child: OutlinedButton.icon(
        onPressed: () {
          Navigator.push(
            context,
            MaterialPageRoute(
              builder: (_) => DocumentQAScreen(
                document: widget.document,
                documentService: widget.documentService,
              ),
            ),
          );
        },
        icon: Icon(
          Icons.question_answer_outlined,
          color: AppTheme.secondaryColor,
        ),
        label: const Text('Ask About This Document'),
      ),
    );
  }

  Widget _buildSection({
    required String title,
    required IconData icon,
    required Widget child,
    Color? iconColor,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Icon(icon, size: 18, color: iconColor ?? AppTheme.primaryColor),
            const SizedBox(width: 8),
            Text(
              title,
              style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
            ),
          ],
        ),
        const SizedBox(height: 8),
        child,
        const SizedBox(height: 20),
      ],
    );
  }

  Widget _buildInferredBadge() {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
      decoration: BoxDecoration(
        color: AppTheme.accentColor.withValues(alpha: 0.2),
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        'AI',
        style: TextStyle(
          fontSize: 9,
          fontWeight: FontWeight.w600,
          color: AppTheme.secondaryColor,
        ),
      ),
    );
  }

  Widget _buildDisclaimerBanner() {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppTheme.textSecondary.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.info_outline, size: 16, color: AppTheme.textSecondary),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              'This analysis was generated by AI. Facts marked "AI" are inferences, not direct extractions. '
              'Verify important information with the relevant authority.',
              style: TextStyle(
                fontSize: 11,
                color: AppTheme.textSecondary,
                height: 1.4,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
