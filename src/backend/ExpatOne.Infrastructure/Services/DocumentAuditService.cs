using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Infrastructure.Services;

public class DocumentAuditService : IDocumentAuditService
{
    private readonly ExpatOneDbContext _dbContext;

    public DocumentAuditService(ExpatOneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(Guid documentId, Guid userId, string action, Guid? targetVersionId = null, Guid? targetShareId = null, string? metadata = null)
    {
        Stage(documentId, userId, action, targetVersionId, targetShareId, metadata);
        await _dbContext.SaveChangesAsync();
    }

    public void Stage(Guid documentId, Guid userId, string action, Guid? targetVersionId = null, Guid? targetShareId = null, string? metadata = null)
    {
        var log = new DocumentAuditLog
        {
            DocumentId = documentId,
            UserId = userId,
            Action = action,
            TargetVersionId = targetVersionId,
            TargetShareId = targetShareId,
            Metadata = metadata,
        };

        _dbContext.DocumentAuditLogs.Add(log);
    }

    public async Task<List<DocumentAuditLogDto>> GetAuditLogsAsync(Guid ownerUserId, Guid documentId)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == ownerUserId)
            ?? throw new KeyNotFoundException("Document not found.");

        var logs = await _dbContext.DocumentAuditLogs
            .Where(a => a.DocumentId == documentId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return logs.Select(a => new DocumentAuditLogDto
        {
            Id = a.Id,
            DocumentId = a.DocumentId,
            Action = a.Action,
            TargetVersionId = a.TargetVersionId,
            TargetShareId = a.TargetShareId,
            Metadata = a.Metadata,
            CreatedAt = a.CreatedAt,
        }).ToList();
    }
}
