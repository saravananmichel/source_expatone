using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using Microsoft.Extensions.Configuration;
namespace ExpatOne.Infrastructure.Services;

public class DocumentIntelligenceService(HttpClient client, IConfiguration config)
    : IDocumentIntelligenceService
{
    public async Task<DocumentAnalysisDto> AnalyzeAsync(Stream file, string contentType, CancellationToken ct)
        => await AnalyzeWithConfigurationAsync(file, contentType, ct, null, null);
    public async Task<DocumentAnalysisDto> AnalyzeWithConfigurationAsync(Stream file, string contentType, CancellationToken ct, string? queuedProvider, bool? queuedFallback)
    {
        var provider = queuedProvider ?? config["DocumentIntelligence:Provider"] ?? "Local";
        if (provider != "Local" || (queuedFallback ?? string.Equals(config["DocumentIntelligence:EnableFallback"], "true", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Document AI requires Local provider with fallback disabled.");
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
    public async Task<DocumentAnswerDto> AskAsync(DocumentAskRequestDto payload, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(55));
        ct = timeout.Token;
        using var request = new HttpRequestMessage(HttpMethod.Post, "ask");
        request.Headers.Add("X-Service-Key", config["DocumentIntelligence:ServiceKey"]);
        request.Content = JsonContent.Create(payload);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var answer = await response.Content.ReadFromJsonAsync<DocumentAnswerDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty document answer.");
        // Validate the private service's citations against the supplied source, too.
        if (answer.Grounded && (answer.Evidence.Count == 0 || answer.Evidence.Any(e =>
            !payload.Context.Any(c => c.Id == e.Id && c.Page == e.Page &&
                !string.IsNullOrWhiteSpace(e.SourceText) && c.Text.Contains(e.SourceText, StringComparison.Ordinal)))))
            throw new InvalidOperationException("Invalid document answer evidence.");
        return answer;
    }
}
