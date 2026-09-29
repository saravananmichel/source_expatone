using ExpatOne.Application.DTOs;

namespace ExpatOne.Application.Interfaces;

public interface IKnowledgeIngestionService
{
    Task<GovernmentSourceDto> RegisterSourceAsync(RegisterSourceDto dto);
    Task<List<GovernmentSourceDto>> GetSourcesAsync(string countryCode = "MY");
    Task<GovernmentSourceDto?> GetSourceAsync(Guid sourceId);
    Task DeactivateSourceAsync(Guid sourceId);
    Task<IngestionResultDto> IngestSourceAsync(Guid sourceId, IngestContentDto dto);
    Task<EmbeddingGenerationResultDto> GenerateEmbeddingsAsync(int batchSize = 50);
}
