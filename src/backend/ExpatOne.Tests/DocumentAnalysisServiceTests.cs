using System.Text.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

public class DocumentAnalysisServiceTests
{
    private static readonly Guid PassportTypeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly string ValidAnalysisJson = JsonSerializer.Serialize(new
    {
        documentCategory = "Passport",
        summary = "Malaysian passport issued to holder",
        importantDates = new[]
        {
            new { label = "Issue Date", date = "01 Jan 2024", isExtracted = true },
            new { label = "Expiry Date", date = "01 Jan 2034", isExtracted = true }
        },
        expiryDate = "01 Jan 2034",
        deadlines = new[] { "Renew 6 months before expiry" },
        requiredActions = new[] { "Keep valid at all times while in Malaysia" },
        keyInformation = new[]
        {
            new { label = "Passport Number", value = "A12345678", isExtracted = true },
            new { label = "Nationality", value = "Malaysian", isExtracted = true }
        },
        warnings = new[] { "Document must not be damaged or defaced" },
        terminology = new[]
        {
            new { term = "Immigration endorsement", explanation = "A stamp or sticker placed in the passport by immigration authorities" }
        }
    }, JsonOptions);

    private (ExpatOneDbContext context, DocumentAnalysisService service, Mock<IStorageService> mockStorage, Mock<IAIService> mockAi) CreateService()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new ExpatOneDbContext(options);

        context.DocumentTypes.Add(new DocumentType
        {
            Id = PassportTypeId, Name = "Passport", HasExpiry = true, IsSystem = true
        });
        context.Users.Add(new User
        {
            Id = UserId, ExternalId = "uid-a", ExternalProvider = "firebase", Email = "a@test.com"
        });
        context.Users.Add(new User
        {
            Id = OtherUserId, ExternalId = "uid-b", ExternalProvider = "firebase", Email = "b@test.com"
        });
        context.SaveChanges();

