using System.Net;
using ExpatOne.Api.Services;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

public class DocumentTextExtractionWorkerTests
{
    private static readonly Guid DocumentTypeId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");

    private sealed class ExtractionHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/extract", request.RequestUri!.AbsolutePath);
            Assert.True(request.Headers.Contains("X-Service-Key"));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"extractedText":"Synthetic extracted text","pageCount":1}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            });
        }
    }

    private static ServiceProvider BuildProvider(
        SqliteConnection connection,
        Mock<IStorageService> storage)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentTextExtraction:ServiceKey"] = "test-service-key"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ExpatOneDbContext>(
            options => options.UseSqlite(connection));
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(storage.Object);
        services.AddScoped(_ => new DocumentTextExtractionClient(
            new HttpClient(new ExtractionHandler())
            {
                BaseAddress = new Uri("http://localhost/")
            },
            configuration));

        return services.BuildServiceProvider();
    }

    private static async Task<Document> AddDocumentAsync(
        ServiceProvider provider,
        string extractionStatus,
        int attempts,
        DateTime leaseUntil)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        await db.Database.EnsureCreatedAsync();

        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalProvider = "test",
            ExternalId = Guid.NewGuid().ToString(),
            Email = $"{Guid.NewGuid():N}@example.test"
        };

        var document = new Document
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DocumentTypeId = DocumentTypeId,
            DocumentName = "Synthetic document",
            S3ObjectKey = "synthetic-document",
            ContentType = "application/pdf",
            FileSizeBytes = 3,
            ExtractionStatus = extractionStatus,
            ExtractionAttempts = attempts,
            ExtractionLeaseUntil = leaseUntil,
            ExtractionLeaseToken = Guid.NewGuid()
        };

        db.Users.Add(user);
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return document;
    }

    [Fact]
    public async Task ExpiredFinalAttemptIsFailedWithoutIncrementingAttempts()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        using var provider = BuildProvider(connection, storage);

        var document = await AddDocumentAsync(
            provider, "PROCESSING", 5, DateTime.UtcNow.AddMinutes(-10));

        var worker = new DocumentTextExtractionWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<DocumentTextExtractionWorker>>());

        Assert.True(await worker.ProcessOne(default));

        using var verifyScope = provider.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var saved = await db.Documents.SingleAsync(d => d.Id == document.Id);

        Assert.Equal("FAILED", saved.ExtractionStatus);
        Assert.Equal(5, saved.ExtractionAttempts);
        Assert.Null(saved.ExtractionLeaseUntil);
        Assert.Null(saved.ExtractionLeaseToken);
        storage.Verify(
            x => x.DownloadFileAsync(It.IsAny<string>()),
            Times.Never);
        Assert.False(await worker.ProcessOne(default));
    }

    [Fact]
    public async Task ExpiredProcessingJobBelowLimitCanCompleteFinalAllowedAttempt()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var storage = new Mock<IStorageService>();
        storage.Setup(x => x.DownloadFileAsync(It.IsAny<string>()))
            .ReturnsAsync(() => (Stream)new MemoryStream([1, 2, 3]));

        using var provider = BuildProvider(connection, storage);

        var document = await AddDocumentAsync(
            provider, "PROCESSING", 4, DateTime.UtcNow.AddMinutes(-10));

        var worker = new DocumentTextExtractionWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<DocumentTextExtractionWorker>>());

        Assert.True(await worker.ProcessOne(default));

        using var verifyScope = provider.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var saved = await db.Documents.SingleAsync(d => d.Id == document.Id);

        Assert.Equal("COMPLETED", saved.ExtractionStatus);
        Assert.Equal(5, saved.ExtractionAttempts);
        Assert.Equal("Synthetic extracted text", saved.ExtractedText);
        Assert.Null(saved.ExtractionLeaseUntil);
        Assert.Null(saved.ExtractionLeaseToken);
        storage.Verify(
            x => x.DownloadFileAsync("synthetic-document"),
            Times.Once);
    }
}
