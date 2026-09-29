using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

public class KnowledgeIngestionServiceTests
{
    private (ExpatOneDbContext context, KnowledgeIngestionService service, Mock<IAIService> mockAi) CreateService()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ExpatOneDbContext(options);

        var mockAi = new Mock<IAIService>();
        mockAi.Setup(a => a.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[768]);

        var logger = new Mock<ILogger<KnowledgeIngestionService>>().Object;
        var service = new KnowledgeIngestionService(context, mockAi.Object, logger);

        return (context, service, mockAi);
    }

    [Fact]
    public async Task RegisterSource_Succeeds()
    {
        var (_, svc, _) = CreateService();

        var result = await svc.RegisterSourceAsync(new RegisterSourceDto
        {
            Name = "Immigration Dept",
            Url = "https://www.imi.gov.my",
            Department = "Immigration",
        });

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Immigration Dept", result.Name);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task RegisterSource_DuplicateUrl_Rejected()
    {
        var (_, svc, _) = CreateService();

        await svc.RegisterSourceAsync(new RegisterSourceDto
        {
            Name = "Source A",
            Url = "https://www.imi.gov.my",
        });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RegisterSourceAsync(new RegisterSourceDto
            {
                Name = "Source B",
                Url = "https://www.imi.gov.my",
            }));
    }

    [Fact]
    public async Task RegisterSource_EmptyName_Rejected()
    {
        var (_, svc, _) = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RegisterSourceAsync(new RegisterSourceDto { Name = "" }));
    }

    [Fact]
    public async Task IngestSource_CreatesChunks()
    {
        var (ctx, svc, _) = CreateService();

        var source = await svc.RegisterSourceAsync(new RegisterSourceDto { Name = "Test" });

        var result = await svc.IngestSourceAsync(source.Id, new IngestContentDto
        {
            Title = "Test Doc",
            Content = "Some government content about immigration procedures.",
            Category = "Immigration",
        });

        Assert.True(result.ContentChanged);
        Assert.True(result.ChunksCreated > 0);

        var chunks = await ctx.GovernmentKnowledge
            .Where(k => k.GovernmentSourceId == source.Id)
            .ToListAsync();
        Assert.Equal(result.ChunksCreated, chunks.Count);
        Assert.All(chunks, c => Assert.Equal("Test Doc", c.Title));
        Assert.All(chunks, c => Assert.Equal("Immigration", c.Category));
    }

    [Fact]
    public async Task IngestSource_SameContent_Idempotent()
    {
        var (_, svc, _) = CreateService();

        var source = await svc.RegisterSourceAsync(new RegisterSourceDto { Name = "Test" });
        var content = "Same content for testing idempotency.";

        var first = await svc.IngestSourceAsync(source.Id, new IngestContentDto
        {
            Title = "Test",
            Content = content,
        });

        var second = await svc.IngestSourceAsync(source.Id, new IngestContentDto
        {
            Title = "Test",
            Content = content,
        });

        Assert.True(first.ContentChanged);
        Assert.False(second.ContentChanged);
        Assert.Equal(0, second.ChunksCreated);
    }

    [Fact]
    public async Task IngestSource_ChangedContent_ReplacesChunks()
    {
        var (ctx, svc, _) = CreateService();

        var source = await svc.RegisterSourceAsync(new RegisterSourceDto { Name = "Test" });

        await svc.IngestSourceAsync(source.Id, new IngestContentDto
        {
            Title = "V1",
            Content = "Original content.",
        });

        await svc.IngestSourceAsync(source.Id, new IngestContentDto
        {
            Title = "V2",
            Content = "Updated content with new information.",
        });

        var chunks = await ctx.GovernmentKnowledge
            .Where(k => k.GovernmentSourceId == source.Id)
            .ToListAsync();
        Assert.All(chunks, c => Assert.Equal("V2", c.Title));
    }

    [Fact]
    public async Task IngestSource_ChunkRetainsSourceMetadata()
    {
        var (ctx, svc, _) = CreateService();

        var source = await svc.RegisterSourceAsync(new RegisterSourceDto
        {
            Name = "IMI",
            Url = "https://www.imi.gov.my/ep",
            Department = "Immigration",
        });

        await svc.IngestSourceAsync(source.Id, new IngestContentDto
        {
            Title = "Employment Pass",
            Content = "How to apply for employment pass.",
            Category = "Work Permits",
        });

        var chunk = await ctx.GovernmentKnowledge
            .FirstAsync(k => k.GovernmentSourceId == source.Id);

        Assert.Equal("Employment Pass", chunk.Title);
        Assert.Equal("Immigration", chunk.Department);
        Assert.Equal("Work Permits", chunk.Category);
        Assert.Equal("https://www.imi.gov.my/ep", chunk.SourceUrl);
        Assert.Equal("MY", chunk.CountryCode);
        Assert.Equal(source.Id, chunk.GovernmentSourceId);
        Assert.Equal(0, chunk.ChunkIndex);
        Assert.NotNull(chunk.ContentHash);
    }

    [Fact]
    public async Task IngestSource_ChunkIndexSequential()
    {
        var (ctx, svc, _) = CreateService();

        var source = await svc.RegisterSourceAsync(new RegisterSourceDto { Name = "Test" });

        var longContent = string.Join("\n\n", Enumerable.Range(0, 50)
            .Select(i => $"Paragraph {i}: " + new string('x', 100)));

        await svc.IngestSourceAsync(source.Id, new IngestContentDto
        {
            Title = "Long Doc",
            Content = longContent,
        });

        var chunks = await ctx.GovernmentKnowledge
            .Where(k => k.GovernmentSourceId == source.Id)
            .OrderBy(k => k.ChunkIndex)
            .ToListAsync();

        Assert.True(chunks.Count > 1);
        for (var i = 0; i < chunks.Count; i++)
            Assert.Equal(i, chunks[i].ChunkIndex);
    }

    [Fact]
    public async Task IngestSource_EmptyContent_Rejected()
    {
        var (_, svc, _) = CreateService();

        var source = await svc.RegisterSourceAsync(new RegisterSourceDto { Name = "Test" });

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.IngestSourceAsync(source.Id, new IngestContentDto
            {
                Title = "Test",
                Content = "",
            }));
    }

    [Fact]
    public async Task IngestSource_NonExistentSource_NotFound()
    {
        var (_, svc, _) = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.IngestSourceAsync(Guid.NewGuid(), new IngestContentDto
            {
                Title = "Test",
                Content = "Content",
            }));
    }

    [Fact]
    public async Task DeactivateSource_Succeeds()
    {
        var (ctx, svc, _) = CreateService();

        var source = await svc.RegisterSourceAsync(new RegisterSourceDto { Name = "Test" });
        await svc.DeactivateSourceAsync(source.Id);

        var dbSource = await ctx.GovernmentSources.FindAsync(source.Id);
        Assert.False(dbSource!.IsActive);
    }

    [Fact]
    public async Task DeactivateSource_NonExistent_NotFound()
    {
        var (_, svc, _) = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.DeactivateSourceAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GenerateEmbeddings_CallsAIServiceForPending()
    {
        var (ctx, svc, mockAi) = CreateService();

        var source = await svc.RegisterSourceAsync(new RegisterSourceDto { Name = "Test" });
        await svc.IngestSourceAsync(source.Id, new IngestContentDto
        {
            Title = "Doc",
            Content = "Some content.",
        });

        var result = await svc.GenerateEmbeddingsAsync();

        Assert.True(result.EmbeddingsGenerated > 0);
        Assert.Equal(0, result.TotalPending);
        mockAi.Verify(a => a.GenerateEmbeddingAsync(It.Is<string>(s => s.Contains("title:"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetSources_ReturnsRegistered()
    {
        var (_, svc, _) = CreateService();

        await svc.RegisterSourceAsync(new RegisterSourceDto { Name = "Source A" });
        await svc.RegisterSourceAsync(new RegisterSourceDto { Name = "Source B" });

        var sources = await svc.GetSourcesAsync();
        Assert.Equal(2, sources.Count);
    }

    [Fact]
    public void ChunkText_ShortContent_SingleChunk()
    {
        var chunks = KnowledgeIngestionService.ChunkText("Short text.");
        Assert.Single(chunks);
        Assert.Equal("Short text.", chunks[0]);
    }

    [Fact]
    public void ChunkText_LongContent_MultipleChunks()
    {
        var content = string.Join("\n\n", Enumerable.Range(0, 50)
            .Select(i => new string('a', 100)));

        var chunks = KnowledgeIngestionService.ChunkText(content);
        Assert.True(chunks.Count > 1);
    }

    [Fact]
    public void ComputeHash_Deterministic()
    {
        var hash1 = KnowledgeIngestionService.ComputeHash("test content");
        var hash2 = KnowledgeIngestionService.ComputeHash("test content");
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }

    [Fact]
    public void ComputeHash_DifferentContent_DifferentHash()
    {
        var hash1 = KnowledgeIngestionService.ComputeHash("content A");
        var hash2 = KnowledgeIngestionService.ComputeHash("content B");
        Assert.NotEqual(hash1, hash2);
    }
}
