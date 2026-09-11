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

public class DocumentServiceTests
{
    private static readonly Guid PassportTypeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private (ExpatOneDbContext context, DocumentService service) CreateService()
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
        mockStorage.Setup(s => s.GeneratePresignedUploadUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Returns("https://s3.example.com/upload");
        mockStorage.Setup(s => s.GeneratePresignedUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ReturnsAsync("https://s3.example.com/download");
        mockStorage.Setup(s => s.DeleteFileAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<DocumentService>>().Object;
        var service = new DocumentService(context, mockStorage.Object, logger);

        return (context, service);
    }

    private RequestUploadDto ValidUploadDto() => new()
    {
        DocumentTypeId = PassportTypeId,
        DocumentName = "My Passport",
        FileName = "passport.pdf",
        ContentType = "application/pdf",
        FileSizeBytes = 500_000,
    };

    [Fact]
    public async Task RequestUpload_CreatesDocumentAndReturnsUrl()
    {
        var (ctx, svc) = CreateService();

        var result = await svc.RequestUploadAsync(UserId, ValidUploadDto());

        Assert.NotEqual(Guid.Empty, result.DocumentId);
        Assert.Contains("upload", result.UploadUrl);
        Assert.Contains(UserId.ToString(), result.ObjectKey);

        var doc = await ctx.Documents.FindAsync(result.DocumentId);
        Assert.NotNull(doc);
        Assert.Equal(DocumentStatus.PendingUpload, doc.Status);
    }

    [Fact]
    public async Task CompleteUpload_ActivatesDocument()
    {
        var (_, svc) = CreateService();

        var upload = await svc.RequestUploadAsync(UserId, ValidUploadDto());
        var doc = await svc.CompleteUploadAsync(UserId, upload.DocumentId);

        Assert.Equal("Active", doc.Status);
        Assert.Equal("My Passport", doc.Name);
    }

    [Fact]
    public async Task GetUserDocuments_ReturnsOnlyOwnActiveDocuments()
    {
        var (_, svc) = CreateService();

        var u1 = await svc.RequestUploadAsync(UserId, ValidUploadDto());
        await svc.CompleteUploadAsync(UserId, u1.DocumentId);

        // Pending document should NOT appear in list
        var pending = ValidUploadDto();
        pending.DocumentName = "Pending";
        await svc.RequestUploadAsync(UserId, pending);

        var docs = await svc.GetUserDocumentsAsync(UserId);
        Assert.Single(docs);
        Assert.Equal("My Passport", docs[0].Name);
    }

    [Fact]
    public async Task GetDocument_OwnerCanAccess()
    {
        var (_, svc) = CreateService();

        var upload = await svc.RequestUploadAsync(UserId, ValidUploadDto());
        await svc.CompleteUploadAsync(UserId, upload.DocumentId);

        var doc = await svc.GetDocumentAsync(UserId, upload.DocumentId);
        Assert.NotNull(doc);
    }

    [Fact]
    public async Task GetDocument_OtherUserCannotAccess()
    {
        var (_, svc) = CreateService();

        var upload = await svc.RequestUploadAsync(UserId, ValidUploadDto());
        await svc.CompleteUploadAsync(UserId, upload.DocumentId);

        var doc = await svc.GetDocumentAsync(OtherUserId, upload.DocumentId);
        Assert.Null(doc);
    }

    [Fact]
    public async Task DeleteDocument_OwnerCanDelete()
    {
        var (ctx, svc) = CreateService();

        var upload = await svc.RequestUploadAsync(UserId, ValidUploadDto());
        await svc.CompleteUploadAsync(UserId, upload.DocumentId);
        await svc.DeleteDocumentAsync(UserId, upload.DocumentId);

        Assert.Null(await ctx.Documents.FindAsync(upload.DocumentId));
    }

    [Fact]
    public async Task DeleteDocument_OtherUserCannotDelete()
    {
        var (_, svc) = CreateService();

        var upload = await svc.RequestUploadAsync(UserId, ValidUploadDto());
        await svc.CompleteUploadAsync(UserId, upload.DocumentId);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.DeleteDocumentAsync(OtherUserId, upload.DocumentId));
    }

    [Fact]
    public async Task GetAccessUrl_OwnerGetsUrl()
    {
        var (_, svc) = CreateService();

        var upload = await svc.RequestUploadAsync(UserId, ValidUploadDto());
        await svc.CompleteUploadAsync(UserId, upload.DocumentId);

        var access = await svc.GetAccessUrlAsync(UserId, upload.DocumentId);
        Assert.Contains("download", access.Url);
        Assert.Equal(300, access.ExpiresInSeconds);
    }

    [Fact]
    public async Task GetAccessUrl_OtherUserRejected()
    {
        var (_, svc) = CreateService();

        var upload = await svc.RequestUploadAsync(UserId, ValidUploadDto());
        await svc.CompleteUploadAsync(UserId, upload.DocumentId);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.GetAccessUrlAsync(OtherUserId, upload.DocumentId));
    }

    [Fact]
    public async Task RequestUpload_RejectsUnsupportedContentType()
    {
        var (_, svc) = CreateService();

        var dto = ValidUploadDto();
        dto.ContentType = "application/zip";

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RequestUploadAsync(UserId, dto));
    }

    [Fact]
    public async Task RequestUpload_RejectsOversizedFile()
    {
        var (_, svc) = CreateService();

        var dto = ValidUploadDto();
        dto.FileSizeBytes = 20 * 1024 * 1024;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RequestUploadAsync(UserId, dto));
    }

    [Fact]
    public async Task RequestUpload_RejectsZeroByteFile()
    {
        var (_, svc) = CreateService();

        var dto = ValidUploadDto();
        dto.FileSizeBytes = 0;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RequestUploadAsync(UserId, dto));
    }

    [Fact]
    public async Task RequestUpload_RejectsEmptyDocumentName()
    {
        var (_, svc) = CreateService();

        var dto = ValidUploadDto();
        dto.DocumentName = "";

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RequestUploadAsync(UserId, dto));
    }

    [Fact]
    public async Task RequestUpload_SanitizesPathTraversal()
    {
        var (ctx, svc) = CreateService();

        var dto = ValidUploadDto();
        dto.FileName = "../../etc/passwd";
        var result = await svc.RequestUploadAsync(UserId, dto);

        var doc = await ctx.Documents.FindAsync(result.DocumentId);
        Assert.DoesNotContain("..", doc!.S3ObjectKey);
    }

    [Fact]
    public async Task GetDocumentTypes_ReturnsAll()
    {
        var (_, svc) = CreateService();

        var types = await svc.GetDocumentTypesAsync();
        Assert.Single(types); // only Passport seeded in test
        Assert.Equal("Passport", types[0].Name);
    }

    [Fact]
    public async Task CompleteUpload_OtherUserCannotComplete()
    {
        var (_, svc) = CreateService();

        var upload = await svc.RequestUploadAsync(UserId, ValidUploadDto());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.CompleteUploadAsync(OtherUserId, upload.DocumentId));
    }
}
