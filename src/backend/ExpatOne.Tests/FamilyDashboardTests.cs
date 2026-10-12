using ExpatOne.Application.DTOs;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Tests;

public class FamilyDashboardTests : IDisposable
{
    private readonly ExpatOneDbContext db;
    private readonly FamilyService family;
    private readonly Guid owner = Guid.NewGuid(), viewer = Guid.NewGuid(), stranger = Guid.NewGuid();
    private readonly Guid document = Guid.NewGuid(), privateDocument = Guid.NewGuid(), pending = Guid.NewGuid();
    private readonly DateTime today = DateTime.UtcNow.Date;
    public FamilyDashboardTests()
    {
        db = new ExpatOneDbContext(new DbContextOptionsBuilder<ExpatOneDbContext>().UseSqlite("Data Source=:memory:").Options);
        db.Database.OpenConnection(); db.Database.EnsureCreated();
        foreach (var (id, email) in new[] { (owner, "owner@test.com"), (viewer, "viewer@test.com"), (stranger, "stranger@test.com") })
            db.Users.Add(new User { Id = id, Email = email, ExternalId = id.ToString() });
        var type = new DocumentType { Id = Guid.NewGuid(), Name = "Passport", HasExpiry = true };
        db.DocumentTypes.Add(type);
        foreach (var id in new[] { document, privateDocument, pending })
            db.Documents.Add(new Document { Id = id, UserId = owner, DocumentTypeId = type.Id,
                DocumentName = id == document ? "Shared passport" : "Private file", S3ObjectKey = $"files/{id}",
                ExpiryDate = today.AddDays(20), Status = id == pending ? DocumentStatus.PendingUpload : DocumentStatus.Active });
        db.SaveChanges();
        family = new FamilyService(db);
    }
    public void Dispose() => db.Dispose();
    private async Task<FamilyRequestDto> Share(bool reminders = false)
    {
        var request = await family.SendAsync(viewer, "owner@test.com");
        return await family.RespondAsync(owner, request.Id, new FamilyDecisionDto {
            Revision = request.Revision, Accept = true, DocumentIds = [document], AllowExpiryReminders = reminders });
    }
    private async Task Confirm()
    {
        var row = (await family.DashboardAsync(owner)).Single(d => d.Id == document);
        await family.ConfirmExpiryAsync(owner, document, new ConfirmFamilyExpiryDto { Fingerprint = row.Fingerprint });
    }
    private async Task<List<FamilyAlertDto>> Expiry(Guid user) =>
        (await family.AlertsAsync(user)).Where(a => a.Kind == "expiry").ToList();

