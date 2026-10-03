using System.Net;
using System.Text;
using System.Text.Json;
using ExpatOne.Api.Services;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
namespace ExpatOne.Tests;

public class DocumentWorkerRelationalTests
{
    private class ModelHandler : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var result = new DocumentAnalysisDto { DocumentCategory = "Passport", Summary = "Synthetic document.", Provider = "Local",
                ModelVersion = "synthetic", RequiresReview = true,
                Statements = [new GroundedStatement { Id = "s1", Text = "Synthetic fact", EvidenceIds = ["e1"] }],
                Evidence = [new DocumentEvidenceDto { Id = "e1", Page = 1, SourceText = "Synthetic" }] };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(result), Encoding.UTF8, "application/json") });
        }
    }
    [Fact]
    public async Task WorkerClaimsPersistsAndRecoversAnExpiredLease()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); connection.Open();
        var services = new ServiceCollection(); services.AddLogging();
        services.AddDbContext<ExpatOneDbContext>(options => options.UseSqlite(connection));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        var storage = new Mock<IStorageService>();
        storage.Setup(x => x.DownloadFileAsync(It.IsAny<string>())).ReturnsAsync(() => (Stream)new MemoryStream([1,2,3]));
        services.AddSingleton(storage.Object);
        var handler = new ModelHandler();
        services.AddScoped(_ => new DocumentIntelligenceService(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }, new ConfigurationBuilder().Build()));
        using var provider = services.BuildServiceProvider();
        var docId = Guid.NewGuid(); var runId = Guid.NewGuid();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>(); await db.Database.EnsureCreatedAsync();
            var user = new User { Id = Guid.NewGuid(), ExternalProvider = "test", ExternalId = "synthetic", Email = "synthetic@example.test" };
            db.Users.Add(user);
            db.Documents.Add(new Document { Id = docId, UserId = user.Id, DocumentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000001"), DocumentName = "Synthetic", S3ObjectKey = "synthetic", ContentType = "application/pdf" });
            db.DocumentAnalysisRuns.Add(new DocumentAnalysisRun { Id = runId, DocumentId = docId, UserId = user.Id, ObjectKey = "synthetic", ContentType = "application/pdf", ConfigurationVersion = "v1", Status = "PROCESSING", LeaseUntil = DateTime.UtcNow.AddMinutes(-10) });
            await db.SaveChangesAsync();
        }
        var worker = new DocumentIntelligenceWorker(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<ILogger<DocumentIntelligenceWorker>>());
        Assert.True(await worker.ProcessOne(default));
        Assert.False(await worker.ProcessOne(default));
        using var verify = provider.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var run = await context.DocumentAnalysisRuns.SingleAsync();
        Assert.Equal("REQUIRES_REVIEW", run.Status);
        Assert.Equal(1, run.Attempts); Assert.NotNull(run.ContentHash); Assert.NotNull(run.ResultJson);
        Assert.NotNull((await context.Documents.SingleAsync()).ExtractedMetadata);
        Assert.Equal(1, handler.Calls);
    }
}
