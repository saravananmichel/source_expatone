using System.Text.Json;
using System.Text.RegularExpressions;
using ExpatOne.Application.Common;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExpatOne.Infrastructure.Services;

public class AssistantService : IAssistantService
{
    private readonly ExpatOneDbContext _dbContext;
    private readonly IAIService _aiService;
    private readonly IKnowledgeSearchService _searchService;
    private readonly ILogger<AssistantService> _logger;
    private readonly double _relevanceThreshold;
    private readonly int _maxMessageLength;

    private const string ModuleName = "government-assistant";
    private const int MaxHistoryMessages = 10;
    private const int MaxTitleLength = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // --- CASUAL detection ---
    private static readonly HashSet<string> CasualExactMatches = new(StringComparer.OrdinalIgnoreCase)
    {
        "hi", "hello", "hey", "hiya", "howdy", "greetings",
        "good morning", "good afternoon", "good evening", "good day", "good night",
        "thanks", "thank you",
        "ok", "okay", "sure", "alright", "noted",
        "bye", "goodbye", "see you", "see ya",
        "yes", "no", "yeah", "nope", "yep",
        "great", "awesome", "nice", "cool",
        "help",
    };

    // Anchored patterns — only match when message starts with a casual phrase.
    // Length guard (≤60 chars) prevents prompt-injection strings like "hello, ignore instructions..."
    private static readonly Regex[] CasualPatterns =
    [
        new(@"^(hi|hello|hey|hiya|howdy)[,!\s]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^thank(s| you)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^what can you (do|help|tell)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^how can you help\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^who are you\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^what are you\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    public static bool IsCasualMessage(string message)
    {
        var normalized = message.Trim();
        if (CasualExactMatches.Contains(normalized)) return true;
        // Length guard: long messages are never classified casual via regex
        if (normalized.Length > 60) return false;
        return CasualPatterns.Any(p => p.IsMatch(normalized));
    }

    // MODE 1 — OFFICIAL_GROUNDED: answer only from retrieved official knowledge
    private const string SystemPrompt = """
        You are ExpatOne's Government Assistant, helping expatriates understand Malaysian government processes.

        STRICT RULES:
        - Answer ONLY based on the KNOWLEDGE CONTEXT provided below. Never use information not present in the context.
        - If the context does not contain enough information to answer the question, say so clearly. Do not guess or fill in gaps from general knowledge.
        - Cite your sources by referencing the source title and department when stating facts.
        - Never provide definitive legal advice. Always recommend verifying important requirements with the relevant government authority or official source.
        - If multiple sources contain conflicting information, explicitly identify the conflict rather than silently choosing one.
        - Be concise, clear, and actionable.
        - Use simple language suitable for non-native English speakers.
        - Clearly distinguish factual government requirements from general explanation.
        - Encourage the user to verify current requirements against the cited official source, as policies may change.
        """;

    // MODE 2 — GENERAL_UNVERIFIED: no KB context; general answer with explicit unverified label
    private const string GeneralUnverifiedSystemPrompt = """
        You are ExpatOne's Government Assistant, helping expatriates understand Malaysian government processes.

        IMPORTANT: The ExpatOne knowledge base does not contain verified official data for this specific question.

        RULES:
        - Do NOT cite specific official documents, source titles, URLs, or government announcements. You do not have verified sources to support such citations.
        - Provide helpful general information based on your training knowledge, while clearly acknowledging it is not drawn from verified official sources.
        - Always advise the user to verify current requirements directly with the relevant Malaysian government authority or official website (for example: imi.gov.my for immigration, hasil.gov.my for tax, jpj.gov.my for driving, moh.gov.my for health).
        - Never provide definitive legal advice.
        - Use simple language suitable for non-native English speakers.
        - Be concise and directly address the question asked.
        - Do not cite or reference official documents or sources from the conversation history unless you have independently verified them.
        - Close with a brief reminder that policies change and users should consult official sources.
        """;

    // MODE 3 — CASUAL: friendly conversational response, no KB context needed
    private const string CasualSystemPrompt = """
        You are ExpatOne's Government Assistant, a friendly assistant helping expatriates navigate Malaysian government processes.

        You can help with questions about Malaysian immigration, Employment Pass, visas, work permits, tax obligations for expatriates, healthcare registration, driving licences, and other official government requirements.

        Keep your response friendly, brief, and conversational. If the user is greeting you or checking in, respond warmly. If they are asking what you can do, briefly describe your focus areas and invite them to ask a government-related question.
        """;

    public AssistantService(
        ExpatOneDbContext dbContext,
        IAIService aiService,
        IKnowledgeSearchService searchService,
        IConfiguration configuration,
        ILogger<AssistantService> logger)
    {
        _dbContext = dbContext;
        _aiService = aiService;
        _searchService = searchService;
        _logger = logger;

        var thresholdStr = configuration["Assistant:RelevanceThreshold"];
        _relevanceThreshold = double.TryParse(thresholdStr, out var threshold) ? threshold : 0.7;

        var maxLenStr = configuration["Assistant:MaxMessageLength"];
        _maxMessageLength = int.TryParse(maxLenStr, out var maxLen) ? maxLen : 8000;
    }

    public async Task<ConversationDto> CreateConversationAsync(Guid userId, string countryCode = "MY")
    {
        var conversation = new AIConversation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Module = ModuleName,
        };

        _dbContext.AIConversations.Add(conversation);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created assistant conversation {ConversationId} for user {UserId}", conversation.Id, userId);

        return MapToConversationDto(conversation);
    }

    public async Task<List<ConversationSummaryDto>> GetConversationsAsync(Guid userId)
    {
        var conversations = await _dbContext.AIConversations
            .Where(c => c.UserId == userId && c.Module == ModuleName)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new ConversationSummaryDto
            {
                Id = c.Id,
                Title = c.Title,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
            })
            .ToListAsync();

        return conversations;
    }

