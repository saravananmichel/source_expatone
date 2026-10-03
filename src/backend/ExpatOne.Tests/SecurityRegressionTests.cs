using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

// ============================================================
// B1 — Unauthenticated access to protected endpoints
// ============================================================
[Collection("Integration")]
public class UnauthenticatedAccessTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public UnauthenticatedAccessTests(AppFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ExpatOneDbContext>));
                if (descriptor != null) services.Remove(descriptor);
                services.AddDbContext<ExpatOneDbContext>(options =>
                    options.UseInMemoryDatabase($"UnauthTests_{Guid.NewGuid()}"));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);

                var mockSearch = new Mock<IKnowledgeSearchService>();
                mockSearch.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                    .ReturnsAsync(new List<KnowledgeSearchResult>());
                services.AddSingleton(mockSearch.Object);
            });

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Knowledge:AdminKey"] = "test-key",
                });
            });
        });
    }

    [Fact]
    public async Task Reminders_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/reminders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task KnowledgeSearch_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/knowledge/search?q=test");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TranslationLanguages_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/translation/languages");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EmergencyAssist_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/emergency/assist", new { message = "help" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

// ============================================================
// B2 — IDOR / Authorization tests
// ============================================================
public class DocumentAnalysisIdorTests
{
    private static readonly Guid PassportTypeId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid UserAId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid UserBId = Guid.Parse("b0000000-0000-0000-0000-000000000001");

    private (ExpatOneDbContext ctx, DocumentAnalysisService svc) CreateService()
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
            Id = UserAId, ExternalId = "uid-a2", ExternalProvider = "firebase", Email = "a2@test.com"
        });
        ctx.Users.Add(new User
        {
            Id = UserBId, ExternalId = "uid-b2", ExternalProvider = "firebase", Email = "b2@test.com"
        });
        ctx.SaveChanges();

        var mockStorage = new Mock<IStorageService>();
        mockStorage.Setup(s => s.DownloadFileAsync(It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // minimal PDF header

        var mockAi = new Mock<IAIService>();
        mockAi.Setup(a => a.AnalyzeDocumentAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new AIResponse
            {
                Content = "{\"documentCategory\":\"Passport\",\"summary\":\"Test\"}",
                StructuredJson = "{\"documentCategory\":\"Passport\",\"summary\":\"Test\"}"
            });

        var logger = new Mock<ILogger<DocumentAnalysisService>>().Object;
        var svc = new DocumentAnalysisService(ctx, new Mock<IDocumentAnalysisJobs>().Object, new Mock<IDocumentIntelligenceService>().Object);
        return (ctx, svc);
    }

    [Fact]
    public async Task AnalyzeDocument_OtherUser_ThrowsKeyNotFound()
    {
        var (ctx, svc) = CreateService();

        // Create document belonging to User A
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = UserAId,
            DocumentTypeId = PassportTypeId,
            DocumentName = "UserA Passport",
            S3ObjectKey = "users/userA/documents/doc1/passport.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 100,
            Status = DocumentStatus.Active,
        };
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        // User B attempts to analyze User A's document
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.AnalyzeDocumentAsync(UserBId, doc.Id));
    }

    [Fact]
    public async Task AnalyzeDocument_OwnerCanAnalyze()
    {
        var (ctx, svc) = CreateService();

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = UserAId,
            DocumentTypeId = PassportTypeId,
            DocumentName = "UserA Passport",
            S3ObjectKey = "users/userA/documents/doc2/passport.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 100,
            Status = DocumentStatus.Active,
        };
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        // Owner can retrieve a completed local analysis for their own document.
        ctx.DocumentAnalysisRuns.Add(DocumentAnalysisServiceTests.Run(doc));
        await ctx.SaveChangesAsync();
        var result = await svc.AnalyzeDocumentAsync(UserAId, doc.Id);
        Assert.NotNull(result);
    }
}

public class ReminderGenerationIdorTests
{
    private static readonly Guid PassportTypeId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid UserAId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid UserBId = Guid.Parse("b1000000-0000-0000-0000-000000000001");