        var mockStorage = new Mock<IStorageService>();
        mockStorage.Setup(s => s.DownloadFileAsync(It.IsAny<string>()))
            .ReturnsAsync(() => new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 })); // %PDF

        var mockAi = new Mock<IAIService>();
        mockAi.Setup(a => a.AnalyzeDocumentAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new AIResponse
            {
                Content = ValidAnalysisJson,
                StructuredJson = ValidAnalysisJson,
            });

        var logger = new Mock<ILogger<DocumentAnalysisService>>().Object;
        var service = new DocumentAnalysisService(context, mockStorage.Object, mockAi.Object, logger);

        return (context, service, mockStorage, mockAi);
    }

    private Document CreateActiveDocument(ExpatOneDbContext context, Guid userId, string contentType = "application/pdf")
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentTypeId = PassportTypeId,
            DocumentName = "Test Document",
            S3ObjectKey = $"users/{userId}/documents/test/doc.pdf",
            ContentType = contentType,
            FileSizeBytes = 500_000,
            Status = DocumentStatus.Active,
        };
        context.Documents.Add(doc);
        context.SaveChanges();
        return doc;
    }

    [Fact]
    public async Task AnalyzeDocument_OwnedPdf_Succeeds()
    {
        var (ctx, svc, _, _) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId, "application/pdf");

        var result = await svc.AnalyzeDocumentAsync(UserId, doc.Id);

        Assert.Equal(doc.Id, result.DocumentId);
        Assert.Equal("Passport", result.DocumentCategory);
        Assert.NotEmpty(result.Summary);
        Assert.NotEmpty(result.ImportantDates);
        Assert.NotEmpty(result.KeyInformation);
    }

    [Fact]
    public async Task AnalyzeDocument_OwnedJpeg_Succeeds()
    {
        var (ctx, svc, _, _) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId, "image/jpeg");

        var result = await svc.AnalyzeDocumentAsync(UserId, doc.Id);

        Assert.Equal(doc.Id, result.DocumentId);
        Assert.NotEmpty(result.Summary);
    }

    [Fact]
    public async Task AnalyzeDocument_OwnedPng_Succeeds()
    {
        var (ctx, svc, _, _) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId, "image/png");

        var result = await svc.AnalyzeDocumentAsync(UserId, doc.Id);

        Assert.Equal(doc.Id, result.DocumentId);
        Assert.NotEmpty(result.Summary);
    }

    [Fact]
    public async Task AnalyzeDocument_OtherUser_Rejected()
    {
        var (ctx, svc, _, _) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.AnalyzeDocumentAsync(OtherUserId, doc.Id));
    }

    [Fact]
    public async Task AnalyzeDocument_CachedAnalysis_ReturnsWithoutGeminiCall()
    {
        var (ctx, svc, _, mockAi) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId);

        var cachedDto = new DocumentAnalysisDto
        {
            DocumentId = doc.Id,
            DocumentCategory = "Cached Passport",
            Summary = "Cached analysis",
            AnalyzedAt = DateTime.UtcNow,
        };
        doc.ExtractedMetadata = JsonSerializer.Serialize(cachedDto, JsonOptions);
        await ctx.SaveChangesAsync();

        var result = await svc.AnalyzeDocumentAsync(UserId, doc.Id);

        Assert.Equal("Cached Passport", result.DocumentCategory);
        mockAi.Verify(a => a.AnalyzeDocumentAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task AnalyzeDocument_ForceReanalyze_BypassesCache()
    {
        var (ctx, svc, _, mockAi) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId);

        doc.ExtractedMetadata = JsonSerializer.Serialize(new DocumentAnalysisDto
        {
            DocumentCategory = "Old", Summary = "Old analysis", AnalyzedAt = DateTime.UtcNow,
        }, JsonOptions);
        await ctx.SaveChangesAsync();

        var result = await svc.AnalyzeDocumentAsync(UserId, doc.Id, forceReanalyze: true);

        Assert.Equal("Passport", result.DocumentCategory);
        mockAi.Verify(a => a.AnalyzeDocumentAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeDocument_MalformedGeminiJson_ThrowsSafely()
    {
        var (ctx, svc, _, mockAi) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId);

        mockAi.Setup(a => a.AnalyzeDocumentAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new AIResponse { Content = "not valid json {{{", StructuredJson = "not valid json {{{"});

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AnalyzeDocumentAsync(UserId, doc.Id));

        Assert.Contains("invalid response", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeDocument_PartialGeminiResponse_HandledSafely()
    {
        var (ctx, svc, _, mockAi) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId);

        var partialJson = JsonSerializer.Serialize(new
        {
            documentCategory = "Visa",
            summary = "A work visa",
        }, JsonOptions);

        mockAi.Setup(a => a.AnalyzeDocumentAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new AIResponse { Content = partialJson, StructuredJson = partialJson });

        var result = await svc.AnalyzeDocumentAsync(UserId, doc.Id);

        Assert.Equal("Visa", result.DocumentCategory);
        Assert.NotNull(result.ImportantDates);
        Assert.Empty(result.ImportantDates);
        Assert.NotNull(result.KeyInformation);
        Assert.Empty(result.KeyInformation);
    }

    [Fact]
    public async Task AnalyzeDocument_MissingDocument_ThrowsNotFound()
    {
        var (_, svc, _, _) = CreateService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.AnalyzeDocumentAsync(UserId, Guid.NewGuid()));
    }

    [Fact]
    public async Task AnalyzeDocument_MissingS3ObjectKey_Throws()
    {
        var (ctx, svc, _, _) = CreateService();
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            DocumentTypeId = PassportTypeId,
            DocumentName = "No Key",
            S3ObjectKey = "",
            ContentType = "application/pdf",
            Status = DocumentStatus.Active,
        };
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AnalyzeDocumentAsync(UserId, doc.Id));
    }

    [Fact]
    public async Task GetDocumentAnalysis_ReturnsNull_WhenNoAnalysis()
    {
        var (ctx, svc, _, _) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId);

        var result = await svc.GetDocumentAnalysisAsync(UserId, doc.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDocumentAnalysis_ReturnsCached_WhenExists()
    {
        var (ctx, svc, _, _) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId);

        var cachedDto = new DocumentAnalysisDto
        {
            DocumentId = doc.Id,
            DocumentCategory = "Insurance",
            Summary = "Health insurance policy",
            AnalyzedAt = DateTime.UtcNow,
        };
        doc.ExtractedMetadata = JsonSerializer.Serialize(cachedDto, JsonOptions);
        await ctx.SaveChangesAsync();

        var result = await svc.GetDocumentAnalysisAsync(UserId, doc.Id);

        Assert.NotNull(result);
        Assert.Equal("Insurance", result!.DocumentCategory);
    }

    [Fact]
    public async Task GetDocumentAnalysis_OtherUser_ReturnsNull()
    {
        var (ctx, svc, _, _) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId);

        doc.ExtractedMetadata = JsonSerializer.Serialize(new DocumentAnalysisDto
        {
            DocumentCategory = "Passport", Summary = "test", AnalyzedAt = DateTime.UtcNow,
        }, JsonOptions);
        await ctx.SaveChangesAsync();

        var result = await svc.GetDocumentAnalysisAsync(OtherUserId, doc.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetDocumentAnalysis_InvalidCachedJson_ReturnsNull()
    {
        var (ctx, svc, _, _) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId);

        doc.ExtractedMetadata = "corrupted json {{{";
        await ctx.SaveChangesAsync();

        var result = await svc.GetDocumentAnalysisAsync(UserId, doc.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task AnalyzeDocument_InvalidCachedJson_TriggersReanalysis()
    {
        var (ctx, svc, _, mockAi) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId);

        doc.ExtractedMetadata = "corrupted json {{{";
        await ctx.SaveChangesAsync();

        var result = await svc.AnalyzeDocumentAsync(UserId, doc.Id);

        Assert.Equal("Passport", result.DocumentCategory);
        mockAi.Verify(a => a.AnalyzeDocumentAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeDocument_PersistsResult()
    {
        var (ctx, svc, _, _) = CreateService();
        var doc = CreateActiveDocument(ctx, UserId);

        await svc.AnalyzeDocumentAsync(UserId, doc.Id);

        var updatedDoc = await ctx.Documents.FindAsync(doc.Id);
        Assert.NotNull(updatedDoc);
        Assert.False(string.IsNullOrEmpty(updatedDoc!.ExtractedMetadata));
    }
}
