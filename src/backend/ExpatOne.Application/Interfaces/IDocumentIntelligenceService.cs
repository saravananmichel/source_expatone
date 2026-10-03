using ExpatOne.Application.DTOs;
namespace ExpatOne.Application.Interfaces;
public interface IDocumentIntelligenceService
{
    Task<DocumentAnswerDto> AskAsync(DocumentAskRequestDto request, CancellationToken cancellationToken);
    Task<DocumentAnalysisDto> AnalyzeAsync(Stream file, string contentType, CancellationToken cancellationToken);
}
public interface IDocumentAnalysisJobs
{
    Task<AnalysisJobDto> EnqueueAsync(Guid userId, Guid documentId, bool force, CancellationToken cancellationToken);
    Task<AnalysisJobDto?> GetAsync(Guid userId, Guid documentId, Guid? analysisId, CancellationToken cancellationToken);
    Task<List<AnalysisJobDto>> HistoryAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);
}
