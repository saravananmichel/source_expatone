using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface IDocumentAnalysisService
{
    Task<DocumentAnalysisDto> AnalyzeDocumentAsync(Guid userId, Guid documentId, bool forceReanalyze = false);
    Task<DocumentAnalysisDto?> GetDocumentAnalysisAsync(Guid userId, Guid documentId);
    Task<DocumentAnswerDto> AskDocumentAsync(Guid userId, Guid documentId, string question, CancellationToken cancellationToken = default);
}
