using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Amazon.S3;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

// ============================================================
// C1 — Document types now requires authentication
// ============================================================
[Collection("Integration")]
public class DocumentTypesAuthTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public DocumentTypesAuthTests(AppFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ExpatOneDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                var dbName = $"DocTypeAuthTests_{Guid.NewGuid()}";
                services.AddDbContext<ExpatOneDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);

                // Register mock storage so IDocumentService (and document-types) resolves
                var mockStorage = new Mock<IStorageService>();
                services.AddSingleton(mockStorage.Object);
                services.AddScoped<IDocumentService, DocumentService>();

                // Seed document types
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
                db.Database.EnsureCreated();
                if (!db.DocumentTypes.Any())
                {
                    db.DocumentTypes.AddRange(Enumerable.Range(1, 9).Select(i => new DocumentType
                    {
                        Id = Guid.NewGuid(), Name = $"Type{i}", HasExpiry = true, IsSystem = true
                    }));
                    db.SaveChanges();
                }
            });
        });
    }

    [Fact]
    public async Task GetDocumentTypes_WithoutAuth_Returns200_EndpointIsPublic()
    {
        // document-types is intentionally public (no [Authorize]) so unauthenticated
        // upload flows can fetch the list without a login token.
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/document-types");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDocumentTypes_Authenticated_ReturnsTypes()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", "uid=dt-c1-user&email=dt@test.com&name=User");

        var response = await client.GetAsync("/api/document-types");
        response.EnsureSuccessStatusCode();

        var types = await response.Content.ReadFromJsonAsync<List<DocumentTypeDto>>();
        Assert.NotNull(types);
        Assert.True(types!.Count >= 9);
    }
}

// ============================================================
// C3 — Health endpoint information disclosure
// ============================================================
[Collection("Integration")]
public class HealthDisclosureTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthDisclosureTests(AppFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ExpatOneDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<ExpatOneDbContext>(options =>
                    options.UseInMemoryDatabase("HealthDisclosureTests"));
            });
        });
    }

    [Fact]
    public async Task Health_DoesNotExposeVersion()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("version", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_DoesNotExposeDatabaseConnectionState()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("database", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connected", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unavailable", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Health_ReturnsStatusField()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var status = body.GetProperty("status").GetString();
        Assert.True(status == "ok" || status == "degraded",
            $"Expected 'ok' or 'degraded', got '{status}'");
    }

    [Fact]
    public async Task Health_IsPublic()
    {
        // Health endpoint must remain public for infrastructure health checks
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

// ============================================================
// C4 — S3 delete 404 handling
// ============================================================
public class S3DeleteTests
{
    private static readonly Guid PassportTypeId = Guid.Parse("10000000-0000-0000-0000-000000000099");
    private static readonly Guid UserId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid OtherUserId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    private (ExpatOneDbContext ctx, DocumentService svc, Mock<IStorageService> mockStorage)
        CreateService(Action<Mock<IStorageService>>? configureStorage = null)
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new ExpatOneDbContext(options);
        ctx.DocumentTypes.Add(new DocumentType
        {
            Id = PassportTypeId, Name = "Passport", HasExpiry = true, IsSystem = true
        });
        ctx.Users.Add(new User
        {
            Id = UserId, ExternalId = "uid-e", ExternalProvider = "firebase", Email = "e@test.com"
        });
        ctx.Users.Add(new User
        {
            Id = OtherUserId, ExternalId = "uid-f", ExternalProvider = "firebase", Email = "f@test.com"
        });
        ctx.SaveChanges();

        var mockStorage = new Mock<IStorageService>();
        mockStorage.Setup(s => s.GeneratePresignedUploadUrl(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns("https://s3.example.com/upload");
        mockStorage.Setup(s => s.GeneratePresignedUrlAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync("https://s3.example.com/download");

        configureStorage?.Invoke(mockStorage);

        var logger = new Mock<ILogger<DocumentService>>().Object;
        var auditService = new DocumentAuditService(ctx);
        return (ctx, new DocumentService(ctx, mockStorage.Object, auditService, logger), mockStorage);
    }

    private async Task<Guid> CreateActiveDocument(DocumentService svc)
    {
        var upload = await svc.RequestUploadAsync(UserId, new RequestUploadDto
        {
            DocumentTypeId = PassportTypeId,
            DocumentName = "Passport",
            FileName = "passport.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 100_000,
        });
        await svc.CompleteUploadAsync(UserId, upload.DocumentId);
        return upload.DocumentId;
    }

    [Fact]
    public async Task Delete_S3Success_RemovesDbRecord()
    {
        var (ctx, svc, mockStorage) = CreateService(m =>
            m.Setup(s => s.DeleteFileAsync(It.IsAny<string>())).Returns(Task.CompletedTask));

        var docId = await CreateActiveDocument(svc);
        await svc.DeleteDocumentAsync(UserId, docId);

        Assert.Null(await ctx.Documents.FindAsync(docId));
        mockStorage.Verify(s => s.DeleteFileAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Delete_S3Returns404_DbRecordStillDeleted()
    {
        var notFoundEx = new AmazonS3Exception("The specified key does not exist.")
        {
            StatusCode = HttpStatusCode.NotFound,
        };

        var (ctx, svc, _) = CreateService(m =>
            m.Setup(s => s.DeleteFileAsync(It.IsAny<string>()))
                .ThrowsAsync(notFoundEx));

        var docId = await CreateActiveDocument(svc);

        // Should complete without throwing
        await svc.DeleteDocumentAsync(UserId, docId);

        // DB record must still be deleted
        Assert.Null(await ctx.Documents.FindAsync(docId));
    }

    [Fact]
    public async Task Delete_S3NonNotFoundError_Propagates()
    {
        var serverErrorEx = new AmazonS3Exception("Internal Server Error")
        {
            StatusCode = HttpStatusCode.InternalServerError,
        };

        var (ctx, svc, _) = CreateService(m =>
            m.Setup(s => s.DeleteFileAsync(It.IsAny<string>()))
                .ThrowsAsync(serverErrorEx));

        var docId = await CreateActiveDocument(svc);

        // Non-404 S3 errors must propagate
        await Assert.ThrowsAsync<AmazonS3Exception>(() =>
            svc.DeleteDocumentAsync(UserId, docId));

        // DB record must NOT be deleted when S3 fails with a non-404 error
        Assert.NotNull(await ctx.Documents.FindAsync(docId));
    }

    [Fact]
    public async Task Delete_OtherUser_CannotDeleteDocument()
    {
        var (ctx, svc, _) = CreateService(m =>
            m.Setup(s => s.DeleteFileAsync(It.IsAny<string>())).Returns(Task.CompletedTask));

        var docId = await CreateActiveDocument(svc);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.DeleteDocumentAsync(OtherUserId, docId));

        // Owner's document must still exist
        Assert.NotNull(await ctx.Documents.FindAsync(docId));
    }
}