    [Fact]
    public async Task DashboardRespectsDocumentGrantsAndCannotLeakOtherMembersFiles()
    {
        Assert.Empty(await family.DashboardAsync(viewer));
        var request = await Share();
        var row = Assert.Single(await family.DashboardAsync(viewer));
        Assert.Equal(document, row.Id); Assert.Equal(owner, row.Owner.Id); Assert.False(row.IsOwned);
        Assert.Equal(2, (await family.DashboardAsync(owner)).Count);
        Assert.Empty(await family.DashboardAsync(stranger));
        await family.UpdateGrantsAsync(owner, request.Id, new FamilyGrantsDto { Revision = request.Revision, DocumentIds = [] });
        Assert.Empty(await family.DashboardAsync(viewer));
    }
    [Fact]
    public async Task ExpiryAlertsRequireBothConfirmationAndOwnerOptIn()
    {
        var request = await Share();
        await Confirm();
        Assert.Single(await Expiry(owner)); Assert.Empty(await Expiry(viewer));
        request = await family.UpdateGrantsAsync(owner, request.Id, new FamilyGrantsDto {
            Revision = request.Revision, DocumentIds = [document], AllowExpiryReminders = true });
        Assert.Single(await Expiry(viewer));
        // Old clients omitting the new field must preserve the owner's existing choice.
        request = await family.UpdateGrantsAsync(owner, request.Id, new FamilyGrantsDto {
            Revision = request.Revision, DocumentIds = [document] });
        Assert.True(request.AllowExpiryReminders);
        request = await family.UpdateGrantsAsync(owner, request.Id, new FamilyGrantsDto {
            Revision = request.Revision, DocumentIds = [document], AllowExpiryReminders = false });
        Assert.Empty(await Expiry(viewer)); Assert.Single(await family.DashboardAsync(viewer));
    }
    [Fact]
    public async Task RevocationAndGrantRemovalSuppressAlertsEvenIfPreviouslyLoaded()
    {
        var request = await Share(true); await Confirm();
        var alert = Assert.Single(await Expiry(viewer));
        request = await family.UpdateGrantsAsync(owner, request.Id, new FamilyGrantsDto {
            Revision = request.Revision, DocumentIds = [] });
        Assert.Empty(await Expiry(viewer));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => family.DismissAlertAsync(viewer, alert.Id));
        request = await family.UpdateGrantsAsync(owner, request.Id, new FamilyGrantsDto {
            Revision = request.Revision, DocumentIds = [document] });
        Assert.Single(await Expiry(viewer));
        await family.EndAsync(owner, request.Id, request.Revision);
        Assert.Empty(await Expiry(viewer)); Assert.Empty(await family.DashboardAsync(viewer));
    }
    [Fact]
    public async Task OnlyOwnerCanConfirmAndStaleDateOrFileNeedsNewConfirmation()
    {
        await Share(true);
        var row = (await family.DashboardAsync(owner)).Single(d => d.Id == document);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => family.ConfirmExpiryAsync(viewer, document,
            new ConfirmFamilyExpiryDto { Fingerprint = row.Fingerprint }));
        Assert.Empty(await Expiry(viewer));
        await Confirm(); Assert.Single(await Expiry(viewer));
        var entity = (await db.Documents.FindAsync(document))!;
        entity.ExpiryDate = today.AddDays(25); await db.SaveChangesAsync();
        Assert.Empty(await Expiry(viewer));
        await Assert.ThrowsAsync<InvalidOperationException>(() => family.ConfirmExpiryAsync(owner, document,
            new ConfirmFamilyExpiryDto { Fingerprint = row.Fingerprint }));
        await Confirm(); Assert.Single(await Expiry(viewer));
        entity.S3ObjectKey = "new-file-version"; await db.SaveChangesAsync();
        Assert.Empty(await Expiry(viewer));
    }
    [Fact]
    public async Task DismissalsArePrivateAndDoNotDismissTheNextMilestone()
    {
        await Share(true); await Confirm();
        var alert = Assert.Single(await Expiry(viewer));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => family.DismissAlertAsync(stranger, alert.Id));
        await family.DismissAlertAsync(viewer, alert.Id);
        await family.DismissAlertAsync(viewer, alert.Id); // idempotent
        Assert.Empty(await Expiry(viewer)); Assert.Single(await Expiry(owner));
        var later = (await family.AlertsAtAsync(viewer, today.AddDays(13))).Where(a => a.Kind == "expiry");
        Assert.NotEqual(alert.Id, Assert.Single(later).Id);
    }
    [Theory]
    [InlineData(91, "Valid", false)]
    [InlineData(90, "Due soon", true)]
    [InlineData(31, "Due soon", true)]
    [InlineData(30, "Due soon", true)]
    [InlineData(8, "Due soon", true)]
    [InlineData(7, "Due soon", true)]
    [InlineData(0, "Due soon", true)]
    [InlineData(-1, "Expired", true)]
    public async Task CalendarDayBoundariesProduceOneCurrentExpiryAlert(int days, string status, bool hasAlert)
    {
        (await db.Documents.FindAsync(document))!.ExpiryDate = today.AddDays(days); await db.SaveChangesAsync();
        await Share(true); await Confirm();
        var row = Assert.Single(await family.DashboardAtAsync(viewer, today.AddHours(23)));
        Assert.Equal(days, row.DaysRemaining); Assert.Equal(status, row.ExpiryStatus);
        var alerts = (await family.AlertsAtAsync(viewer, today.AddHours(23))).Where(a => a.Kind == "expiry");
        Assert.Equal(hasAlert ? 1 : 0, alerts.Count());
    }
    [Fact]
    public async Task MissingDateHasNoExpiryAlertAndCannotBeConfirmed()
    {
        (await db.Documents.FindAsync(document))!.ExpiryDate = null; await db.SaveChangesAsync();
        await Share(true);
        var row = Assert.Single(await family.DashboardAsync(viewer));
        Assert.Equal("No expiry date", row.ExpiryStatus); Assert.Null(row.DaysRemaining);
        await Assert.ThrowsAsync<ArgumentException>(Confirm); Assert.Empty(await Expiry(viewer));
    }
    [Fact]
    public async Task RequestAlertsAreParticipantScopedAndNotDuplicatedByGrantEdits()
    {
        var request = await family.SendAsync(viewer, "owner@test.com");
        Assert.Single(await family.AlertsAsync(owner)); Assert.Empty(await family.AlertsAsync(stranger));
        request = await family.RespondAsync(owner, request.Id, new FamilyDecisionDto { Revision = request.Revision, Accept = true });
        Assert.Empty(await family.AlertsAsync(owner));
        var alert = Assert.Single(await family.AlertsAsync(viewer));
        await family.DismissAlertAsync(viewer, alert.Id);
        await family.UpdateGrantsAsync(owner, request.Id, new FamilyGrantsDto { Revision = request.Revision, DocumentIds = [document] });
        Assert.Empty(await family.AlertsAsync(viewer));
    }
}
