using Amazon.S3;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpatOne.Infrastructure.Services;

public class DocumentVersionService : IDocumentVersionService
{
    private readonly ExpatOneDbContext _dbContext;
    private readonly IStorageService _storageService;
    private readonly IDocumentAuditService _auditService;
    private readonly ILogger<DocumentVersionService> _logger;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public DocumentVersionService(
        ExpatOneDbContext dbContext,
        IStorageService storageService,
        IDocumentAuditService auditService,
        ILogger<DocumentVersionService> logger)
    {
        _dbContext = dbContext;
        _storageService = storageService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<VersionUploadUrlResponseDto> RequestVersionUploadAsync(Guid userId, Guid documentId, UploadVersionDto dto)
    {
        var document = await _dbContext.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId && d.Status != DocumentStatus.PendingUpload)
            ?? throw new KeyNotFoundException("Document not found.");

        ValidateVersionUpload(dto);

        var nextVersion = document.Versions.Count > 0
            ? document.Versions.Max(v => v.VersionNumber) + 1
            : 1;

        var safeFileName = SanitizeFileName(dto.FileName);
        var objectKey = $"users/{userId}/documents/{documentId}/v{nextVersion}/{safeFileName}";

        var version = new DocumentVersion
        {
            DocumentId = documentId,
            VersionNumber = nextVersion,
            S3ObjectKey = objectKey,
            OriginalFileName = dto.FileName,
            ContentType = dto.ContentType,
            FileSizeBytes = dto.FileSizeBytes,
            IsCurrent = false,
            UploadedByUserId = userId,
        };

        _dbContext.DocumentVersions.Add(version);
        await _dbContext.SaveChangesAsync();

        var uploadUrl = _storageService.GeneratePresignedUploadUrl(
            objectKey, dto.ContentType, TimeSpan.FromMinutes(15));

        _logger.LogInformation("Version upload requested for document {DocumentId} version {VersionNumber}", documentId, nextVersion);

        return new VersionUploadUrlResponseDto
        {
            VersionId = version.Id,
            UploadUrl = uploadUrl,
        };
    }

    public async Task<DocumentVersionDto> CompleteVersionUploadAsync(Guid userId, Guid documentId, Guid versionId)
    {
        var document = await _dbContext.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId)
            ?? throw new KeyNotFoundException("Document not found.");

        var version = document.Versions.FirstOrDefault(v => v.Id == versionId)
            ?? throw new KeyNotFoundException("Version not found.");

        if (version.IsCurrent)
            throw new InvalidOperationException("Version upload already completed.");

        foreach (var v in document.Versions.Where(v => v.IsCurrent))
            v.IsCurrent = false;

        version.IsCurrent = true;

        document.ExtractedMetadata = null;
        document.S3ObjectKey = version.S3ObjectKey;
        document.OriginalFileName = version.OriginalFileName;
        document.ContentType = version.ContentType;
        document.FileSizeBytes = version.FileSizeBytes;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(documentId, userId, "version_uploaded", targetVersionId: versionId);

        _logger.LogInformation("Version {VersionId} completed for document {DocumentId}", versionId, documentId);

        return MapToDto(version);
    }

    public async Task<List<DocumentVersionDto>> GetVersionsAsync(Guid userId, Guid documentId)
    {
        var hasAccess = await _dbContext.Documents
            .AnyAsync(d => d.Id == documentId && d.UserId == userId)
            || await _dbContext.DocumentShares
                .AnyAsync(s => s.DocumentId == documentId && s.SharedWithUserId == userId && s.RevokedAt == null);

        if (!hasAccess)
            throw new KeyNotFoundException("Document not found.");

        var versions = await _dbContext.DocumentVersions
            .Where(v => v.DocumentId == documentId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync();

        return versions.Select(MapToDto).ToList();
    }

    public async Task<AccessUrlResponseDto> GetVersionAccessUrlAsync(Guid userId, Guid documentId, Guid versionId)
    {
        var hasAccess = await _dbContext.Documents
            .AnyAsync(d => d.Id == documentId && d.UserId == userId)
            || await _dbContext.DocumentShares
                .AnyAsync(s => s.DocumentId == documentId && s.SharedWithUserId == userId && s.RevokedAt == null);

        if (!hasAccess)
            throw new KeyNotFoundException("Document not found.");

        var version = await _dbContext.DocumentVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.DocumentId == documentId)
            ?? throw new KeyNotFoundException("Version not found.");

        var expirySeconds = 300;
        var url = await _storageService.GeneratePresignedUrlAsync(
            version.S3ObjectKey, TimeSpan.FromSeconds(expirySeconds));

        await _auditService.LogAsync(documentId, userId, "version_accessed", targetVersionId: versionId);

        return new AccessUrlResponseDto
        {
            Url = url,
            ExpiresInSeconds = expirySeconds,
        };
    }

    public async Task DeleteVersionAsync(Guid userId, Guid documentId, Guid versionId)
    {
        var document = await _dbContext.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId)
            ?? throw new KeyNotFoundException("Document not found.");

        var version = document.Versions.FirstOrDefault(v => v.Id == versionId)
            ?? throw new KeyNotFoundException("Version not found.");

        if (version.IsCurrent && document.Versions.Count(v => v.Id != versionId) > 0)
            throw new InvalidOperationException("Cannot delete the current version while other versions exist. Upload a new version first.");

        try
        {
            await _storageService.DeleteFileAsync(version.S3ObjectKey);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Storage object already absent for version {VersionId}", versionId);
        }

        _dbContext.DocumentVersions.Remove(version);

        if (version.IsCurrent)
        {
            var previous = document.Versions
                .Where(v => v.Id != versionId)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();

            if (previous != null)
            {
                previous.IsCurrent = true;
                document.S3ObjectKey = previous.S3ObjectKey;
                document.OriginalFileName = previous.OriginalFileName;
                document.ContentType = previous.ContentType;
                document.FileSizeBytes = previous.FileSizeBytes;
            }
        }

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(documentId, userId, "version_deleted", targetVersionId: versionId);

        _logger.LogInformation("Deleted version {VersionId} from document {DocumentId}", versionId, documentId);
    }

    private static void ValidateVersionUpload(UploadVersionDto dto)
    {
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

    private static DocumentVersionDto MapToDto(DocumentVersion v) => new()
    {
        Id = v.Id,
        DocumentId = v.DocumentId,
        VersionNumber = v.VersionNumber,
        OriginalFileName = v.OriginalFileName,
        ContentType = v.ContentType,
        FileSizeBytes = v.FileSizeBytes,
        IsCurrent = v.IsCurrent,
        CreatedAt = v.CreatedAt,
    };
}
