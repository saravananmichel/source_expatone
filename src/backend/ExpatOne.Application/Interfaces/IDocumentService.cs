using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface IDocumentService
{
    Task<UploadUrlResponseDto> RequestUploadAsync(Guid userId, RequestUploadDto dto);
    Task<DocumentDto> CompleteUploadAsync(Guid userId, Guid documentId);
    Task<List<DocumentDto>> GetUserDocumentsAsync(Guid userId);
    Task<DocumentDto?> GetDocumentAsync(Guid userId, Guid documentId);
    Task<AccessUrlResponseDto> GetAccessUrlAsync(Guid userId, Guid documentId);
    Task DeleteDocumentAsync(Guid userId, Guid documentId);
    Task<List<DocumentTypeDto>> GetDocumentTypesAsync();
}
