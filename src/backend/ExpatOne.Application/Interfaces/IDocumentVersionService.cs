using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface IDocumentVersionService
{
    Task<VersionUploadUrlResponseDto> RequestVersionUploadAsync(Guid userId, Guid documentId, UploadVersionDto dto);
    Task<DocumentVersionDto> CompleteVersionUploadAsync(Guid userId, Guid documentId, Guid versionId);
    Task<List<DocumentVersionDto>> GetVersionsAsync(Guid userId, Guid documentId);
    Task<AccessUrlResponseDto> GetVersionAccessUrlAsync(Guid userId, Guid documentId, Guid versionId);
    Task DeleteVersionAsync(Guid userId, Guid documentId, Guid versionId);
}
