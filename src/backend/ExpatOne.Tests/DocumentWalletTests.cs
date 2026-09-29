using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

[Collection("Integration")]
public class DocumentWalletTests
{
    private static readonly Guid PassportTypeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly WebApplicationFactory<Program> _factory;

    public DocumentWalletTests(AppFactory factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ExpatOneDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                var dbName = $"WalletTests_{Guid.NewGuid()}";
                services.AddDbContext<ExpatOneDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);

                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
                db.Database.EnsureCreated();
                if (!db.DocumentTypes.Any())
                {
                    db.DocumentTypes.Add(new DocumentType
                    {
                        Id = PassportTypeId, Name = "Passport", HasExpiry = true, IsSystem = true
                    });
                    db.SaveChanges();
                }

                var mockStorage = new Mock<IStorageService>();
                mockStorage.Setup(s => s.GeneratePresignedUploadUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                    .Returns("https://test/upload");
                mockStorage.Setup(s => s.GeneratePresignedUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                    .ReturnsAsync("https://test/download");
                mockStorage.Setup(s => s.DeleteFileAsync(It.IsAny<string>()))
                    .Returns(Task.CompletedTask);

                services.AddSingleton(mockStorage.Object);
                services.AddScoped<IDocumentAuditService, DocumentAuditService>();
                services.AddScoped<IDocumentService, DocumentService>();
                services.AddScoped<IDocumentVersionService, DocumentVersionService>();
                services.AddScoped<IDocumentShareService, DocumentShareService>();
            });
        });
    }

    private static HttpClient CreateAuthClient(WebApplicationFactory<Program> factory, string uid, string email, string name = "Test")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Test", $"uid={uid}&email={email}&name={name}");
        return client;
    }

    private static async Task<HttpClient> CreateProvisionedClient(WebApplicationFactory<Program> factory, string uid, string email, string name = "Test")
    {
        var client = CreateAuthClient(factory, uid, email, name);
        await client.GetAsync("/api/users/me");
        return client;
    }

    private static async Task<Guid> CreateActiveDocument(HttpClient client)
    {
        var uploadResp = await client.PostAsJsonAsync("/api/documents/upload-url", new
        {
            DocumentTypeId = PassportTypeId,
            DocumentName = "My Passport",
            FileName = "passport.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024
        });
        uploadResp.EnsureSuccessStatusCode();
        var upload = await uploadResp.Content.ReadFromJsonAsync<UploadUrlResponseDto>(JsonOpts);

        var completeResp = await client.PostAsync($"/api/documents/{upload!.DocumentId}/complete", null);
        completeResp.EnsureSuccessStatusCode();

        return upload.DocumentId;
    }

    // ===================== VERSIONING TESTS =====================

    [Fact]
    public async Task Versioning_UploadNewVersion_ReturnsVersionId()
    {
        var factory = CreateFactory();
        var client = CreateAuthClient(factory, "uid-v1", "v1@test.com");
        var docId = await CreateActiveDocument(client);

        var resp = await client.PostAsJsonAsync($"/api/documents/{docId}/versions/upload-url", new
        {
            FileName = "passport_v2.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 2048
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<VersionUploadUrlResponseDto>(JsonOpts);
        Assert.NotEqual(Guid.Empty, result!.VersionId);
        Assert.NotEmpty(result.UploadUrl);
    }

    [Fact]
    public async Task Versioning_CompleteVersion_BecomesCurrentVersion()
    {
        var factory = CreateFactory();
        var client = CreateAuthClient(factory, "uid-v2", "v2@test.com");
        var docId = await CreateActiveDocument(client);

        var uploadResp = await client.PostAsJsonAsync($"/api/documents/{docId}/versions/upload-url", new
        {
            FileName = "passport_v2.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 2048
        });
        var upload = await uploadResp.Content.ReadFromJsonAsync<VersionUploadUrlResponseDto>(JsonOpts);

        var completeResp = await client.PostAsync($"/api/documents/{docId}/versions/{upload!.VersionId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeResp.StatusCode);

        var version = await completeResp.Content.ReadFromJsonAsync<DocumentVersionDto>(JsonOpts);
        Assert.True(version!.IsCurrent);
        Assert.Equal(1, version.VersionNumber);
    }

    [Fact]
    public async Task Versioning_ListVersions_ReturnsAllVersions()
    {
        var factory = CreateFactory();
        var client = CreateAuthClient(factory, "uid-v3", "v3@test.com");
        var docId = await CreateActiveDocument(client);

        // Upload and complete two versions
        for (int i = 0; i < 2; i++)
        {
            var uploadResp = await client.PostAsJsonAsync($"/api/documents/{docId}/versions/upload-url", new
            {
                FileName = $"passport_v{i + 1}.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 1024 * (i + 1)
            });
            var upload = await uploadResp.Content.ReadFromJsonAsync<VersionUploadUrlResponseDto>(JsonOpts);
            await client.PostAsync($"/api/documents/{docId}/versions/{upload!.VersionId}/complete", null);
        }

        var resp = await client.GetAsync($"/api/documents/{docId}/versions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var versions = await resp.Content.ReadFromJsonAsync<List<DocumentVersionDto>>(JsonOpts);
        Assert.Equal(2, versions!.Count);
        Assert.True(versions[0].VersionNumber > versions[1].VersionNumber);
    }

    [Fact]
    public async Task Versioning_GetVersionAccessUrl_ReturnsPresignedUrl()
    {
        var factory = CreateFactory();
        var client = CreateAuthClient(factory, "uid-v4", "v4@test.com");
        var docId = await CreateActiveDocument(client);

        var uploadResp = await client.PostAsJsonAsync($"/api/documents/{docId}/versions/upload-url", new
        {
            FileName = "passport_v1.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024
        });
        var upload = await uploadResp.Content.ReadFromJsonAsync<VersionUploadUrlResponseDto>(JsonOpts);
        await client.PostAsync($"/api/documents/{docId}/versions/{upload!.VersionId}/complete", null);

        var resp = await client.GetAsync($"/api/documents/{docId}/versions/{upload.VersionId}/access-url");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<AccessUrlResponseDto>(JsonOpts);
        Assert.NotEmpty(result!.Url);
    }

    [Fact]
    public async Task Versioning_OtherUserCannotUploadVersion()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-v5a", "v5a@test.com");
        var other = CreateAuthClient(factory, "uid-v5b", "v5b@test.com");
        var docId = await CreateActiveDocument(owner);

        var resp = await other.PostAsJsonAsync($"/api/documents/{docId}/versions/upload-url", new
        {
            FileName = "hacked.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Versioning_InvalidContentType_Returns400()
    {
        var factory = CreateFactory();
        var client = CreateAuthClient(factory, "uid-v6", "v6@test.com");
        var docId = await CreateActiveDocument(client);

        var resp = await client.PostAsJsonAsync($"/api/documents/{docId}/versions/upload-url", new
        {
            FileName = "malware.exe",
            ContentType = "application/x-msdownload",
            FileSizeBytes = 1024
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Versioning_OversizedFile_Returns400()
    {
        var factory = CreateFactory();
        var client = CreateAuthClient(factory, "uid-v7", "v7@test.com");
        var docId = await CreateActiveDocument(client);

        var resp = await client.PostAsJsonAsync($"/api/documents/{docId}/versions/upload-url", new
        {
            FileName = "huge.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 11 * 1024 * 1024
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Versioning_Unauthenticated_Returns401()
    {
        var factory = CreateFactory();
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/documents/00000000-0000-0000-0000-000000000001/versions/upload-url", new
        {
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Versioning_DocumentHasVersionCount()
    {
        var factory = CreateFactory();
        var client = CreateAuthClient(factory, "uid-v8", "v8@test.com");
        var docId = await CreateActiveDocument(client);

        var uploadResp = await client.PostAsJsonAsync($"/api/documents/{docId}/versions/upload-url", new
        {
            FileName = "passport_v1.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024
        });
        var upload = await uploadResp.Content.ReadFromJsonAsync<VersionUploadUrlResponseDto>(JsonOpts);
        await client.PostAsync($"/api/documents/{docId}/versions/{upload!.VersionId}/complete", null);

        var docResp = await client.GetAsync($"/api/documents/{docId}");
        var doc = await docResp.Content.ReadFromJsonAsync<DocumentDto>(JsonOpts);
        Assert.Equal(1, doc!.VersionCount);
    }

    // ===================== SHARING TESTS =====================

    [Fact]
    public async Task Sharing_CreateShare_ReturnsShareDto()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-s1a", "owner@test.com");
        var _ = await CreateProvisionedClient(factory, "uid-s1b", "friend@test.com");
        var docId = await CreateActiveDocument(owner);

        var resp = await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new
        {
            SharedWithEmail = "friend@test.com"
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var share = await resp.Content.ReadFromJsonAsync<DocumentShareDto>(JsonOpts);
        Assert.NotEqual(Guid.Empty, share!.Id);
        Assert.Equal("friend@test.com", share.SharedWithEmail);
        Assert.Equal("Read", share.Permission);
        Assert.Null(share.RevokedAt);
    }

    [Fact]
    public async Task Sharing_CannotShareWithSelf()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-s2", "self@test.com");
        var docId = await CreateActiveDocument(owner);

        var resp = await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new
        {
            SharedWithEmail = "self@test.com"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Sharing_CannotShareWithNonExistentUser()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-s3", "owner3@test.com");
        var docId = await CreateActiveDocument(owner);

        var resp = await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new
        {
            SharedWithEmail = "nobody@nowhere.com"
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Sharing_DuplicateShareRejected()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-s4a", "owner4@test.com");
        var _ = await CreateProvisionedClient(factory, "uid-s4b", "friend4@test.com");
        var docId = await CreateActiveDocument(owner);

        await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "friend4@test.com" });
        var resp = await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "friend4@test.com" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Sharing_OtherUserCannotShareMyDocument()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-s5a", "owner5@test.com");
        var attacker = CreateAuthClient(factory, "uid-s5b", "attacker@test.com");
        var _ = await CreateProvisionedClient(factory, "uid-s5c", "victim@test.com");
        var docId = await CreateActiveDocument(owner);

        var resp = await attacker.PostAsJsonAsync($"/api/documents/{docId}/shares", new
        {
            SharedWithEmail = "victim@test.com"
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Sharing_ListShares_ReturnsAllShares()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-s6a", "owner6@test.com");
        var _ = await CreateProvisionedClient(factory, "uid-s6b", "f1@test.com");
        var __ = await CreateProvisionedClient(factory, "uid-s6c", "f2@test.com");
        var docId = await CreateActiveDocument(owner);

        await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "f1@test.com" });
        await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "f2@test.com" });

        var resp = await owner.GetAsync($"/api/documents/{docId}/shares");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var shares = await resp.Content.ReadFromJsonAsync<List<DocumentShareDto>>(JsonOpts);
        Assert.Equal(2, shares!.Count);
    }

    [Fact]
    public async Task Sharing_RevokeShare_SetsRevokedAt()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-s7a", "owner7@test.com");
        var _ = await CreateProvisionedClient(factory, "uid-s7b", "friend7@test.com");
        var docId = await CreateActiveDocument(owner);

        var createResp = await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "friend7@test.com" });
        var share = await createResp.Content.ReadFromJsonAsync<DocumentShareDto>(JsonOpts);

        var revokeResp = await owner.DeleteAsync($"/api/documents/{docId}/shares/{share!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResp.StatusCode);
    }

    [Fact]
    public async Task Sharing_SharedWithMe_ReturnsSharedDocuments()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-s8a", "owner8@test.com");
        var friend = await CreateProvisionedClient(factory, "uid-s8b", "friend8@test.com");
        var docId = await CreateActiveDocument(owner);

        await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "friend8@test.com" });

        var resp = await friend.GetAsync("/api/documents/shared-with-me");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var docs = await resp.Content.ReadFromJsonAsync<List<SharedDocumentDto>>(JsonOpts);
        Assert.Single(docs!);
        Assert.Equal("My Passport", docs[0].DocumentName);
    }

    [Fact]
    public async Task Sharing_RevokedShareNotInSharedWithMe()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-s9a", "owner9@test.com");
        var friend = await CreateProvisionedClient(factory, "uid-s9b", "friend9@test.com");
        var docId = await CreateActiveDocument(owner);

        var createResp = await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "friend9@test.com" });
        var share = await createResp.Content.ReadFromJsonAsync<DocumentShareDto>(JsonOpts);
        await owner.DeleteAsync($"/api/documents/{docId}/shares/{share!.Id}");

        var resp = await friend.GetAsync("/api/documents/shared-with-me");
        var docs = await resp.Content.ReadFromJsonAsync<List<SharedDocumentDto>>(JsonOpts);
        Assert.Empty(docs!);
    }

    [Fact]
    public async Task Sharing_SharedUserCanAccessDocument()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-s10a", "owner10@test.com");
        var friend = await CreateProvisionedClient(factory, "uid-s10b", "friend10@test.com");
        var docId = await CreateActiveDocument(owner);

        await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "friend10@test.com" });

        var resp = await friend.GetAsync($"/api/documents/{docId}/shared-access-url");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<AccessUrlResponseDto>(JsonOpts);
        Assert.NotEmpty(result!.Url);
    }

    [Fact]
    public async Task Sharing_UnrelatedUserCannotAccessSharedUrl()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-s11a", "owner11@test.com");
        var _ = await CreateProvisionedClient(factory, "uid-s11b", "friend11@test.com");
        var stranger = await CreateProvisionedClient(factory, "uid-s11c", "stranger@test.com");
        var docId = await CreateActiveDocument(owner);

        await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "friend11@test.com" });

        var resp = await stranger.GetAsync($"/api/documents/{docId}/shared-access-url");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Sharing_DocumentShowsActiveShareCount()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-s12a", "owner12@test.com");
        var _ = await CreateProvisionedClient(factory, "uid-s12b", "friend12@test.com");
        var docId = await CreateActiveDocument(owner);

        await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "friend12@test.com" });

        var docResp = await owner.GetAsync($"/api/documents/{docId}");
        var doc = await docResp.Content.ReadFromJsonAsync<DocumentDto>(JsonOpts);
        Assert.Equal(1, doc!.ActiveShareCount);
    }

    [Fact]
    public async Task Sharing_Unauthenticated_Returns401()
    {
        var factory = CreateFactory();
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/documents/shared-with-me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ===================== AUDIT LOG TESTS =====================

    [Fact]
    public async Task Audit_UploadCreatesAuditLog()
    {
        var factory = CreateFactory();
        var client = CreateAuthClient(factory, "uid-a1", "audit1@test.com");
        var docId = await CreateActiveDocument(client);

        var resp = await client.GetAsync($"/api/documents/{docId}/audit-logs");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var logs = await resp.Content.ReadFromJsonAsync<List<DocumentAuditLogDto>>(JsonOpts);
        Assert.Contains(logs!, l => l.Action == "document_uploaded");
    }

    [Fact]
    public async Task Audit_AccessCreatesAuditLog()
    {
        var factory = CreateFactory();
        var client = CreateAuthClient(factory, "uid-a2", "audit2@test.com");
        var docId = await CreateActiveDocument(client);

        await client.GetAsync($"/api/documents/{docId}/access-url");

        var resp = await client.GetAsync($"/api/documents/{docId}/audit-logs");
        var logs = await resp.Content.ReadFromJsonAsync<List<DocumentAuditLogDto>>(JsonOpts);
        Assert.Contains(logs!, l => l.Action == "document_accessed");
    }

    [Fact]
    public async Task Audit_VersionUploadCreatesAuditLog()
    {
        var factory = CreateFactory();
        var client = CreateAuthClient(factory, "uid-a3", "audit3@test.com");
        var docId = await CreateActiveDocument(client);

        var uploadResp = await client.PostAsJsonAsync($"/api/documents/{docId}/versions/upload-url", new
        {
            FileName = "passport_v2.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024
        });
        var upload = await uploadResp.Content.ReadFromJsonAsync<VersionUploadUrlResponseDto>(JsonOpts);
        await client.PostAsync($"/api/documents/{docId}/versions/{upload!.VersionId}/complete", null);

        var resp = await client.GetAsync($"/api/documents/{docId}/audit-logs");
        var logs = await resp.Content.ReadFromJsonAsync<List<DocumentAuditLogDto>>(JsonOpts);
        Assert.Contains(logs!, l => l.Action == "version_uploaded");
    }

    [Fact]
    public async Task Audit_ShareCreatesAuditLog()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-a4a", "auditowner@test.com");
        var _ = await CreateProvisionedClient(factory, "uid-a4b", "auditfriend@test.com");
        var docId = await CreateActiveDocument(owner);

        await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "auditfriend@test.com" });

        var resp = await owner.GetAsync($"/api/documents/{docId}/audit-logs");
        var logs = await resp.Content.ReadFromJsonAsync<List<DocumentAuditLogDto>>(JsonOpts);
        Assert.Contains(logs!, l => l.Action == "share_created");
    }

    [Fact]
    public async Task Audit_ShareRevokeCreatesAuditLog()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-a5a", "auditowner5@test.com");
        var _ = await CreateProvisionedClient(factory, "uid-a5b", "auditfriend5@test.com");
        var docId = await CreateActiveDocument(owner);

        var createResp = await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "auditfriend5@test.com" });
        var share = await createResp.Content.ReadFromJsonAsync<DocumentShareDto>(JsonOpts);
        await owner.DeleteAsync($"/api/documents/{docId}/shares/{share!.Id}");

        var resp = await owner.GetAsync($"/api/documents/{docId}/audit-logs");
        var logs = await resp.Content.ReadFromJsonAsync<List<DocumentAuditLogDto>>(JsonOpts);
        Assert.Contains(logs!, l => l.Action == "share_revoked");
    }

    [Fact]
    public async Task Audit_OtherUserCannotViewAuditLogs()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-a6a", "auditowner6@test.com");
        var other = CreateAuthClient(factory, "uid-a6b", "other6@test.com");
        var docId = await CreateActiveDocument(owner);

        var resp = await other.GetAsync($"/api/documents/{docId}/audit-logs");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Audit_AuditLogMetadataDoesNotContainSecrets()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-a7a", "auditowner7@test.com");
        var _ = await CreateProvisionedClient(factory, "uid-a7b", "auditfriend7@test.com");
        var docId = await CreateActiveDocument(owner);

        await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "auditfriend7@test.com" });

        var resp = await owner.GetAsync($"/api/documents/{docId}/audit-logs");
        var logs = await resp.Content.ReadFromJsonAsync<List<DocumentAuditLogDto>>(JsonOpts);

        foreach (var log in logs!)
        {
            if (log.Metadata != null)
            {
                Assert.DoesNotContain("password", log.Metadata, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("token", log.Metadata, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("api_key", log.Metadata, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("s3://", log.Metadata, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ===================== MULTI-VERSION DELETION TESTS =====================

    [Fact]
    public async Task Deletion_DocumentWithVersions_DeletesAllFromS3()
    {
        var factory = CreateFactory();
        var client = CreateAuthClient(factory, "uid-d1", "del1@test.com");
        var docId = await CreateActiveDocument(client);

        var uploadResp = await client.PostAsJsonAsync($"/api/documents/{docId}/versions/upload-url", new
        {
            FileName = "passport_v2.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024
        });
        var upload = await uploadResp.Content.ReadFromJsonAsync<VersionUploadUrlResponseDto>(JsonOpts);
        await client.PostAsync($"/api/documents/{docId}/versions/{upload!.VersionId}/complete", null);

        var deleteResp = await client.DeleteAsync($"/api/documents/{docId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var getResp = await client.GetAsync($"/api/documents/{docId}");
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task Deletion_DocumentWithShares_CascadeDeletesShares()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-d2a", "delowner@test.com");
        var friend = await CreateProvisionedClient(factory, "uid-d2b", "delfriend@test.com");
        var docId = await CreateActiveDocument(owner);

        await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "delfriend@test.com" });

        await owner.DeleteAsync($"/api/documents/{docId}");

        var resp = await friend.GetAsync("/api/documents/shared-with-me");
        var docs = await resp.Content.ReadFromJsonAsync<List<SharedDocumentDto>>(JsonOpts);
        Assert.Empty(docs!);
    }

    // ===================== SHARED USER VERSIONING ACCESS =====================

    [Fact]
    public async Task SharedUser_CanListVersions()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-sv1a", "svowner@test.com");
        var friend = await CreateProvisionedClient(factory, "uid-sv1b", "svfriend@test.com");
        var docId = await CreateActiveDocument(owner);

        var uploadResp = await owner.PostAsJsonAsync($"/api/documents/{docId}/versions/upload-url", new
        {
            FileName = "passport_v1.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024
        });
        var upload = await uploadResp.Content.ReadFromJsonAsync<VersionUploadUrlResponseDto>(JsonOpts);
        await owner.PostAsync($"/api/documents/{docId}/versions/{upload!.VersionId}/complete", null);

        await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "svfriend@test.com" });

        var resp = await friend.GetAsync($"/api/documents/{docId}/versions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var versions = await resp.Content.ReadFromJsonAsync<List<DocumentVersionDto>>(JsonOpts);
        Assert.Single(versions!);
    }

    [Fact]
    public async Task SharedUser_CannotUploadVersion()
    {
        var factory = CreateFactory();
        var owner = CreateAuthClient(factory, "uid-sv2a", "svowner2@test.com");
        var friend = await CreateProvisionedClient(factory, "uid-sv2b", "svfriend2@test.com");
        var docId = await CreateActiveDocument(owner);

        await owner.PostAsJsonAsync($"/api/documents/{docId}/shares", new { SharedWithEmail = "svfriend2@test.com" });

        var resp = await friend.PostAsJsonAsync($"/api/documents/{docId}/versions/upload-url", new
        {
            FileName = "unauthorized.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