    private (ExpatOneDbContext ctx, ReminderService svc, Guid docId) Setup()
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
            Id = UserAId, ExternalId = "uid-a3", ExternalProvider = "firebase", Email = "a3@test.com"
        });
        ctx.Users.Add(new User
        {
            Id = UserBId, ExternalId = "uid-b3", ExternalProvider = "firebase", Email = "b3@test.com"
        });

        var docId = Guid.NewGuid();
        ctx.Documents.Add(new Document
        {
            Id = docId,
            UserId = UserAId,
            DocumentTypeId = PassportTypeId,
            DocumentName = "UserA Passport",
            S3ObjectKey = "k",
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = DocumentStatus.Active,
        });
        ctx.SaveChanges();

        var svc = new ReminderService(ctx, new Mock<ILogger<ReminderService>>().Object);
        return (ctx, svc, docId);
    }

    [Fact]
    public async Task GenerateReminders_OtherUserDocument_ThrowsKeyNotFound()
    {
        var (_, svc, docId) = Setup();

        // User B attempts to generate reminders for User A's document
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GenerateRemindersAsync(UserBId, new GenerateRemindersDto
            {
                DocumentId = docId,
                DaysBeforeExpiry = new List<int> { 30, 7 },
            }));
    }

    [Fact]
    public async Task GenerateReminders_OwnDocument_Succeeds()
    {
        var (_, svc, docId) = Setup();

        var reminders = await svc.GenerateRemindersAsync(UserAId, new GenerateRemindersDto
        {
            DocumentId = docId,
            DaysBeforeExpiry = new List<int> { 30, 7 },
        });

        Assert.Equal(2, reminders.Count);
    }
}

// ============================================================
// B3 — File upload validation
// ============================================================
public class FileUploadValidationTests
{
    private static readonly Guid PassportTypeId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("c0000000-0000-0000-0000-000000000001");

    private (ExpatOneDbContext ctx, DocumentService svc) CreateService()
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
            Id = UserId, ExternalId = "uid-c", ExternalProvider = "firebase", Email = "c@test.com"
        });
        ctx.SaveChanges();

        var mockStorage = new Mock<IStorageService>();
        mockStorage.Setup(s => s.GeneratePresignedUploadUrl(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns("https://s3.example.com/upload");

        var logger = new Mock<ILogger<DocumentService>>().Object;
        var auditService = new DocumentAuditService(ctx);
        return (ctx, new DocumentService(ctx, mockStorage.Object, auditService, logger));
    }

    [Fact]
    public async Task Upload_OversizedFile_IsRejected()
    {
        var (_, svc) = CreateService();
        var dto = new RequestUploadDto
        {
            DocumentTypeId = PassportTypeId,
            DocumentName = "Big File",
            FileName = "big.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 11 * 1024 * 1024, // 11 MB — over 10 MB limit
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RequestUploadAsync(UserId, dto));
        Assert.Contains("maximum", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("application/octet-stream")]
    [InlineData("text/html")]
    [InlineData("application/x-msdownload")]
    [InlineData("application/zip")]
    public async Task Upload_UnsupportedMimeType_IsRejected(string contentType)
    {
        var (_, svc) = CreateService();
        var dto = new RequestUploadDto
        {
            DocumentTypeId = PassportTypeId,
            DocumentName = "Bad File",
            FileName = "file.pdf",
            ContentType = contentType,
            FileSizeBytes = 500_000,
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RequestUploadAsync(UserId, dto));
    }

    [Theory]
    [InlineData("../../../etc/passwd.pdf")]
    [InlineData("/etc/passwd.pdf")]
    public async Task Upload_MaliciousFilename_SanitizedInObjectKey(string maliciousFileName)
    {
        var (ctx, svc) = CreateService();
        var dto = new RequestUploadDto
        {
            DocumentTypeId = PassportTypeId,
            DocumentName = "Passport",
            FileName = maliciousFileName,
            ContentType = "application/pdf",
            FileSizeBytes = 100_000,
        };

        var result = await svc.RequestUploadAsync(UserId, dto);

        // Retrieve the document and verify the S3 key
        var doc = await ctx.Documents.FindAsync(result.DocumentId);
        Assert.NotNull(doc);

        // Object key must stay within the user's path prefix
        Assert.StartsWith($"users/{UserId}/documents/", doc!.S3ObjectKey);

        // Must not contain directory traversal sequences
        Assert.DoesNotContain("..", doc.S3ObjectKey);
        Assert.DoesNotContain("/etc/", doc.S3ObjectKey);
        Assert.DoesNotContain("\\", doc.S3ObjectKey);
    }
}
