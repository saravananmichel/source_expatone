using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ExpatOne.Tests;

public class FamilyServiceTests : IDisposable
{
    private readonly ExpatOneDbContext db;
    private readonly FamilyService family;
    private readonly DocumentService documents;
    private readonly Guid requester = Guid.NewGuid(), owner = Guid.NewGuid(), stranger = Guid.NewGuid();
    private readonly Guid first = Guid.NewGuid(), second = Guid.NewGuid(), own = Guid.NewGuid(), pending = Guid.NewGuid();
    public FamilyServiceTests()
    {
        // Relational constraints and translated access queries, not only an in-memory LINQ mock.
        db = new ExpatOneDbContext(new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection(); db.Database.EnsureCreated();
        foreach (var (id, email) in new[] { (requester, "requester@test.com"), (owner, "owner@test.com"), (stranger, "stranger@test.com") })
            db.Users.Add(new User { Id = id, ExternalId = id.ToString(), Email = email });
        var type = new DocumentType { Id = Guid.NewGuid(), Name = "Family test", HasExpiry = false };
        db.DocumentTypes.Add(type);
        foreach (var (id, user) in new[] { (first, owner), (second, owner), (own, requester), (pending, owner) })
            db.Documents.Add(new Document { Id = id, UserId = user, DocumentTypeId = type.Id,
                DocumentName = id.ToString(), S3ObjectKey = $"users/{user}/{id}",
                Status = id == pending ? DocumentStatus.PendingUpload : DocumentStatus.Active });
        db.SaveChanges();
        family = new FamilyService(db);
        var storage = new Mock<IStorageService>();
        storage.Setup(s => s.GeneratePresignedUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).ReturnsAsync("https://example.test/file");
        documents = new DocumentService(db, storage.Object, new DocumentAuditService(db), NullLogger<DocumentService>.Instance);
    }
    public void Dispose() => db.Dispose();
    private Task<FamilyRequestDto> Send() => family.SendAsync(requester, " OWNER@test.com ");
    private Task<FamilyRequestDto> Accept(FamilyRequestDto r, params Guid[] ids) => family.RespondAsync(owner, r.Id,
        new FamilyDecisionDto { Revision = r.Revision, Accept = true, DocumentIds = ids.ToList() });

    [Fact]
    public async Task SelectedDocumentsAreMergedAndCanBeOpenedButNotDeleted()
    {
        var r = await Send();
        Assert.Single(await documents.GetUserDocumentsAsync(requester));
        Assert.Null(await documents.GetDocumentAsync(requester, first));
        var accepted = await Accept(r, first);
        Assert.Equal("Accepted", accepted.Status);
        var list = await documents.GetUserDocumentsAsync(requester);
        Assert.Equal(2, list.Count);
        Assert.True(list.Single(d => d.Id == first).IsSharedWithMe);
        Assert.False(list.Single(d => d.Id == own).IsSharedWithMe);
        Assert.NotNull(await documents.GetDocumentAsync(requester, first));
        Assert.NotNull(await documents.GetAccessUrlAsync(requester, first));
        Assert.Null(await documents.GetDocumentAsync(requester, second));
        Assert.Null(await documents.GetDocumentAsync(stranger, first));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => documents.GetAccessUrlAsync(requester, second));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => documents.DeleteDocumentAsync(requester, first));
    }
    [Fact]
    public async Task OnlyRecipientCanRespondAndOnlyOwnedCompletedDocumentsCanBeGranted()
    {
        var r = await Send();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => family.RespondAsync(requester, r.Id,
            new FamilyDecisionDto { Revision = r.Revision, Accept = true, DocumentIds = [first] }));
        await Assert.ThrowsAsync<ArgumentException>(() => Accept(r, own));
        await Assert.ThrowsAsync<ArgumentException>(() => Accept(r, pending));
        await Assert.ThrowsAsync<ArgumentException>(() => Accept(r, Guid.NewGuid()));
        Assert.Empty(await db.FamilyDocumentGrants.ToListAsync());
    }
    [Fact]
    public async Task AllSelectionDoesNotAutomaticallyShareFutureUploadsAndCanBeReduced()
    {
        var r = await Accept(await Send(), first, second);
        var future = new Document { Id = Guid.NewGuid(), UserId = owner,
            DocumentTypeId = (await db.Documents.FindAsync(first))!.DocumentTypeId,
            DocumentName = "New private upload", S3ObjectKey = "private-new" };
        db.Documents.Add(future); await db.SaveChangesAsync();
        Assert.Null(await documents.GetDocumentAsync(requester, future.Id));
        r = await family.UpdateGrantsAsync(owner, r.Id, new FamilyGrantsDto { Revision = r.Revision, DocumentIds = [second] });
        Assert.Null(await documents.GetDocumentAsync(requester, first));
        Assert.NotNull(await documents.GetDocumentAsync(requester, second));
        r = await family.EndAsync(owner, r.Id, r.Revision);
        Assert.Equal("Revoked", r.Status);
        Assert.Single(await documents.GetUserDocumentsAsync(requester));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => documents.GetAccessUrlAsync(requester, second));
    }
    [Fact]
    public async Task DuplicateSelfAndStaleRequestsAreRejectedAndHistoryIsRetained()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => family.SendAsync(owner, "owner@test.com"));
        var r = await Send();
        await Assert.ThrowsAsync<InvalidOperationException>(Send);
        var accepted = await Accept(r, first);
        await Assert.ThrowsAsync<InvalidOperationException>(() => family.EndAsync(owner, r.Id, r.Revision));
        await family.EndAsync(owner, accepted.Id, accepted.Revision);
        await Send();
        Assert.Equal(2, (await family.ListAsync(owner)).Count);
    }
    [Fact]
    public async Task RejectionCancellationAndEmptyAcceptanceGrantNoDocuments()
    {
        var r = await Send();
        var rejected = await family.RespondAsync(owner, r.Id, new FamilyDecisionDto { Revision = r.Revision, Accept = false });
        Assert.Equal("Rejected", rejected.Status);
        r = await Send();
        Assert.Equal("Cancelled", (await family.EndAsync(requester, r.Id, r.Revision)).Status);
        r = await Accept(await Send());
        Assert.Equal("Accepted", r.Status);
        Assert.Single(await documents.GetUserDocumentsAsync(requester));
    }
    [Fact]
    public async Task ThirdPartyCannotSeeOrChangeRequestAndRequesterCannotChangeGrants()
    {
        var r = await Accept(await Send(), first);
        Assert.Empty(await family.ListAsync(stranger));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => family.EndAsync(stranger, r.Id, r.Revision));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => family.UpdateGrantsAsync(requester, r.Id,
            new FamilyGrantsDto { Revision = r.Revision, DocumentIds = [second] }));
    }
}
