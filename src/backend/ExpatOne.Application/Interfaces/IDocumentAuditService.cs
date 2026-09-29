using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface IDocumentAuditService
{
    Task LogAsync(Guid documentId, Guid userId, string action, Guid? targetVersionId = null, Guid? targetShareId = null, string? metadata = null);
    // Stages an audit entry in the DbContext without calling SaveChangesAsync.
    // Caller is responsible for the save. Use when the audit and the primary operation must be atomic.
    void Stage(Guid documentId, Guid userId, string action, Guid? targetVersionId = null, Guid? targetShareId = null, string? metadata = null);
    Task<List<DocumentAuditLogDto>> GetAuditLogsAsync(Guid ownerUserId, Guid documentId);
}
