using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
namespace ExpatOne.Tests;

public class TemporaryFamilySharingTests : IDisposable
{
    private readonly ExpatOneDbContext db;
    private readonly FamilyService family;
    private readonly DocumentService documents;
    private readonly Mock<IStorageService> storage = new();
    private readonly Guid owner = Guid.NewGuid(), viewer = Guid.NewGuid(), docId = Guid.NewGuid();
    public TemporaryFamilySharingTests()
    {
        db = new ExpatOneDbContext(new DbContextOptionsBuilder<ExpatOneDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection(); db.Database.EnsureCreated();
        db.Users.AddRange(new User { Id = owner, Email = "owner@test.com", ExternalId = "owner" },
            new User { Id = viewer, Email = "viewer@test.com", ExternalId = "viewer" });
        var type = new DocumentType { Id = Guid.NewGuid(), Name = "Passport", HasExpiry = true };
        db.DocumentTypes.Add(type);
        db.Documents.Add(new Document { Id = docId, UserId = owner, DocumentTypeId = type.Id,
            DocumentName = "Passport", S3ObjectKey = "private/passport", ExpiryDate = DateTime.UtcNow.Date.AddDays(20) });
        db.SaveChanges(); family = new FamilyService(db);
        storage.Setup(s => s.GeneratePresignedUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>())).ReturnsAsync("https://test/permanent");
        storage.Setup(s => s.GeneratePresignedUrlUntilAsync(It.IsAny<string>(), It.IsAny<DateTime>())).ReturnsAsync("https://test/temporary");
        documents = new DocumentService(db, storage.Object, new DocumentAuditService(db), NullLogger<DocumentService>.Instance);
    }
    public void Dispose() => db.Dispose();
    private async Task<FamilyRequestDto> Share(DateTimeOffset? deadline)
    {
        var request = await family.SendAsync(viewer, "owner@test.com");
        return await family.RespondAsync(owner, request.Id, new FamilyDecisionDto {
            Revision = request.Revision, Accept = true, DocumentIds = [docId],
            AllowExpiryReminders = true, AccessExpiresAt = deadline });
    }
    [Fact]
    public async Task ExistingPermanentSharingStillWorks()
    {
        var r = await Share(null);
        Assert.Null(r.AccessExpiresAt); Assert.False(r.IsExpired);
        Assert.NotNull(await documents.GetDocumentAsync(viewer, docId));
        var url = await documents.GetAccessUrlAsync(viewer, docId);
        Assert.Equal(300, url.ExpiresInSeconds);
        storage.Verify(s => s.GeneratePresignedUrlUntilAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }
    [Fact]
    public async Task ExpiryDeniesListDetailDownloadDashboardAndRemindersButKeepsOwnerAccess()
    {
        var r = await Share(DateTimeOffset.UtcNow.AddHours(1));
        var row = Assert.Single(await family.DashboardAsync(owner));
        await family.ConfirmExpiryAsync(owner, docId, new ConfirmFamilyExpiryDto { Fingerprint = row.Fingerprint });
        Assert.Contains(await family.AlertsAsync(viewer), a => a.Kind == "expiry");
        (await db.FamilyRequests.FindAsync(r.Id))!.AccessExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        Assert.Empty(await documents.GetUserDocumentsAsync(viewer));
        Assert.Null(await documents.GetDocumentAsync(viewer, docId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => documents.GetAccessUrlAsync(viewer, docId));
        Assert.Empty(await family.DashboardAsync(viewer));
        var alerts = await family.AlertsAsync(viewer);
        Assert.DoesNotContain(alerts, a => a.Kind == "expiry");
        Assert.Contains(alerts, a => a.Title == "Family request: Expired");
        Assert.True(Assert.Single(await family.ListAsync(viewer)).IsExpired);
        Assert.NotNull(await documents.GetDocumentAsync(owner, docId));
    }
    [Fact]
    public async Task ExactDeadlineIsExclusiveForDashboardAndAlerts()
    {
        var deadline = DateTime.UtcNow.AddHours(1);
        await Share(deadline);
        Assert.Single(await family.DashboardAtAsync(viewer, deadline.AddTicks(-1)));
        Assert.Empty(await family.DashboardAtAsync(viewer, deadline));
        Assert.Contains(await family.AlertsAtAsync(viewer, deadline), a => a.Title == "Family request: Expired");
    }
    [Fact]
    public async Task OnlyOwnerCanSetExpiryAndOldClientsCannotAccidentallyClearIt()
    {
        var r = await Share(DateTimeOffset.UtcNow.AddDays(2));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => family.UpdateGrantsAsync(viewer, r.Id,
            new FamilyGrantsDto { Revision = r.Revision, DocumentIds = [docId], UpdateAccessExpiry = true }));
        var updated = await family.UpdateGrantsAsync(owner, r.Id,
            new FamilyGrantsDto { Revision = r.Revision, DocumentIds = [docId] });
        Assert.Equal(r.AccessExpiresAt, updated.AccessExpiresAt);
        updated = await family.UpdateGrantsAsync(owner, r.Id,
            new FamilyGrantsDto { Revision = updated.Revision, DocumentIds = [docId], UpdateAccessExpiry = true });
        Assert.Null(updated.AccessExpiresAt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => family.UpdateGrantsAsync(owner, r.Id,
            new FamilyGrantsDto { Revision = r.Revision, DocumentIds = [docId], AccessExpiresAt = DateTimeOffset.UtcNow.AddHours(1) }));
    }
    [Fact]
    public async Task OwnerCanRenewExpiredAccessAndRevokeBeforeDeadline()
    {
        var r = await Share(DateTimeOffset.UtcNow.AddHours(1));
        (await db.FamilyRequests.FindAsync(r.Id))!.AccessExpiresAt = DateTime.UtcNow.AddDays(-1); await db.SaveChangesAsync();
        Assert.Null(await documents.GetDocumentAsync(viewer, docId));
        r = await family.UpdateGrantsAsync(owner, r.Id, new FamilyGrantsDto { Revision = r.Revision,
            DocumentIds = [docId], UpdateAccessExpiry = true, AccessExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
        Assert.False(r.IsExpired); Assert.NotNull(await documents.GetDocumentAsync(viewer, docId));
        await family.EndAsync(owner, r.Id, r.Revision);
        Assert.Null(await documents.GetDocumentAsync(viewer, docId));
    }
    [Fact]
    public async Task PastDeadlineRejectedAndTimezoneOffsetsNormalized()
    {
        var pending = await family.SendAsync(viewer, "owner@test.com");
        await Assert.ThrowsAsync<ArgumentException>(() => family.RespondAsync(owner, pending.Id,
            new FamilyDecisionDto { Revision = pending.Revision, Accept = true, DocumentIds = [docId], AccessExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) }));
        Assert.Empty(await db.FamilyDocumentGrants.ToListAsync());
        var local = DateTimeOffset.UtcNow.AddDays(1).ToOffset(TimeSpan.FromHours(8));
        var r = await family.RespondAsync(owner, pending.Id, new FamilyDecisionDto {
            Revision = pending.Revision, Accept = true, DocumentIds = [docId], AccessExpiresAt = local });
        Assert.Equal(local.UtcDateTime, r.AccessExpiresAt);
        await Assert.ThrowsAsync<ArgumentException>(() => family.UpdateGrantsAsync(owner, r.Id,
            new FamilyGrantsDto { Revision = r.Revision, DocumentIds = [docId], AccessExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) }));
    }
    [Fact]
    public async Task SignedUrlUsesAbsoluteDeadlineAndNeverExceedsFiveMinutes()
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        var r = await Share(deadline);
        var response = await documents.GetAccessUrlAsync(viewer, docId);
        Assert.InRange(response.ExpiresInSeconds, 1, 90);
        storage.Verify(s => s.GeneratePresignedUrlUntilAsync("private/passport", deadline), Times.Once);
        await family.UpdateGrantsAsync(owner, r.Id, new FamilyGrantsDto { Revision = r.Revision,
            DocumentIds = [docId], AccessExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
        var before = DateTime.UtcNow;
        await documents.GetAccessUrlAsync(viewer, docId);
        var after = DateTime.UtcNow;
        storage.Verify(s => s.GeneratePresignedUrlUntilAsync("private/passport",
            It.Is<DateTime>(d => d >= before.AddMinutes(5) && d <= after.AddMinutes(5))), Times.Once);
    }
}
