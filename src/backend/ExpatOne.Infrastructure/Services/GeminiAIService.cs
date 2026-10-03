using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpatOne.Application.Common;
using ExpatOne.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExpatOne.Infrastructure.Services;

public class GeminiAIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiAIService> _logger;

    private const int MaxRetries = 3;
    private static readonly int[] RetryDelaysMs = [500, 1000, 2000];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _embeddingModel;
    private const int EmbeddingDimensions = 768;

    internal static bool IsTransientStatusCode(int statusCode) =>
        statusCode == 429 || statusCode == 503 || statusCode >= 500;

    public GeminiAIService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiAIService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API key is not configured.");
        _model = configuration["Gemini:Model"] ?? "gemini-2.5-flash";
        _embeddingModel = configuration["Gemini:EmbeddingModel"] ?? "gemini-embedding-2";
    }

    private HttpRequestMessage BuildGeminiRequest(string endpoint, string jsonPayload)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{endpoint}";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-goog-api-key", _apiKey);
        return request;
    }

    private HttpRequestMessage BuildGeminiEmbeddingRequest(string jsonPayload)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_embeddingModel}:embedContent";
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-goog-api-key", _apiKey);
        return request;
    }

    public Task<AIResponse> AnalyzeDocumentAsync(Stream documentStream, string contentType, string? userQuestion = null)
        => throw new NotSupportedException("Document AI is available only through private document-ai/Ollama.");

    public async Task<AIResponse> GenerateResponseAsync(string prompt, AIContext? context = null)
    {
        var contents = new List<object>();

        if (context?.ConversationHistory is { Count: > 0 } history)
        {
            foreach (var msg in history)
            {
                var role = msg.Role == "assistant" ? "model" : msg.Role;
                contents.Add(new { role, parts = new[] { new { text = msg.Content } } });
            }
        }

        contents.Add(new { role = "user", parts = new[] { new { text = prompt } } });

        var systemText = BuildAssistantSystemPrompt(context);

        var requestBody = new Dictionary<string, object>
        {
            ["contents"] = contents,
        };

        if (!string.IsNullOrEmpty(systemText))
        {
            requestBody["system_instruction"] = new { parts = new[] { new { text = systemText } } };
        }

        var jsonPayload = JsonSerializer.Serialize(requestBody, JsonOptions);

        _logger.LogInformation("Gemini request: model={Model}, endpoint=generateContent (assistant)", _model);

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var request = BuildGeminiRequest($"{_model}:generateContent", jsonPayload);
                response = await _httpClient.SendAsync(request);
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Gemini API request timed out for assistant response");
                throw new AIProviderUnavailableException("The Government Assistant is temporarily unavailable. Please try again in a moment.");
            }
            catch (HttpRequestException)
            {
                _logger.LogError("Network error communicating with Gemini API for assistant");
                throw new AIProviderUnavailableException("The Government Assistant is temporarily unavailable. Please try again in a moment.");
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Gemini response: HTTP 200, model={Model}", _model);
                var responseBody = await response.Content.ReadAsStringAsync();
                var text = ExtractTextFromGeminiResponse(responseBody);
                return new AIResponse { Content = text };
            }

            var statusCode = (int)response.StatusCode;

            if (!IsTransientStatusCode(statusCode))
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Gemini API permanent error: StatusCode={StatusCode}, Model={Model}",
                    statusCode, _model);
                throw statusCode switch
                {
                    400 => new ArgumentException("The question could not be processed. Please try rephrasing."),
                    401 or 403 => new InvalidOperationException("Assistant service configuration error."),
                    _ => new InvalidOperationException("The assistant could not generate a response. Please try again.")
                };
            }

            var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds
                ?? response.Headers.RetryAfter?.Date?.Subtract(DateTimeOffset.UtcNow).TotalSeconds;

            if (attempt < MaxRetries)
            {
                _logger.LogWarning(
                    "Gemini transient error: HTTP {StatusCode}, model={Model}, retrying attempt {Attempt}/{MaxRetries}, RetryAfter={RetryAfter}",
                    statusCode, _model, attempt + 1, MaxRetries, retryAfter);
                await Task.Delay(RetryDelaysMs[attempt]);
            }
            else
            {
                _logger.LogError(
                    "Gemini transient error: HTTP {StatusCode}, model={Model}, persisted after {MaxRetries} retries, RetryAfter={RetryAfter}",
                    statusCode, _model, MaxRetries, retryAfter);
                throw new AIProviderUnavailableException(
                    "The Government Assistant is temporarily unavailable. Please try again in a moment.");
            }
        }

        throw new AIProviderUnavailableException(
            "The Government Assistant is temporarily unavailable. Please try again in a moment.");
    }

    private static string BuildAssistantSystemPrompt(AIContext? context)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(context?.SystemPrompt))
            sb.AppendLine(context.SystemPrompt);

        if (context?.RelevantKnowledge is { Count: > 0 } knowledge)
        {
            sb.AppendLine();
            sb.AppendLine("KNOWLEDGE CONTEXT:");
            sb.AppendLine();
            foreach (var entry in knowledge)
            {
                sb.AppendLine(entry);
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
        var systemPrompt = BuildTranslationSystemPrompt(sourceLanguage, targetLanguage);

        var requestBody = new Dictionary<string, object>
        {
            ["contents"] = new[] { new { role = "user", parts = new[] { new { text } } } },
            ["system_instruction"] = new { parts = new[] { new { text = systemPrompt } } },
        };

        var jsonPayload = JsonSerializer.Serialize(requestBody, JsonOptions);

        _logger.LogInformation("Gemini request: model={Model}, endpoint=generateContent (translation)", _model);

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var request = BuildGeminiRequest($"{_model}:generateContent", jsonPayload);
                response = await _httpClient.SendAsync(request);
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Gemini API request timed out for translation");
                throw new AIProviderUnavailableException("Translation is temporarily unavailable. Please try again in a moment.");
            }
            catch (HttpRequestException)
            {
                _logger.LogError("Network error communicating with Gemini API for translation");
                throw new AIProviderUnavailableException("Translation is temporarily unavailable. Please try again in a moment.");
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Gemini response: HTTP 200, model={Model} (translation)", _model);
                var responseBody = await response.Content.ReadAsStringAsync();
                return ExtractTextFromGeminiResponse(responseBody);
            }

            var statusCode = (int)response.StatusCode;

            if (!IsTransientStatusCode(statusCode))
            {
                _logger.LogError(
                    "Gemini API permanent error: StatusCode={StatusCode}, Model={Model} (translation)",
                    statusCode, _model);
                throw statusCode switch
                {
                    400 => new ArgumentException("The text could not be translated. Please try different text."),
                    401 or 403 => new InvalidOperationException("Translation service configuration error."),
                    _ => new InvalidOperationException("Translation failed. Please try again.")
                };
            }

            if (attempt < MaxRetries)
            {
                _logger.LogWarning(
                    "Gemini transient error: HTTP {StatusCode}, model={Model}, retrying attempt {Attempt}/{MaxRetries} (translation)",
                    statusCode, _model, attempt + 1, MaxRetries);
                await Task.Delay(RetryDelaysMs[attempt]);
            }
            else
            {
                _logger.LogError(
                    "Gemini transient error: HTTP {StatusCode}, model={Model}, persisted after {MaxRetries} retries (translation)",
                    statusCode, _model, MaxRetries);
                throw new AIProviderUnavailableException(
                    "Translation is temporarily unavailable. Please try again in a moment.");
            }
        }

        throw new AIProviderUnavailableException(
            "Translation is temporarily unavailable. Please try again in a moment.");
    }

    private static string BuildTranslationSystemPrompt(string sourceLanguage, string targetLanguage)
    {
        var sourceInstruction = sourceLanguage == "auto"
            ? "Detect the language of the input text automatically."
            : $"The source language is {sourceLanguage}.";

        return $"""
            You are a professional translator. {sourceInstruction} Translate the text to {targetLanguage}.

            RULES:
            - Return ONLY the translated text. No explanations, no commentary, no preamble.
            - Preserve the original meaning faithfully.
            - Preserve all proper names exactly as they appear.
            - Preserve all numbers exactly as they appear.
            - Preserve all dates exactly as they appear.
            - Preserve all URLs exactly as they appear.
            - Preserve all email addresses exactly as they appear.
            - Preserve formatting, line breaks, and punctuation where practical.
            - Preserve tone and register where practical.
            - Do NOT answer questions contained in the text — translate them.
            - Do NOT summarize the text.
            - Do NOT add explanations or notes.
            - Do NOT rewrite or paraphrase beyond what translation requires.
            - Do NOT invent or add information not present in the original.
            - Do NOT change numerical values, currency amounts, or legal references.
            """;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text for embedding cannot be empty.");

        var requestBody = new
        {
            model = $"models/{_embeddingModel}",
            content = new { parts = new[] { new { text } } },
            output_dimensionality = EmbeddingDimensions
        };

        var jsonPayload = JsonSerializer.Serialize(requestBody, JsonOptions);

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var request = BuildGeminiEmbeddingRequest(jsonPayload);
                response = await _httpClient.SendAsync(request);
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Gemini embedding request timed out");
                throw new InvalidOperationException("Embedding generation timed out. Please try again.");
            }
            catch (HttpRequestException)
            {
                _logger.LogError("Network error communicating with Gemini embedding API");
                throw new InvalidOperationException("Unable to reach the embedding service. Please try again.");
            }

            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return ExtractEmbeddingFromResponse(responseBody);

            var statusCode = (int)response.StatusCode;
            var isTransient = statusCode == 429 || statusCode == 503 || statusCode >= 500;

            if (isTransient && attempt < MaxRetries)
            {
                _logger.LogWarning(
                    "Gemini transient error: HTTP {StatusCode}, model={Model}, retrying attempt {Attempt}/{MaxRetries} (embedding)",
                    statusCode, _embeddingModel, attempt + 1, MaxRetries);
                await Task.Delay(RetryDelaysMs[attempt]);
                continue;
            }

            _logger.LogError(
                "Gemini embedding API error: StatusCode={StatusCode}, Model={Model}",
                statusCode, _embeddingModel);
            throw statusCode switch
            {
                400 => new ArgumentException("The text could not be processed for embedding."),
                401 or 403 => new InvalidOperationException("Embedding service configuration error."),
                429 => new InvalidOperationException("Embedding service is temporarily unavailable due to rate limiting. Please try again shortly."),
                >= 500 => new InvalidOperationException("Embedding service is temporarily unavailable. Please try again."),
                _ => new InvalidOperationException("Embedding generation failed. Please try again.")
            };
        }

        throw new InvalidOperationException("Embedding generation failed after retries. Please try again.");
    }

    private static float[] ExtractEmbeddingFromResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (!root.TryGetProperty("embedding", out var embedding))
            throw new InvalidOperationException("Embedding response missing 'embedding' field.");

        if (!embedding.TryGetProperty("values", out var values))
            throw new InvalidOperationException("Embedding response missing 'values' array.");

        var result = new float[values.GetArrayLength()];
        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            result[index++] = value.GetSingle();
        }

        if (result.Length != EmbeddingDimensions)
            throw new InvalidOperationException($"Expected {EmbeddingDimensions} dimensions but received {result.Length}.");

        return result;
    }

    private static string ExtractTextFromGeminiResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            throw new InvalidOperationException("Document analysis returned no results. Please try again.");

        var firstCandidate = candidates[0];
        if (!firstCandidate.TryGetProperty("content", out var content))
            throw new InvalidOperationException("Document analysis returned an unexpected response format.");

        if (!content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
            throw new InvalidOperationException("Document analysis returned an empty response.");

        var firstPart = parts[0];
        if (!firstPart.TryGetProperty("text", out var text))
            throw new InvalidOperationException("Document analysis returned an unexpected response format.");

        return text.GetString()
            ?? throw new InvalidOperationException("Document analysis returned a null response.");
    }

}
