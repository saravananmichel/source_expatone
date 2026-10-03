using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpatOne.Infrastructure.Services;

public class DocumentShareService : IDocumentShareService
{
    private readonly ExpatOneDbContext _dbContext;
    private readonly IDocumentAuditService _auditService;
    private readonly IStorageService _storageService;
    private readonly ILogger<DocumentShareService> _logger;

    public DocumentShareService(
        ExpatOneDbContext dbContext,
        IDocumentAuditService auditService,
        IStorageService storageService,
        ILogger<DocumentShareService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<DocumentShareDto> CreateShareAsync(Guid ownerUserId, Guid documentId, CreateShareDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SharedWithEmail))
            throw new ArgumentException("Email is required.");

        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == ownerUserId && d.Status != DocumentStatus.PendingUpload)
            ?? throw new KeyNotFoundException("Document not found.");

        var targetUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == dto.SharedWithEmail.Trim().ToLower() && u.IsActive)
            ?? throw new KeyNotFoundException("User not found. They must have an ExpatOne account.");

        if (targetUser.Id == ownerUserId)
            throw new ArgumentException("Cannot share a document with yourself.");

        var existingShare = await _dbContext.DocumentShares
            .FirstOrDefaultAsync(s => s.DocumentId == documentId && s.SharedWithUserId == targetUser.Id && s.RevokedAt == null);

        if (existingShare != null)
            throw new ArgumentException("Document is already shared with this user.");

        var share = new DocumentShare
        {
            DocumentId = documentId,
            OwnerUserId = ownerUserId,
            SharedWithUserId = targetUser.Id,
            Permission = "Read",
        };

        _dbContext.DocumentShares.Add(share);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(documentId, ownerUserId, "share_created", targetShareId: share.Id,
            metadata: $"shared_with_user_id:{targetUser.Id}");

        _logger.LogInformation("Document {DocumentId} shared", documentId);

        return new DocumentShareDto
        {
            Id = share.Id,
            DocumentId = share.DocumentId,
            SharedWithUserId = share.SharedWithUserId,
            SharedWithEmail = targetUser.Email,
            Permission = share.Permission,
            CreatedAt = share.CreatedAt,
        };
    }

    public async Task<List<DocumentShareDto>> GetSharesForDocumentAsync(Guid ownerUserId, Guid documentId)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == ownerUserId)
            ?? throw new KeyNotFoundException("Document not found.");

        var shares = await _dbContext.DocumentShares
            .Include(s => s.SharedWithUser)
            .Where(s => s.DocumentId == documentId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return shares.Select(s => new DocumentShareDto
        {
            Id = s.Id,
            DocumentId = s.DocumentId,
            DocumentName = document.DocumentName,
            SharedWithUserId = s.SharedWithUserId,
            SharedWithEmail = s.SharedWithUser.Email,
            Permission = s.Permission,
            CreatedAt = s.CreatedAt,
            RevokedAt = s.RevokedAt,
        }).ToList();
    }

    public async Task RevokeShareAsync(Guid ownerUserId, Guid documentId, Guid shareId)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == ownerUserId)
            ?? throw new KeyNotFoundException("Document not found.");

        var share = await _dbContext.DocumentShares
            .FirstOrDefaultAsync(s => s.Id == shareId && s.DocumentId == documentId)
            ?? throw new KeyNotFoundException("Share not found.");

        if (share.RevokedAt != null)
            throw new InvalidOperationException("Share is already revoked.");

        share.RevokedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(documentId, ownerUserId, "share_revoked", targetShareId: shareId);

        _logger.LogInformation("Share {ShareId} revoked for document {DocumentId}", shareId, documentId);
    }

    public async Task<List<SharedDocumentDto>> GetDocumentsSharedWithMeAsync(Guid userId)
    {
        var shares = await _dbContext.DocumentShares
            .Include(s => s.Document)
                .ThenInclude(d => d.DocumentType)
            .Include(s => s.OwnerUser)
            .Where(s => s.SharedWithUserId == userId && s.RevokedAt == null
                && s.Document.Status != DocumentStatus.PendingUpload)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return shares.Select(s => new SharedDocumentDto
        {
            ShareId = s.Id,
            DocumentId = s.DocumentId,
            DocumentName = s.Document.DocumentName,
            DocumentType = s.Document.DocumentType.Name,
            OwnerEmail = s.OwnerUser.Email,
            Permission = s.Permission,
            ExpiryDate = s.Document.ExpiryDate,
            SharedAt = s.CreatedAt,
        }).ToList();
    }

    public async Task<AccessUrlResponseDto> GetSharedDocumentAccessUrlAsync(Guid userId, Guid documentId)
    {
        var share = await _dbContext.DocumentShares
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.DocumentId == documentId && s.SharedWithUserId == userId
                && s.RevokedAt == null && s.Document.Status != DocumentStatus.PendingUpload)
            ?? throw new KeyNotFoundException("Shared document not found or access revoked.");

        var expirySeconds = 300;
        var url = await _storageService.GeneratePresignedUrlAsync(
            share.Document.S3ObjectKey, TimeSpan.FromSeconds(expirySeconds));

        await _auditService.LogAsync(documentId, userId, "shared_document_accessed");

        return new AccessUrlResponseDto
        {
            Url = url,
            ExpiresInSeconds = expirySeconds,
        };
    }
}
