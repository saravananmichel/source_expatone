using System.Text.Json;
using ExpatOne.Application.Common;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Infrastructure.Services;

// Document routes depend only on the private local provider and persisted job results.
public class DocumentAnalysisService(ExpatOneDbContext db, IDocumentAnalysisJobs jobs,
    IDocumentIntelligenceService intelligence) : IDocumentAnalysisService
{
    public async Task<DocumentAnalysisDto> AnalyzeDocumentAsync(Guid userId, Guid documentId, bool forceReanalyze = false)
    {
        if (!forceReanalyze && await GetDocumentAnalysisAsync(userId, documentId) is { } existing) return existing;
        await jobs.EnqueueAsync(userId, documentId, forceReanalyze, default);
        throw new InvalidOperationException("Local document analysis is processing. Please retry when it finishes.");
    }

    public async Task<DocumentAnalysisDto?> GetDocumentAnalysisAsync(Guid userId, Guid documentId)
    {
        var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId)
            ?? throw new KeyNotFoundException("Document not found.");
        var version = await db.DocumentVersions.AsNoTracking().FirstOrDefaultAsync(v => v.DocumentId == documentId && v.IsCurrent);
        var runs = await db.DocumentAnalysisRuns.AsNoTracking().Where(x => x.DocumentId == documentId && x.UserId == userId &&
            x.ObjectKey == document.S3ObjectKey && (x.Status == "COMPLETED" || x.Status == "REQUIRES_REVIEW"))
            .OrderByDescending(x => x.CreatedAt).ToListAsync();
        foreach (var run in runs)
        {
            if (version != null && (run.DocumentVersionId != version.Id ||
                (version.Sha256Hash != null && version.Sha256Hash != run.ContentHash))) continue;
            try
            {
                var result = JsonSerializer.Deserialize<DocumentAnalysisDto>(run.ResultJson ?? "null", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (result?.Provider == "Local" && (result.ModelVersion == "qwen3:4b" || result.ModelVersion?.StartsWith("qwen3:4b@", StringComparison.Ordinal) == true) &&
                    result.SemanticDocument is { ValueKind: JsonValueKind.Object } semantic && semantic.TryGetProperty("pages", out var pages) &&
                    pages.ValueKind == JsonValueKind.Array && pages.GetArrayLength() > 0) return result;
            }
            catch (JsonException) { /* Invalid/stale results require local reanalysis; never an external provider. */ }
        }
        return null;
    }

    public async Task<DocumentAnswerDto> AskDocumentAsync(Guid userId, Guid documentId, string question, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length > 2000)
            throw new ArgumentException("Question must contain between 1 and 2000 characters.");
        var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId)
            ?? throw new KeyNotFoundException("Document not found.");
        if (document.Status == DocumentStatus.PendingUpload) throw new InvalidOperationException("Complete the upload first.");
        var analysis = await GetDocumentAnalysisAsync(userId, documentId);
        if (analysis == null)
        {
            await jobs.EnqueueAsync(userId, documentId, true, cancellationToken);
            throw new InvalidOperationException("Local document analysis is processing. Open document analysis and retry when it finishes.");
        }
        var context = DocumentContextSelector.Select(analysis, question);
        if (context.Count == 0) return new DocumentAnswerDto { DocumentId = documentId, DocumentName = document.DocumentName,
            Answer = "The document does not provide enough evidence to answer this question.", Grounded = false };
        try
        {
            var answer = await intelligence.AskAsync(new DocumentAskRequestDto { Question = question.Trim(),
                DocumentCategory = analysis.DocumentCategory, Context = context }, cancellationToken);
            answer.DocumentId = documentId;
            answer.DocumentName = document.DocumentName;
            return answer;
        }
        catch (HttpRequestException) { throw new AIProviderUnavailableException("Local document Q&A is unavailable. Please retry."); }
        catch (TaskCanceledException) { throw new AIProviderUnavailableException("Local document Q&A timed out. Please retry."); }
    }
}
