using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface IDocumentShareService
{
    Task<DocumentShareDto> CreateShareAsync(Guid ownerUserId, Guid documentId, CreateShareDto dto);
    Task<List<DocumentShareDto>> GetSharesForDocumentAsync(Guid ownerUserId, Guid documentId);
    Task RevokeShareAsync(Guid ownerUserId, Guid documentId, Guid shareId);
    Task<List<SharedDocumentDto>> GetDocumentsSharedWithMeAsync(Guid userId);
    Task<AccessUrlResponseDto> GetSharedDocumentAccessUrlAsync(Guid userId, Guid documentId);
}
