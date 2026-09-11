namespace ExpatOne.Application.Interfaces;

public interface IAIService
{
    Task<AIResponse> GenerateResponseAsync(string prompt, AIContext? context = null);
    Task<AIResponse> AnalyzeDocumentAsync(Stream documentStream, string contentType, string? userQuestion = null);
    Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
    Task<float[]> GenerateEmbeddingAsync(string text);
}

public class AIResponse
{
    public required string Content { get; set; }
    public string? StructuredJson { get; set; }
    public List<string> SourceReferences { get; set; } = [];
    public float? ConfidenceScore { get; set; }
}

public class AIContext
{
    public string? SystemPrompt { get; set; }
    public List<AIMessage> ConversationHistory { get; set; } = [];
    public List<string> RelevantKnowledge { get; set; } = [];
}

public class AIMessage
{
    public required string Role { get; set; }
    public required string Content { get; set; }
}
