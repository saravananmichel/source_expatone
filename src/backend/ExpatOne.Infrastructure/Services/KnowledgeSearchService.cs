using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace ExpatOne.Infrastructure.Services;

public class KnowledgeSearchService : IKnowledgeSearchService
{
    private readonly ExpatOneDbContext _dbContext;
    private readonly IAIService _aiService;
    private readonly ILogger<KnowledgeSearchService> _logger;

    public KnowledgeSearchService(
        ExpatOneDbContext dbContext,
        IAIService aiService,
        ILogger<KnowledgeSearchService> logger)
    {
        _dbContext = dbContext;
        _aiService = aiService;
        _logger = logger;
    }

    private const int MaxResults = 20;

    public async Task<List<KnowledgeSearchResult>> SearchAsync(string query, string countryCode = "MY", int maxResults = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Search query is required.");

        if (maxResults <= 0)
            throw new ArgumentException("maxResults must be positive.");

        maxResults = Math.Min(maxResults, MaxResults);

        var queryText = $"task: search result | query: {query}";
        float[] queryEmbedding;
        try
        {
            queryEmbedding = await _aiService.GenerateEmbeddingAsync(queryText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for search query");
            throw new InvalidOperationException("Unable to process your search. Please try again.");
        }

        var queryVector = new Vector(queryEmbedding);

        var sql = """
            SELECT gk."Id", gk."Title", gk."Content", gk."SourceUrl", gk."Department",
                   gk."Category", gk."CountryCode", gk."LastVerifiedDate",
                   gs."Name" as "SourceName",
                   1 - (gk."Embedding" <=> @queryVector::vector) as "RelevanceScore"
            FROM government_knowledge gk
            LEFT JOIN government_sources gs ON gk."GovernmentSourceId" = gs."Id"
            WHERE gk."Embedding" IS NOT NULL AND gk."CountryCode" = @countryCode
                  AND (gs."Id" IS NULL OR gs."IsActive" = true)
            ORDER BY gk."Embedding" <=> @queryVector::vector
            LIMIT @maxResults
            """;

        var connection = _dbContext.Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            var vectorParam = new NpgsqlParameter("@queryVector", NpgsqlDbType.Unknown)
            {
                Value = queryVector.ToString()
            };
            command.Parameters.Add(vectorParam);
            command.Parameters.Add(new NpgsqlParameter("@countryCode", countryCode));
            command.Parameters.Add(new NpgsqlParameter("@maxResults", maxResults));

            using var reader = await command.ExecuteReaderAsync();
            var results = new List<KnowledgeSearchResult>();

            while (await reader.ReadAsync())
            {
                results.Add(new KnowledgeSearchResult
                {
                    KnowledgeId = reader.GetGuid(reader.GetOrdinal("Id")),
                    Title = reader.GetString(reader.GetOrdinal("Title")),
                    Content = reader.GetString(reader.GetOrdinal("Content")),
                    SourceUrl = reader.IsDBNull(reader.GetOrdinal("SourceUrl")) ? null : reader.GetString(reader.GetOrdinal("SourceUrl")),
                    SourceName = reader.IsDBNull(reader.GetOrdinal("SourceName")) ? null : reader.GetString(reader.GetOrdinal("SourceName")),
                    Department = reader.IsDBNull(reader.GetOrdinal("Department")) ? null : reader.GetString(reader.GetOrdinal("Department")),
                    Category = reader.IsDBNull(reader.GetOrdinal("Category")) ? null : reader.GetString(reader.GetOrdinal("Category")),
                    CountryCode = reader.GetString(reader.GetOrdinal("CountryCode")),
                    LastVerifiedDate = reader.IsDBNull(reader.GetOrdinal("LastVerifiedDate")) ? null : reader.GetDateTime(reader.GetOrdinal("LastVerifiedDate")),
                    RelevanceScore = reader.GetDouble(reader.GetOrdinal("RelevanceScore")),
                });
            }

            return results;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
