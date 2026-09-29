import '../constants/api_constants.dart';
import '../networking/api_client.dart';

class MessageSource {
  final String? title;
  final String? url;
  final String? department;
  final String? category;
  final String? countryCode;
  final double relevanceScore;

  const MessageSource({
    this.title,
    this.url,
    this.department,
    this.category,
    this.countryCode,
    this.relevanceScore = 0,
  });

  factory MessageSource.fromJson(Map<String, dynamic> json) => MessageSource(
        title: json['title'] as String?,
        url: json['url'] as String?,
        department: json['department'] as String?,
        category: json['category'] as String?,
        countryCode: json['countryCode'] as String?,
        relevanceScore: (json['relevanceScore'] as num?)?.toDouble() ?? 0,
      );
}

class AssistantMessage {
  final String id;
  final String role;
  final String content;
  final List<MessageSource>? sources;
  // "official_grounded" | "general_unverified" | "casual" — null for user messages
  final String? responseMode;
  final DateTime createdAt;

  const AssistantMessage({
    required this.id,
    required this.role,
    required this.content,
    this.sources,
    this.responseMode,
    required this.createdAt,
  });

  factory AssistantMessage.fromJson(Map<String, dynamic> json) =>
      AssistantMessage(
        id: json['id'] as String,
        role: json['role'] as String,
        content: json['content'] as String,
        sources: (json['sources'] as List<dynamic>?)
            ?.map((s) => MessageSource.fromJson(s as Map<String, dynamic>))
            .toList(),
        responseMode: json['responseMode'] as String?,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );

  bool get isUser => role == 'user';
  bool get isAssistant => role == 'assistant';
  bool get isGeneralUnverified => responseMode == 'general_unverified';
}

class ConversationSummary {
  final String id;
  final String? title;
  final DateTime createdAt;
  final DateTime updatedAt;

  const ConversationSummary({
    required this.id,
    this.title,
    required this.createdAt,
    required this.updatedAt,
  });

  factory ConversationSummary.fromJson(Map<String, dynamic> json) =>
      ConversationSummary(
        id: json['id'] as String,
        title: json['title'] as String?,
        createdAt: DateTime.parse(json['createdAt'] as String),
        updatedAt: DateTime.parse(json['updatedAt'] as String),
      );
}

class Conversation {
  final String id;
  final String? title;
  final String module;
  final DateTime createdAt;
  final DateTime updatedAt;
  final List<AssistantMessage> messages;

  const Conversation({
    required this.id,
    this.title,
    required this.module,
    required this.createdAt,
    required this.updatedAt,
    required this.messages,
  });

  factory Conversation.fromJson(Map<String, dynamic> json) => Conversation(
        id: json['id'] as String,
        title: json['title'] as String?,
        module: json['module'] as String? ?? 'government-assistant',
        createdAt: DateTime.parse(json['createdAt'] as String),
        updatedAt: DateTime.parse(json['updatedAt'] as String),
        messages: (json['messages'] as List<dynamic>?)
                ?.map(
                    (m) => AssistantMessage.fromJson(m as Map<String, dynamic>))
                .toList() ??
            [],
      );
}

class AssistantService {
  final ApiClient _apiClient;

  AssistantService(this._apiClient);

  Future<Conversation> createConversation() async {
    final response = await _apiClient.post('/assistant/conversations', body: {
      'countryCode': 'MY',
    });
    return Conversation.fromJson(response);
  }

  Future<List<ConversationSummary>> getConversations() async {
    final response = await _apiClient.getList('/assistant/conversations');
    return response.map((e) => ConversationSummary.fromJson(e)).toList();
  }

  Future<Conversation> getConversation(String id) async {
    final response = await _apiClient.get('/assistant/conversations/$id');
    return Conversation.fromJson(response);
  }

  Future<AssistantMessage> sendMessage(
      String conversationId, String message) async {
    final response = await _apiClient.post(
      '/assistant/conversations/$conversationId/messages',
      body: {'message': message},
      timeout: ApiConstants.aiTimeout,
    );
    return AssistantMessage.fromJson(response);
  }

  Future<void> deleteConversation(String id) async {
    await _apiClient.delete('/assistant/conversations/$id');
  }
}
