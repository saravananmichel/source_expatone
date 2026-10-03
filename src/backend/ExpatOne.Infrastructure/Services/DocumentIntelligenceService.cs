using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using Microsoft.Extensions.Configuration;
namespace ExpatOne.Infrastructure.Services;

public class DocumentIntelligenceService(HttpClient client, IConfiguration config, IAIService? gemini = null)
    : IDocumentIntelligenceService
{
    public async Task<DocumentAnalysisDto> AnalyzeAsync(Stream file, string contentType, CancellationToken ct)
        => await AnalyzeWithConfigurationAsync(file, contentType, ct, null, null);
    public async Task<DocumentAnalysisDto> AnalyzeWithConfigurationAsync(Stream file, string contentType, CancellationToken ct, string? queuedProvider, bool? queuedFallback)
    {
        var provider = queuedProvider ?? config["DocumentIntelligence:Provider"] ?? "Local";
        if (provider is not ("Local" or "Gemini" or "Hybrid"))
            throw new InvalidOperationException("Invalid document intelligence provider.");
        if (provider != "Gemini")
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "analyze");
                request.Headers.Add("X-Service-Key", config["DocumentIntelligence:ServiceKey"]);
                using var form = new MultipartFormDataContent();
                // The caller owns the file stream. StreamContent is disposed with the request.
                var content = new StreamContent(file);
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                form.Add(content, "file", "document");
                request.Content = form;
                using var response = await client.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<DocumentAnalysisDto>(cancellationToken: ct)
                    ?? throw new InvalidOperationException("Empty analysis.");
                if (result.Evidence is null || result.Statements is null || result.Evidence.Count == 0 || result.Statements.Count == 0 ||
                    string.IsNullOrWhiteSpace(result.Summary) || string.IsNullOrWhiteSpace(result.ModelVersion))
                    throw new InvalidOperationException("Ungrounded analysis.");
                if (result.AnalyzerVersion == "document-engine-v2")
                {
                    var evidenceIds = result.Evidence.Select(e => e.Id).ToHashSet();
                    var statuses = new HashSet<string> { "SUPPORTED", "PARTIALLY_SUPPORTED", "UNSUPPORTED", "UNCERTAIN" };
                    if (evidenceIds.Count != result.Evidence.Count || result.Evidence.Any(e => e.Page < 1 || string.IsNullOrWhiteSpace(e.SourceText)) ||
                        result.Statements.Select(s => s.Id).Distinct().Count() != result.Statements.Count ||
                        result.Statements.Any(s => !statuses.Contains(s.SupportStatus) || s.EvidenceIds.Count == 0 || s.EvidenceIds.Any(id => !evidenceIds.Contains(id))))
                        throw new InvalidOperationException("Invalid support references.");
                    result.RequiresReview = true;
                    if (result.Statements.Any(s => s.SupportStatus != "SUPPORTED")) result.Confidence = "low";
                }
                return result;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && gemini != null &&
                (queuedFallback ?? string.Equals(config["DocumentIntelligence:EnableFallback"], "true", StringComparison.OrdinalIgnoreCase)) &&
                ex is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException)
            {
                // A separate downloaded stream is needed for fallback because HTTP owns its content.
                throw new LocalAnalysisFallbackException();
            }
        }
        return await AnalyzeGeminiAsync(file, contentType, ct);
    }
    public async Task<DocumentAnalysisDto> AnalyzeGeminiAsync(Stream file, string type, CancellationToken ct)
    {
        if (gemini is null) throw new InvalidOperationException("Fallback unavailable.");
        var response = await gemini.AnalyzeDocumentAsync(file, type).WaitAsync(ct);
        var result = JsonSerializer.Deserialize<DocumentAnalysisDto>(response.StructuredJson ?? response.Content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid model output.");
        if (string.IsNullOrWhiteSpace(result.Summary)) throw new InvalidOperationException("Invalid model output.");
        result.Provider = "Gemini";
        result.ModelVersion = config["Gemini:Model"] ?? "configured-gemini";
        // Legacy Gemini output has no verifiable page evidence; never represent it as verified.
        result.RequiresReview = true;
        result.Confidence = "low";
        return result;
    }
}
public class LocalAnalysisFallbackException : Exception;
