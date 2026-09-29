using System.Text.Json;
using System.Text.Json.Serialization;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpatOne.Infrastructure.Services;

public class DocumentAnalysisService : IDocumentAnalysisService
{
    private readonly ExpatOneDbContext _dbContext;
    private readonly IStorageService _storageService;
    private readonly IAIService _aiService;
    private readonly ILogger<DocumentAnalysisService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly HashSet<string> KnownDocumentCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Passport", "Visa", "Employment Pass", "Immigration Document",
        "Employment Contract", "Insurance", "Rental Agreement", "Tax Document",
        "Government Letter", "Government Correspondence", "General Correspondence",
        "Driving Licence", "Medical Card", "Work Permit", "Other"
    };

    private const int MaxQuestionLength = 2000;

    public DocumentAnalysisService(
        ExpatOneDbContext dbContext,
        IStorageService storageService,
        IAIService aiService,
        ILogger<DocumentAnalysisService> logger)
    {
        _dbContext = dbContext;
        _storageService = storageService;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<DocumentAnalysisDto> AnalyzeDocumentAsync(Guid userId, Guid documentId, bool forceReanalyze = false)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId)
            ?? throw new KeyNotFoundException("Document not found.");

        if (!forceReanalyze && !string.IsNullOrEmpty(document.ExtractedMetadata))
        {
            var cached = DeserializeAnalysis(document.ExtractedMetadata, documentId);
            if (cached is not null)
                return cached;
        }

        if (string.IsNullOrEmpty(document.S3ObjectKey))
            throw new InvalidOperationException("Document has no associated file.");

        if (string.IsNullOrEmpty(document.ContentType))
            throw new InvalidOperationException("Document has no content type.");

        Stream documentStream;
        try
        {
            documentStream = await _storageService.DownloadFileAsync(document.S3ObjectKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download document {DocumentId} from storage for analysis", documentId);
            throw new InvalidOperationException("Unable to retrieve the document for analysis. Please try again.");
        }

        AIResponse aiResponse;
        try
        {
            await using (documentStream)
            {
                aiResponse = await _aiService.AnalyzeDocumentAsync(documentStream, document.ContentType);
            }
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI analysis failed for document {DocumentId}", documentId);
            throw new InvalidOperationException("Document analysis failed. Please try again.");
        }

        var analysisDto = ParseAnalysisResponse(aiResponse.StructuredJson ?? aiResponse.Content, documentId);

        document.ExtractedMetadata = JsonSerializer.Serialize(analysisDto, JsonOptions);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Analysis completed for document {DocumentId} by user {UserId}", documentId, userId);

        return analysisDto;
    }

    public async Task<DocumentAnalysisDto?> GetDocumentAnalysisAsync(Guid userId, Guid documentId)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);

        if (document is null)
            return null;

        if (string.IsNullOrEmpty(document.ExtractedMetadata))
            return null;

        return DeserializeAnalysis(document.ExtractedMetadata, documentId);
    }

    public async Task<DocumentAnswerDto> AskDocumentAsync(Guid userId, Guid documentId, string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question cannot be empty.");

        if (question.Length > MaxQuestionLength)
            throw new ArgumentException($"Question cannot exceed {MaxQuestionLength} characters.");

        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId)
            ?? throw new KeyNotFoundException("Document not found.");

        if (string.IsNullOrEmpty(document.S3ObjectKey))
            throw new InvalidOperationException("Document has no associated file.");

        if (string.IsNullOrEmpty(document.ContentType))
            throw new InvalidOperationException("Document has no content type.");

        Stream documentStream;
        try
        {
            documentStream = await _storageService.DownloadFileAsync(document.S3ObjectKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download document {DocumentId} from storage for Q&A", documentId);
            throw new InvalidOperationException("Unable to retrieve the document. Please try again.");
        }

        AIResponse aiResponse;
        try
        {
            await using (documentStream)
            {
                aiResponse = await _aiService.AnalyzeDocumentAsync(documentStream, document.ContentType, question);
            }
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document Q&A failed for document {DocumentId}", documentId);
            throw new InvalidOperationException("Unable to answer your question about this document. Please try again.");
        }

        _logger.LogInformation("Q&A completed for document {DocumentId} by user {UserId}", documentId, userId);

        return new DocumentAnswerDto
        {
            DocumentId = documentId,
            Answer = aiResponse.Content,
            Grounded = true,
            DocumentName = document.DocumentName,
        };
    }

    private DocumentAnalysisDto? DeserializeAnalysis(string json, Guid documentId)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<DocumentAnalysisDto>(json, JsonOptions);
            if (dto is not null)
                dto.DocumentId = documentId;
            return dto;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid cached analysis JSON for document {DocumentId}, will re-analyze", documentId);
            return null;
        }
    }

    private static DocumentAnalysisDto ParseAnalysisResponse(string json, Guid documentId)
    {
        DocumentAnalysisDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<DocumentAnalysisDto>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Document analysis returned an invalid response format.", ex);
        }

        if (dto is null)
            throw new InvalidOperationException("Document analysis returned an empty response.");

        dto.DocumentId = documentId;
        dto.AnalyzedAt = DateTime.UtcNow;
        dto.ImportantDates ??= [];
        dto.Deadlines ??= [];
        dto.RequiredActions ??= [];
        dto.KeyInformation ??= [];
        dto.Warnings ??= [];
        dto.Terminology ??= [];

        dto.DocumentCategory = NormalizeDocumentCategory(dto.DocumentCategory);

        return dto;
    }

    private static string NormalizeDocumentCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "Other";

        if (KnownDocumentCategories.Contains(category))
            return KnownDocumentCategories.First(k => k.Equals(category, StringComparison.OrdinalIgnoreCase));

        var lower = category.ToLowerInvariant();
        if (lower.Contains("passport")) return "Passport";
        if (lower.Contains("visa") || lower.Contains("pass") && lower.Contains("employ")) return "Employment Pass";
        if (lower.Contains("visa")) return "Visa";
        if (lower.Contains("immigration")) return "Immigration Document";
        if (lower.Contains("employment") && lower.Contains("contract")) return "Employment Contract";
        if (lower.Contains("insurance") || lower.Contains("policy")) return "Insurance";
        if (lower.Contains("rental") || lower.Contains("tenancy") || lower.Contains("lease")) return "Rental Agreement";
        if (lower.Contains("tax")) return "Tax Document";
        if (lower.Contains("government") && lower.Contains("letter")) return "Government Letter";
        if (lower.Contains("government")) return "Government Correspondence";
        if (lower.Contains("correspondence") || lower.Contains("letter")) return "General Correspondence";
        if (lower.Contains("driv")) return "Driving Licence";
        if (lower.Contains("medical")) return "Medical Card";
        if (lower.Contains("work permit")) return "Work Permit";

        return "Other";
    }
}