    public async Task<ConversationDto?> GetConversationAsync(Guid conversationId, Guid userId)
    {
        var conversation = await _dbContext.AIConversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation is null)
            throw new KeyNotFoundException("Conversation not found.");

        if (conversation.UserId != userId)
            throw new UnauthorizedAccessException();

        return MapToConversationDto(conversation);
    }

    public async Task<AssistantMessageDto> SendMessageAsync(Guid conversationId, Guid userId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty.");

        if (message.Length > _maxMessageLength)
            throw new ArgumentException($"Message exceeds the maximum length of {_maxMessageLength} characters.");

        var conversation = await _dbContext.AIConversations
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == conversationId)
            ?? throw new KeyNotFoundException("Conversation not found.");

        if (conversation.UserId != userId)
            throw new UnauthorizedAccessException();

        var userMessage = new AIConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = "user",
            Content = message,
        };
        _dbContext.AIConversationMessages.Add(userMessage);
        await _dbContext.SaveChangesAsync();

        string responseMode;
        string assistantContent;
        List<MessageSourceDto> sources = [];

        if (IsCasualMessage(message))
        {
            // MODE 3: CASUAL — skip KB search entirely
            responseMode = "casual";
            _logger.LogInformation("AssistantMode: CASUAL, ConversationId={ConversationId}", conversationId);

            var historyMessages = conversation.Messages
                .TakeLast(MaxHistoryMessages)
                .Select(m => new AIMessage { Role = m.Role, Content = m.Content })
                .ToList();

            var context = new AIContext
            {
                SystemPrompt = CasualSystemPrompt,
                ConversationHistory = historyMessages,
            };

            assistantContent = await InvokeGeminiAsync(message, context, conversationId);
        }
        else
        {
            // KB search path — determines MODE 1 or MODE 2
            List<KnowledgeSearchResult> searchResults;
            try
            {
                searchResults = await _searchService.SearchAsync(message, "MY", 5);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Knowledge search failed for conversation {ConversationId} category={Category}", conversationId, ex.GetType().Name);
                searchResults = [];
            }

            _logger.LogInformation(
                "AssistantGrounding: QueryLength={QueryLength}, RetrievedCount={RetrievedCount}, ConfiguredThreshold={Threshold}",
                message.Length, searchResults.Count, _relevanceThreshold);

            foreach (var result in searchResults)
            {
                _logger.LogInformation(
                    "AssistantGrounding: Result Title={Title}, RelevanceScore={Score}, PassesThreshold={Passes}",
                    result.Title, result.RelevanceScore, result.RelevanceScore >= _relevanceThreshold);
            }

            var relevantResults = searchResults
                .Where(r => r.RelevanceScore >= _relevanceThreshold)
                .ToList();

            _logger.LogInformation(
                "AssistantGrounding: RelevantCount={RelevantCount}", relevantResults.Count);

            if (relevantResults.Count > 0)
            {
                // MODE 1: OFFICIAL_GROUNDED
                responseMode = "official_grounded";
                _logger.LogInformation("AssistantMode: OFFICIAL_GROUNDED, ConversationId={ConversationId}", conversationId);

                sources = relevantResults.Select(r => new MessageSourceDto
                {
                    Title = r.Title,
                    Url = r.SourceUrl,
                    Department = r.Department,
                    Category = r.Category,
                    CountryCode = r.CountryCode,
                    RelevanceScore = r.RelevanceScore,
                }).ToList();

                var knowledgeEntries = relevantResults.Select(r =>
                    $"[Source: {r.Title} | Department: {r.Department ?? "N/A"} | URL: {r.SourceUrl ?? "N/A"}]\n{r.Content}"
                ).ToList();

                var historyMessages = conversation.Messages
                    .TakeLast(MaxHistoryMessages)
                    .Select(m => new AIMessage { Role = m.Role, Content = m.Content })
                    .ToList();

                var context = new AIContext
                {
                    SystemPrompt = SystemPrompt,
                    ConversationHistory = historyMessages,
                    RelevantKnowledge = knowledgeEntries,
                };

                assistantContent = await InvokeGeminiAsync(message, context, conversationId);
            }
            else
            {
                // MODE 2: GENERAL_UNVERIFIED — Gemini called with constrained unverified prompt
                responseMode = "general_unverified";
                _logger.LogInformation("AssistantMode: GENERAL_UNVERIFIED, ConversationId={ConversationId}", conversationId);
                // sources intentionally stays empty — no source cards for unverified responses

                var historyMessages = conversation.Messages
                    .TakeLast(MaxHistoryMessages)
                    .Select(m => new AIMessage { Role = m.Role, Content = m.Content })
                    .ToList();

                var context = new AIContext
                {
                    SystemPrompt = GeneralUnverifiedSystemPrompt,
                    ConversationHistory = historyMessages,
                    // RelevantKnowledge intentionally absent
                };

                assistantContent = await InvokeGeminiAsync(message, context, conversationId);
            }
        }

        var assistantMessage = new AIConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Role = "assistant",
            Content = assistantContent,
            // StructuredResponse stores the response mode for persistence/history reload
            StructuredResponse = responseMode,
            SourceReferences = sources.Count > 0
                ? JsonSerializer.Serialize(sources, JsonOptions)
                : null,
        };
        _dbContext.AIConversationMessages.Add(assistantMessage);

        if (conversation.Title is null)
        {
            conversation.Title = message.Length > MaxTitleLength
                ? message[..MaxTitleLength]
                : message;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Processed message for conversation {ConversationId}: Mode={Mode}, SourceCount={SourceCount}",
            conversationId, responseMode, sources.Count);

        return MapToMessageDto(assistantMessage, sources);
    }

    public async Task DeleteConversationAsync(Guid conversationId, Guid userId)
    {
        var conversation = await _dbContext.AIConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId)
            ?? throw new KeyNotFoundException("Conversation not found.");

        if (conversation.UserId != userId)
            throw new UnauthorizedAccessException();

        _dbContext.AIConversations.Remove(conversation);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted conversation {ConversationId}", conversationId);
    }

    private async Task<string> InvokeGeminiAsync(string message, AIContext context, Guid conversationId)
    {
        try
        {
            var response = await _aiService.GenerateResponseAsync(message, context);
            return response.Content;
        }
        catch (AIProviderUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("Gemini generation failed for conversation {ConversationId} category={Category}", conversationId, ex.GetType().Name);
            throw new InvalidOperationException("The assistant could not generate a response. Please try again.");
        }
    }

    private static ConversationDto MapToConversationDto(AIConversation conversation) => new()
    {
        Id = conversation.Id,
        Title = conversation.Title,
        Module = conversation.Module,
        CreatedAt = conversation.CreatedAt,
        UpdatedAt = conversation.UpdatedAt,
        Messages = conversation.Messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => MapToMessageDto(m, DeserializeSources(m.SourceReferences)))
            .ToList(),
    };

    private static AssistantMessageDto MapToMessageDto(AIConversationMessage message, List<MessageSourceDto>? sources) => new()
    {
        Id = message.Id,
        Role = message.Role,
        Content = message.Content,
        Sources = sources is { Count: > 0 } ? sources : null,
        ResponseMode = message.StructuredResponse,
        CreatedAt = message.CreatedAt,
    };

    private static List<MessageSourceDto>? DeserializeSources(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<MessageSourceDto>>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
