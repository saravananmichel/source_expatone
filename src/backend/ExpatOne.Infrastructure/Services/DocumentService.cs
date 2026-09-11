using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpatOne.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly ExpatOneDbContext _dbContext;
    private readonly IStorageService _storageService;
    private readonly ILogger<DocumentService> _logger;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public DocumentService(ExpatOneDbContext dbContext, IStorageService storageService, ILogger<DocumentService> logger)
    {
        _dbContext = dbContext;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<UploadUrlResponseDto> RequestUploadAsync(Guid userId, RequestUploadDto dto)
    {
        ValidateUploadRequest(dto);

        var docType = await _dbContext.DocumentTypes.FindAsync(dto.DocumentTypeId)
            ?? throw new ArgumentException("Invalid document type.");

        var documentId = Guid.NewGuid();
        var safeFileName = SanitizeFileName(dto.FileName);
        var objectKey = $"users/{userId}/documents/{documentId}/{safeFileName}";

        var document = new Document
        {
            Id = documentId,
            UserId = userId,
            DocumentTypeId = dto.DocumentTypeId,
            DocumentName = dto.DocumentName.Trim(),
            OriginalFileName = dto.FileName,
            S3ObjectKey = objectKey,
            ContentType = dto.ContentType,
            FileSizeBytes = dto.FileSizeBytes,
            ExpiryDate = dto.ExpiryDate,
            Status = DocumentStatus.PendingUpload,
        };

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync();

        var uploadUrl = _storageService.GeneratePresignedUploadUrl(
            objectKey, dto.ContentType, TimeSpan.FromMinutes(15));

        _logger.LogInformation("Upload requested for document {DocumentId} by user {UserId}", documentId, userId);

        return new UploadUrlResponseDto
        {
            DocumentId = documentId,
            UploadUrl = uploadUrl,
            ObjectKey = objectKey,
        };
    }

    public async Task<DocumentDto> CompleteUploadAsync(Guid userId, Guid documentId)
    {
        var document = await _dbContext.Documents
            .Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId)
            ?? throw new KeyNotFoundException("Document not found.");

        if (document.Status != DocumentStatus.PendingUpload)
            throw new InvalidOperationException("Document is not pending upload.");

        document.Status = DocumentStatus.Active;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Upload completed for document {DocumentId}", documentId);

        return MapToDto(document);
    }

    public async Task<List<DocumentDto>> GetUserDocumentsAsync(Guid userId)
    {
        var documents = await _dbContext.Documents
            .Include(d => d.DocumentType)
            .Where(d => d.UserId == userId && d.Status != DocumentStatus.PendingUpload)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return documents.Select(MapToDto).ToList();
    }

    public async Task<DocumentDto?> GetDocumentAsync(Guid userId, Guid documentId)
    {
        var document = await _dbContext.Documents
            .Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId
                && d.Status != DocumentStatus.PendingUpload);

        return document is null ? null : MapToDto(document);
    }

    public async Task<AccessUrlResponseDto> GetAccessUrlAsync(Guid userId, Guid documentId)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId
                && d.Status != DocumentStatus.PendingUpload)
            ?? throw new KeyNotFoundException("Document not found.");

        var expirySeconds = 300; // 5 minutes
        var url = await _storageService.GeneratePresignedUrlAsync(
            document.S3ObjectKey, TimeSpan.FromSeconds(expirySeconds));

        return new AccessUrlResponseDto
        {
            Url = url,
            ExpiresInSeconds = expirySeconds,
        };
    }

    public async Task DeleteDocumentAsync(Guid userId, Guid documentId)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId)
            ?? throw new KeyNotFoundException("Document not found.");

        // Delete from S3 first — if this fails, metadata is still intact for retry
        try
        {
            await _storageService.DeleteFileAsync(document.S3ObjectKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete S3 object {ObjectKey} for document {DocumentId}", document.S3ObjectKey, documentId);
            throw;
        }

        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted document {DocumentId} for user {UserId}", documentId, userId);
    }

    public async Task<List<DocumentTypeDto>> GetDocumentTypesAsync()
    {
        var types = await _dbContext.DocumentTypes
            .OrderBy(dt => dt.Name)
            .ToListAsync();

        return types.Select(dt => new DocumentTypeDto
        {
            Id = dt.Id,
            Name = dt.Name,
            Description = dt.Description,
            Category = dt.Category,
            HasExpiry = dt.HasExpiry,
        }).ToList();
    }

    private static void ValidateUploadRequest(RequestUploadDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DocumentName))
            throw new ArgumentException("Document name is required.");

        if (string.IsNullOrWhiteSpace(dto.FileName))
            throw new ArgumentException("File name is required.");

        if (string.IsNullOrWhiteSpace(dto.ContentType))
            throw new ArgumentException("Content type is required.");

        if (!AllowedContentTypes.Contains(dto.ContentType))
            throw new ArgumentException($"File type '{dto.ContentType}' is not supported. Allowed: PDF, JPEG, PNG.");

        if (dto.FileSizeBytes <= 0)
            throw new ArgumentException("File size must be greater than zero.");

        if (dto.FileSizeBytes > MaxFileSizeBytes)
            throw new ArgumentException($"File size exceeds the maximum of {MaxFileSizeBytes / (1024 * 1024)} MB.");
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
            name = "document";

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "document";

        if (sanitized.Length > 200)
            sanitized = sanitized[..200];

        return sanitized;
    }

    private static DocumentDto MapToDto(Document doc) => new()
    {
        Id = doc.Id,
        Name = doc.DocumentName,
        DocumentType = doc.DocumentType.Name,
        DocumentTypeId = doc.DocumentTypeId,
        OriginalFileName = doc.OriginalFileName,
        ContentType = doc.ContentType,
        FileSizeBytes = doc.FileSizeBytes,
        Status = doc.Status.ToString(),
        ExpiryDate = doc.ExpiryDate,
        CreatedAt = doc.CreatedAt,
        UpdatedAt = doc.UpdatedAt,
    };
}
