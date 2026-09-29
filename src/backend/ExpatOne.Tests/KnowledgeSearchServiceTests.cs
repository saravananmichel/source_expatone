using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

public class KnowledgeSearchServiceTests
{
    private (ExpatOneDbContext context, Mock<IAIService> mockAi) CreateContext()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ExpatOneDbContext(options);

        var mockAi = new Mock<IAIService>();
        mockAi.Setup(a => a.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(new float[768]);

        return (context, mockAi);
    }

    [Fact]
    public async Task Search_CallsEmbeddingWithQueryPrefix()
    {
        var (ctx, mockAi) = CreateContext();
        var logger = new Mock<ILogger<KnowledgeSearchService>>().Object;
        var svc = new KnowledgeSearchService(ctx, mockAi.Object, logger);

        // Search will fail on vector query (InMemory doesn't support pgvector)
        // but we can verify the embedding call was made with correct prefix
        try
        {
            await svc.SearchAsync("employment pass renewal");
        }
        catch
        {
            // Expected: InMemory provider doesn't support vector operations
        }

        mockAi.Verify(a => a.GenerateEmbeddingAsync(
            It.Is<string>(s => s.StartsWith("task: search result | query:"))),
            Times.Once);
    }

    [Fact]
    public async Task Search_EmptyQuery_Rejected()
    {
        var (ctx, mockAi) = CreateContext();
        var logger = new Mock<ILogger<KnowledgeSearchService>>().Object;
        var svc = new KnowledgeSearchService(ctx, mockAi.Object, logger);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SearchAsync(""));
    }

    [Fact]
    public async Task Search_WhitespaceQuery_Rejected()
    {
        var (ctx, mockAi) = CreateContext();
        var logger = new Mock<ILogger<KnowledgeSearchService>>().Object;
        var svc = new KnowledgeSearchService(ctx, mockAi.Object, logger);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.SearchAsync("   "));
    }

    [Fact]
    public async Task Search_EmbeddingFailure_ThrowsControlledError()
    {
        var (ctx, mockAi) = CreateContext();
        mockAi.Setup(a => a.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("API error"));

        var logger = new Mock<ILogger<KnowledgeSearchService>>().Object;
        var svc = new KnowledgeSearchService(ctx, mockAi.Object, logger);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SearchAsync("test query"));

        Assert.Contains("Unable to process", ex.Message);
    }
}
