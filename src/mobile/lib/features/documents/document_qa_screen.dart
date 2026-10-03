import 'package:flutter/material.dart';
import '../../core/services/document_service.dart';
import '../../core/theme/app_theme.dart';

class DocumentQAScreen extends StatefulWidget {
  final DocumentItem document;
  final DocumentService documentService;

  const DocumentQAScreen({
    super.key,
    required this.document,
    required this.documentService,
  });

  @override
  State<DocumentQAScreen> createState() => _DocumentQAScreenState();
}

class _DocumentQAScreenState extends State<DocumentQAScreen> {
  final _controller = TextEditingController();
  final _scrollController = ScrollController();
  final _focusNode = FocusNode();
  final List<_QAEntry> _history = [];
  bool _isLoading = false;
  String? _error;

  static const _maxQuestionLength = 2000;
  static const _exampleQuestions = [
    'What are the key dates in this document?',
    'Who issued this document?',
    'Are there any renewal requirements?',
    'What conditions or restrictions apply?',
  ];

  @override
  void dispose() {
    _controller.dispose();
    _scrollController.dispose();
    _focusNode.dispose();
    super.dispose();
  }

  Future<void> _askQuestion() async {
    final question = _controller.text.trim();
    if (question.isEmpty) return;
    if (question.length > _maxQuestionLength) {
      setState(() {
        _error = 'Question must be under $_maxQuestionLength characters.';
      });
      return;
    }

    setState(() {
      _history.add(_QAEntry(question: question));
      _controller.clear();
      _isLoading = true;
      _error = null;
    });

    _scrollToBottom();

    try {
      final answer = await widget.documentService.askDocument(
        widget.document.id,
        question,
      );
      if (mounted) {
        setState(() {
          _history.last = _QAEntry(
            question: question,
            answer: answer.answer,
            grounded: answer.grounded,
            evidence: answer.evidence,
          );
          _isLoading = false;
        });
        _scrollToBottom();
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _history.last = _QAEntry(
            question: question,
            error: 'Unable to get an answer. Please try again.',
          );
          _isLoading = false;
        });
      }
    }
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollController.hasClients) {
        _scrollController.animateTo(
          _scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 300),
          curve: Curves.easeOut,
        );
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(
          'Ask: ${widget.document.name}',
          overflow: TextOverflow.ellipsis,
        ),
      ),
      body: Column(
        children: [
          Expanded(
            child: _history.isEmpty ? _buildEmptyState() : _buildHistory(),
          ),
          _buildInputBar(),
        ],
      ),
    );
  }

  Widget _buildEmptyState() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(24),
      child: Column(
        children: [
          const SizedBox(height: 32),
          Icon(Icons.question_answer_outlined, size: 56,
              color: AppTheme.textSecondary.withValues(alpha: 0.4)),
          const SizedBox(height: 16),
          Text(
            'Ask a question about this document',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w600,
              color: AppTheme.textPrimary,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Answers are based only on the content of your document.',
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 13, color: AppTheme.textSecondary),
          ),
          const SizedBox(height: 24),
          Text('Try asking:', style: TextStyle(
              fontSize: 13, color: AppTheme.textSecondary, fontWeight: FontWeight.w500)),
          const SizedBox(height: 12),
          ..._exampleQuestions.map((q) => Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: InkWell(
              onTap: () {
                _controller.text = q;
                _focusNode.requestFocus();
              },
              borderRadius: BorderRadius.circular(8),
              child: Container(
                width: double.infinity,
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                decoration: BoxDecoration(
                  border: Border.all(color: AppTheme.accentColor.withValues(alpha: 0.4)),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Text(q, style: TextStyle(fontSize: 13, color: AppTheme.primaryColor)),
              ),
            ),
          )),
        ],
      ),
    );
  }

  Widget _buildHistory() {
    return ListView.builder(
      controller: _scrollController,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      itemCount: _history.length + (_isLoading ? 1 : 0),
      itemBuilder: (context, index) {
        if (index == _history.length) {
          return _buildLoadingBubble();
        }
        return _buildQAEntry(_history[index]);
      },
    );
  }

  Widget _buildQAEntry(_QAEntry entry) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Align(
          alignment: Alignment.centerRight,
          child: Container(
            margin: const EdgeInsets.only(bottom: 8, left: 48),
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
            decoration: BoxDecoration(
              color: AppTheme.primaryColor,
              borderRadius: BorderRadius.circular(16),
            ),
            child: Text(entry.question,
                style: const TextStyle(color: Colors.white, fontSize: 14)),
          ),
        ),
        if (entry.answer != null) ...[
          Align(
            alignment: Alignment.centerLeft,
            child: Container(
              margin: const EdgeInsets.only(bottom: 4, right: 48),
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              decoration: BoxDecoration(
                color: AppTheme.surfaceColor,
                border: Border.all(color: const Color(0xFFE8E8E8)),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Text(entry.answer!,
                  style: TextStyle(color: AppTheme.textPrimary, fontSize: 14, height: 1.5)),
            ),
          ),
          ...entry.evidence.map((e) => Padding(
            padding: const EdgeInsets.only(left: 4, right: 48, bottom: 8),
            child: Text('Page ${e.page}: “${e.sourceText}”',
                style: TextStyle(fontSize: 12, color: AppTheme.textSecondary)),
          )),
          if (entry.grounded == false)
            Padding(
              padding: const EdgeInsets.only(bottom: 12, left: 4),
              child: Row(
                children: [
                  Icon(Icons.info_outline, size: 13, color: AppTheme.warningColor),
                  const SizedBox(width: 4),
                  Text('This answer could not be fully grounded in the document.',
                      style: TextStyle(fontSize: 11, color: AppTheme.warningColor)),
                ],
              ),
            )
          else
            const SizedBox(height: 12),
        ],
        if (entry.error != null) ...[
          Align(
            alignment: Alignment.centerLeft,
            child: Container(
              margin: const EdgeInsets.only(bottom: 12, right: 48),
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              decoration: BoxDecoration(
                color: AppTheme.errorColor.withValues(alpha: 0.08),
                borderRadius: BorderRadius.circular(16),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.error_outline, size: 16, color: AppTheme.errorColor),
                  const SizedBox(width: 8),
                  Flexible(child: Text(entry.error!,
                      style: TextStyle(color: AppTheme.errorColor, fontSize: 13))),
                ],
              ),
            ),
          ),
        ],
      ],
    );
  }

  Widget _buildLoadingBubble() {
    return Align(
      alignment: Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.only(right: 48, bottom: 12),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
        decoration: BoxDecoration(
          color: AppTheme.surfaceColor,
          border: Border.all(color: const Color(0xFFE8E8E8)),
          borderRadius: BorderRadius.circular(16),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            SizedBox(
              width: 16, height: 16,
              child: CircularProgressIndicator(
                strokeWidth: 2,
                color: AppTheme.secondaryColor,
              ),
            ),
            const SizedBox(width: 10),
            Text('Reading document...',
                style: TextStyle(color: AppTheme.textSecondary, fontSize: 13)),
          ],
        ),
      ),
    );
  }

  Widget _buildInputBar() {
    return Container(
      padding: EdgeInsets.only(
        left: 12, right: 8, top: 8,
        bottom: MediaQuery.of(context).padding.bottom + 8,
      ),
      decoration: BoxDecoration(
        color: AppTheme.surfaceColor,
        border: Border(top: BorderSide(color: const Color(0xFFE8E8E8))),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 6),
              child: Text(_error!, style: TextStyle(color: AppTheme.errorColor, fontSize: 12)),
            ),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _controller,
                  focusNode: _focusNode,
                  maxLength: _maxQuestionLength,
                  maxLines: 3,
                  minLines: 1,
                  textInputAction: TextInputAction.send,
                  onSubmitted: (_) => _askQuestion(),
                  decoration: InputDecoration(
                    hintText: 'Ask about this document...',
                    counterText: '',
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(24),
                      borderSide: BorderSide(color: const Color(0xFFE8E8E8)),
                    ),
                    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                  ),
                ),
              ),
              const SizedBox(width: 8),
              IconButton(
                onPressed: _isLoading ? null : _askQuestion,
                icon: Icon(Icons.send, color: _isLoading ? AppTheme.textSecondary : AppTheme.primaryColor),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _QAEntry {
  final String question;
  final String? answer;
  final bool? grounded;
  final String? error;
  final List<DocumentAnswerEvidence> evidence;

  _QAEntry({required this.question, this.answer, this.grounded, this.error, this.evidence = const []});
}
