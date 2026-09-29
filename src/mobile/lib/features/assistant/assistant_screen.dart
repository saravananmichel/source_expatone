import 'dart:async';

import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../core/error/app_exception.dart';
import '../../core/services/assistant_service.dart';
import '../../core/theme/app_theme.dart';

class AssistantScreen extends StatefulWidget {
  final AssistantService assistantService;

  const AssistantScreen({super.key, required this.assistantService});

  @override
  State<AssistantScreen> createState() => _AssistantScreenState();
}

class _AssistantScreenState extends State<AssistantScreen> {
  final _controller = TextEditingController();
  final _scrollController = ScrollController();

  String? _conversationId;
  final List<AssistantMessage> _messages = [];
  bool _isSending = false;
  String? _error;

  @override
  void dispose() {
    _controller.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  Future<void> _sendMessage(String text) async {
    final message = text.trim();
    if (message.isEmpty || _isSending) return;

    _controller.clear();
    setState(() {
      _isSending = true;
      _error = null;
    });

    try {
      if (_conversationId == null) {
        final conversation =
            await widget.assistantService.createConversation();
        _conversationId = conversation.id;
      }

      _messages.add(AssistantMessage(
        id: 'temp-${DateTime.now().millisecondsSinceEpoch}',
        role: 'user',
        content: message,
        createdAt: DateTime.now(),
      ));
      setState(() {});
      _scrollToBottom();

      final response = await widget.assistantService.sendMessage(
        _conversationId!,
        message,
      );

      if (mounted) {
        setState(() {
          _messages.add(response);
          _isSending = false;
        });
        _scrollToBottom();
      }
    } on NetworkException catch (e) {
      if (mounted) {
        setState(() {
          _error = (e.statusCode == 503)
              ? 'The Government Assistant is temporarily unavailable. Please try again in a moment.'
              : e.message;
          _isSending = false;
        });
      }
    } on TimeoutException {
      if (mounted) {
        setState(() {
          _error =
              'The Government Assistant is temporarily unavailable. Please try again in a moment.';
          _isSending = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = 'The Government Assistant is temporarily unavailable. Please try again in a moment.';
          _isSending = false;
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

  void _startNewConversation() {
    setState(() {
      _conversationId = null;
      _messages.clear();
      _error = null;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Government Assistant'),
        actions: [
          if (_messages.isNotEmpty)
            IconButton(
              icon: const Icon(Icons.add_comment_outlined),
              onPressed: _startNewConversation,
              tooltip: 'New conversation',
            ),
        ],
      ),
      body: Column(
        children: [
          Expanded(
            child: _messages.isEmpty
                ? _buildEmptyState()
                : _buildMessageList(),
          ),
          if (_error != null) _buildErrorBanner(),
          _buildInputArea(),
        ],
      ),
    );
  }

  Widget _buildEmptyState() {
    const examples = [
      'What is the minimum salary for Employment Pass Category II?',
      'What documents do I need for an Employment Pass?',
      'How long can an Employment Pass be issued for?',
    ];

    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              Icons.account_balance,
              size: 56,
              color: AppTheme.primaryColor.withValues(alpha: 0.6),
            ),
            const SizedBox(height: 16),
            Text(
              'Ask about Malaysian\ngovernment requirements',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text(
              'Answers are based on official government sources in the ExpatOne knowledge base.',
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 24),
            ...examples.map((q) => Padding(
                  padding: const EdgeInsets.only(bottom: 8),
                  child: SizedBox(
                    width: double.infinity,
                    child: OutlinedButton(
                      onPressed: () => _sendMessage(q),
                      style: OutlinedButton.styleFrom(
                        alignment: Alignment.centerLeft,
                        padding: const EdgeInsets.symmetric(
                            horizontal: 16, vertical: 12),
                      ),
                      child: Text(q, style: const TextStyle(fontSize: 13)),
                    ),
                  ),
                )),
          ],
        ),
      ),
    );
  }

  Widget _buildMessageList() {
    return ListView.builder(
      controller: _scrollController,
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      itemCount: _messages.length + (_isSending ? 1 : 0),
      itemBuilder: (context, index) {
        if (index == _messages.length) {
          return _buildTypingIndicator();
        }
        return _buildMessageBubble(_messages[index]);
      },
    );
  }

  Widget _buildMessageBubble(AssistantMessage message) {
    final isUser = message.isUser;

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Column(
        crossAxisAlignment:
            isUser ? CrossAxisAlignment.end : CrossAxisAlignment.start,
        children: [
          Container(
            constraints: BoxConstraints(
              maxWidth: MediaQuery.of(context).size.width * 0.8,
            ),
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
            decoration: BoxDecoration(
              color: isUser
                  ? AppTheme.primaryColor
                  : AppTheme.surfaceColor,
              borderRadius: BorderRadius.circular(16).copyWith(
                bottomRight: isUser ? const Radius.circular(4) : null,
                bottomLeft: !isUser ? const Radius.circular(4) : null,
              ),
              border: isUser
                  ? null
                  : Border.all(color: const Color(0xFFE8E8E8)),
            ),
            child: SelectableText(
              message.content,
              style: TextStyle(
                color: isUser ? Colors.white : AppTheme.textPrimary,
                fontSize: 14,
                height: 1.4,
              ),
            ),
          ),
          if (!isUser && message.isGeneralUnverified)
            _buildUnverifiedBanner(),
          if (message.sources != null && message.sources!.isNotEmpty)
            _buildSourceCards(message.sources!),
        ],
      ),
    );
  }

  Widget _buildSourceCards(List<MessageSource> sources) {
    return Padding(
      padding: const EdgeInsets.only(top: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.only(left: 4, bottom: 4),
            child: Text(
              'Sources',
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.w600,
                color: AppTheme.textSecondary,
              ),
            ),
          ),
          ...sources.map((source) => Container(
                margin: const EdgeInsets.only(bottom: 4),
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
                constraints: BoxConstraints(
                  maxWidth: MediaQuery.of(context).size.width * 0.8,
                ),
                decoration: BoxDecoration(
                  color: AppTheme.accentColor.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(
                    color: AppTheme.accentColor.withValues(alpha: 0.2),
                  ),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    if (source.title != null)
                      Text(
                        source.title!,
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    if (source.department != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 2),
                        child: Text(
                          source.department!,
                          style: TextStyle(
                            fontSize: 11,
                            color: AppTheme.textSecondary,
                          ),
                        ),
                      ),
                    if (source.url != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 4),
                        child: GestureDetector(
                          onTap: () => _openUrl(source.url!),
                          child: Text(
                            'View official source',
                            style: TextStyle(
                              fontSize: 11,
                              color: AppTheme.secondaryColor,
                              decoration: TextDecoration.underline,
                            ),
                          ),
                        ),
                      ),
                  ],
                ),
              )),
        ],
      ),
    );
  }

  Widget _buildUnverifiedBanner() {
    return Padding(
      padding: const EdgeInsets.only(top: 6),
      child: Container(
        constraints: BoxConstraints(
          maxWidth: MediaQuery.of(context).size.width * 0.8,
        ),
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
        decoration: BoxDecoration(
          color: AppTheme.warningColor.withValues(alpha: 0.1),
          borderRadius: BorderRadius.circular(8),
          border: Border.all(
            color: AppTheme.warningColor.withValues(alpha: 0.3),
          ),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.info_outline, size: 14, color: AppTheme.warningColor),
            const SizedBox(width: 6),
            Flexible(
              child: Text(
                'General information — not from official sources. Verify with the relevant authority.',
                style: TextStyle(fontSize: 11, color: AppTheme.warningColor),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildTypingIndicator() {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Align(
        alignment: Alignment.centerLeft,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          decoration: BoxDecoration(
            color: AppTheme.surfaceColor,
            borderRadius:
                BorderRadius.circular(16).copyWith(
                  bottomLeft: const Radius.circular(4),
                ),
            border: Border.all(color: const Color(0xFFE8E8E8)),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              SizedBox(
                width: 16,
                height: 16,
                child: CircularProgressIndicator(
                  strokeWidth: 2,
                  color: AppTheme.primaryColor,
                ),
              ),
              const SizedBox(width: 8),
              Text(
                'Searching official sources...',
                style: TextStyle(
                  fontSize: 13,
                  color: AppTheme.textSecondary,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildErrorBanner() {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
      color: AppTheme.errorColor.withValues(alpha: 0.1),
      child: Row(
        children: [
          Icon(Icons.error_outline, size: 16, color: AppTheme.errorColor),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              _error!,
              style: TextStyle(fontSize: 12, color: AppTheme.errorColor),
            ),
          ),
          IconButton(
            icon: const Icon(Icons.close, size: 16),
            onPressed: () => setState(() => _error = null),
            padding: EdgeInsets.zero,
            constraints: const BoxConstraints(),
          ),
        ],
      ),
    );
  }

  Widget _buildInputArea() {
    return Container(
      padding: EdgeInsets.only(
        left: 16,
        right: 8,
        top: 8,
        bottom: MediaQuery.of(context).padding.bottom + 8,
      ),
      decoration: const BoxDecoration(
        color: Colors.white,
        border: Border(top: BorderSide(color: Color(0xFFE8E8E8))),
      ),
      child: Row(
        children: [
          Expanded(
            child: TextField(
              controller: _controller,
              enabled: !_isSending,
              textInputAction: TextInputAction.send,
              onSubmitted: _sendMessage,
              maxLines: 3,
              minLines: 1,
              decoration: const InputDecoration(
                hintText: 'Ask about government requirements...',
                border: InputBorder.none,
                contentPadding: EdgeInsets.symmetric(vertical: 10),
              ),
              style: const TextStyle(fontSize: 14),
            ),
          ),
          IconButton(
            icon: Icon(
              Icons.send,
              color: _isSending
                  ? AppTheme.textSecondary
                  : AppTheme.primaryColor,
            ),
            onPressed: _isSending
                ? null
                : () => _sendMessage(_controller.text),
          ),
        ],
      ),
    );
  }

  Future<void> _openUrl(String url) async {
    final uri = Uri.tryParse(url);
    if (uri != null && await canLaunchUrl(uri)) {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
    }
  }
}
