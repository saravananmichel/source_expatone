using System.Security.Cryptography;
using System.Text;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpatOne.Infrastructure.Services;

public class KnowledgeIngestionService : IKnowledgeIngestionService
{
    private readonly ExpatOneDbContext _dbContext;
    private readonly IAIService _aiService;
    private readonly ILogger<KnowledgeIngestionService> _logger;

    private const int TargetChunkSize = 2000;
    private const int ChunkOverlap = 200;

    public KnowledgeIngestionService(
        ExpatOneDbContext dbContext,
        IAIService aiService,
        ILogger<KnowledgeIngestionService> logger)
    {
        _dbContext = dbContext;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<GovernmentSourceDto> RegisterSourceAsync(RegisterSourceDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Source name is required.");

        if (!string.IsNullOrEmpty(dto.Url))
        {
            var exists = await _dbContext.GovernmentSources
                .AnyAsync(s => s.Url == dto.Url && s.IsActive);
            if (exists)
                throw new ArgumentException("An active source with this URL already exists.");
        }

        var source = new GovernmentSource
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Url = dto.Url?.Trim(),
            Department = dto.Department?.Trim(),
            CountryCode = dto.CountryCode,
            IsActive = true,
        };

        _dbContext.GovernmentSources.Add(source);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Registered government source {SourceId}: {SourceName}", source.Id, source.Name);

        return MapSourceToDto(source);
    }

    public async Task<List<GovernmentSourceDto>> GetSourcesAsync(string countryCode = "MY")
    {
        var sources = await _dbContext.GovernmentSources
            .Where(s => s.CountryCode == countryCode)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return sources.Select(MapSourceToDto).ToList();
    }

    public async Task<GovernmentSourceDto?> GetSourceAsync(Guid sourceId)
    {
        var source = await _dbContext.GovernmentSources.FindAsync(sourceId);
        return source is null ? null : MapSourceToDto(source);
    }

    public async Task DeactivateSourceAsync(Guid sourceId)
    {
        var source = await _dbContext.GovernmentSources.FindAsync(sourceId)
            ?? throw new KeyNotFoundException("Source not found.");

        source.IsActive = false;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deactivated government source {SourceId}", sourceId);
    }

    public async Task<IngestionResultDto> IngestSourceAsync(Guid sourceId, IngestContentDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            throw new ArgumentException("Content is required.");

        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new ArgumentException("Title is required.");

        var source = await _dbContext.GovernmentSources.FindAsync(sourceId)
            ?? throw new KeyNotFoundException("Source not found.");

        var contentHash = ComputeHash(dto.Content);

        if (source.ContentHash == contentHash)
        {
            return new IngestionResultDto
            {
                SourceId = sourceId,
                ChunksCreated = 0,
                ContentChanged = false,
            };
        }

        var existingChunks = await _dbContext.GovernmentKnowledge
            .Where(k => k.GovernmentSourceId == sourceId)
            .ToListAsync();
        _dbContext.GovernmentKnowledge.RemoveRange(existingChunks);

        var chunks = ChunkText(dto.Content);
        var knowledgeItems = new List<GovernmentKnowledge>();

        for (var i = 0; i < chunks.Count; i++)
        {
            knowledgeItems.Add(new GovernmentKnowledge
            {
                Id = Guid.NewGuid(),
                Title = dto.Title.Trim(),
                Content = chunks[i],
                SourceUrl = source.Url,
                Department = source.Department,
                Category = dto.Category?.Trim(),
                CountryCode = source.CountryCode,
                GovernmentSourceId = sourceId,
                ChunkIndex = i,
                ContentHash = ComputeHash(chunks[i]),
            });
        }

        _dbContext.GovernmentKnowledge.AddRange(knowledgeItems);

        source.ContentHash = contentHash;
        source.LastIngestedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Ingested {ChunkCount} chunks for source {SourceId}", knowledgeItems.Count, sourceId);

        return new IngestionResultDto
        {
            SourceId = sourceId,
            ChunksCreated = knowledgeItems.Count,
            ContentChanged = true,
        };
    }

    public async Task<EmbeddingGenerationResultDto> GenerateEmbeddingsAsync(int batchSize = 50)
    {
        var pending = await _dbContext.GovernmentKnowledge
            .Where(k => k.EmbeddedAt == null)
            .OrderBy(k => k.CreatedAt)
            .Take(batchSize)
            .ToListAsync();

        var totalPending = await _dbContext.GovernmentKnowledge
            .CountAsync(k => k.EmbeddedAt == null);

        var generated = 0;
        foreach (var item in pending)
        {
            try
            {
                var textToEmbed = $"title: {item.Title} | text: {item.Content}";
                var embedding = await _aiService.GenerateEmbeddingAsync(textToEmbed);
                item.Embedding = new Pgvector.Vector(embedding);
                item.EmbeddedAt = DateTime.UtcNow;
                generated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to generate embedding for knowledge {KnowledgeId} category={Category}", item.Id, ex.GetType().Name);
                break;
            }
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Generated {Count} embeddings, {Remaining} still pending",
            generated, totalPending - generated);

        return new EmbeddingGenerationResultDto
        {
            EmbeddingsGenerated = generated,
            TotalPending = totalPending - generated,
        };
    }

    public static List<string> ChunkText(string text)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        var normalized = text.Replace("\r\n", "\n").Trim();

        if (normalized.Length <= TargetChunkSize)
        {
            chunks.Add(normalized);
            return chunks;
        }

        var position = 0;
        while (position < normalized.Length)
        {
            var end = Math.Min(position + TargetChunkSize, normalized.Length);

            if (end < normalized.Length)
            {
                var breakPoint = normalized.LastIndexOf("\n\n", end, Math.Min(end - position, 500));
                if (breakPoint <= position)
                    breakPoint = normalized.LastIndexOf('\n', end, Math.Min(end - position, 300));
                if (breakPoint <= position)
                    breakPoint = normalized.LastIndexOf(' ', end, Math.Min(end - position, 200));
                if (breakPoint > position)
                    end = breakPoint;
            }

            var chunk = normalized[position..end].Trim();
            if (chunk.Length > 0)
                chunks.Add(chunk);

            position = end - ChunkOverlap;
            if (position <= (chunks.Count > 0 ? end - chunk.Length : 0))
                position = end;
        }

        return chunks;
    }

    public static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static GovernmentSourceDto MapSourceToDto(GovernmentSource source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Url = source.Url,
        Department = source.Department,
        CountryCode = source.CountryCode,
        IsActive = source.IsActive,
        LastIngestedAt = source.LastIngestedAt,
        CreatedAt = source.CreatedAt,
    };
}
