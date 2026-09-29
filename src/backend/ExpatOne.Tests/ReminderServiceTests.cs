using ExpatOne.Application.DTOs;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpatOne.Tests;

public class ReminderServiceTests
{
    private static readonly Guid PassportTypeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UserAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private (ExpatOneDbContext ctx, ReminderService svc, Guid docWithExpiry, Guid docNoExpiry) Setup()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var ctx = new ExpatOneDbContext(options);

        ctx.DocumentTypes.Add(new DocumentType { Id = PassportTypeId, Name = "Passport", HasExpiry = true, IsSystem = true });
        ctx.Users.Add(new User { Id = UserAId, ExternalId = "a", ExternalProvider = "firebase", Email = "a@test.com" });
        ctx.Users.Add(new User { Id = UserBId, ExternalId = "b", ExternalProvider = "firebase", Email = "b@test.com" });

        var docWithExpiry = Guid.NewGuid();
        ctx.Documents.Add(new Document
        {
            Id = docWithExpiry, UserId = UserAId, DocumentTypeId = PassportTypeId,
            DocumentName = "My Passport", S3ObjectKey = "k1",
            ExpiryDate = DateTime.UtcNow.AddYears(1), Status = DocumentStatus.Active
        });

        var docNoExpiry = Guid.NewGuid();
        ctx.Documents.Add(new Document
        {
            Id = docNoExpiry, UserId = UserAId, DocumentTypeId = PassportTypeId,
            DocumentName = "Letter", S3ObjectKey = "k2",
            ExpiryDate = null, Status = DocumentStatus.Active
        });

        var docUserB = Guid.NewGuid();
        ctx.Documents.Add(new Document
        {
            Id = docUserB, UserId = UserBId, DocumentTypeId = PassportTypeId,
            DocumentName = "B Passport", S3ObjectKey = "k3",
            ExpiryDate = DateTime.UtcNow.AddYears(1), Status = DocumentStatus.Active
        });

        ctx.SaveChanges();

        var svc = new ReminderService(ctx, new Mock<ILogger<ReminderService>>().Object);
        return (ctx, svc, docWithExpiry, docNoExpiry);
    }

    [Fact]
    public async Task CreateReminder_ForOwnDocumentWithExpiry_Succeeds()
    {
        var (_, svc, docId, _) = Setup();
        var r = await svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 30 });
        Assert.Equal(30, r.DaysBeforeExpiry);
        Assert.Equal("Active", r.Status);
    }

    [Fact]
    public async Task CreateReminder_ForDocumentWithoutExpiry_Fails()
    {
        var (_, svc, _, noExpiry) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = noExpiry, DaysBeforeExpiry = 7 }));
    }

    [Fact]
    public async Task CreateReminder_ForOtherUserDocument_Fails()
    {
        var (ctx, svc, _, _) = Setup();
        var docB = ctx.Documents.First(d => d.UserId == UserBId).Id;
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docB, DaysBeforeExpiry = 7 }));
    }

    [Fact]
    public async Task CreateReminder_DuplicateOffset_Fails()
    {
        var (_, svc, docId, _) = Setup();
        await svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 14 });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 14 }));
    }

    [Fact]
    public async Task CreateReminder_InvalidOffset_Fails()
    {
        var (_, svc, docId, _) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 0 }));
    }

    [Fact]
    public async Task GenerateReminders_CreatesMultiple()
    {
        var (_, svc, docId, _) = Setup();
        var result = await svc.GenerateRemindersAsync(UserAId, new GenerateRemindersDto
        {
            DocumentId = docId, DaysBeforeExpiry = [30, 14, 7]
        });
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GenerateReminders_IsIdempotent()
    {
        var (_, svc, docId, _) = Setup();
        await svc.GenerateRemindersAsync(UserAId, new GenerateRemindersDto
        {
            DocumentId = docId, DaysBeforeExpiry = [30, 14]
        });
        var second = await svc.GenerateRemindersAsync(UserAId, new GenerateRemindersDto
        {
            DocumentId = docId, DaysBeforeExpiry = [30, 14]
        });
        Assert.Empty(second);
    }

    [Fact]
    public async Task GenerateReminders_CancelsRemovedOffsets()
    {
        var (ctx, svc, docId, _) = Setup();
        await svc.GenerateRemindersAsync(UserAId, new GenerateRemindersDto
        {
            DocumentId = docId, DaysBeforeExpiry = [30, 14, 7]
        });
        await svc.GenerateRemindersAsync(UserAId, new GenerateRemindersDto
        {
            DocumentId = docId, DaysBeforeExpiry = [30]
        });

        var cancelled = ctx.Reminders.Count(r => r.DocumentId == docId && r.Status == ReminderStatus.Cancelled);
        Assert.Equal(2, cancelled);
    }

    [Fact]
    public async Task GetUserReminders_ReturnsOnlyOwn()
    {
        var (_, svc, docId, _) = Setup();
        await svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 30 });

        var remindersA = await svc.GetUserRemindersAsync(UserAId);
        var remindersB = await svc.GetUserRemindersAsync(UserBId);
        Assert.Single(remindersA);
        Assert.Empty(remindersB);
    }

    [Fact]
    public async Task GetReminder_OtherUserCannotAccess()
    {
        var (_, svc, docId, _) = Setup();
        var r = await svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 7 });

        var found = await svc.GetReminderAsync(UserBId, r.Id);
        Assert.Null(found);
    }

    [Fact]
    public async Task UpdateReminder_CanDismiss()
    {
        var (_, svc, docId, _) = Setup();
        var r = await svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 7 });
        var updated = await svc.UpdateReminderAsync(UserAId, r.Id, new UpdateReminderDto { Status = "Dismissed" });
        Assert.Equal("Dismissed", updated.Status);
    }

    [Fact]
    public async Task UpdateReminder_OtherUserCannotUpdate()
    {
        var (_, svc, docId, _) = Setup();
        var r = await svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 7 });
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.UpdateReminderAsync(UserBId, r.Id, new UpdateReminderDto { Status = "Dismissed" }));
    }

    [Fact]
    public async Task DeleteReminder_OwnerCanDelete()
    {
        var (ctx, svc, docId, _) = Setup();
        var r = await svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 7 });
        await svc.DeleteReminderAsync(UserAId, r.Id);
        Assert.Empty(ctx.Reminders);
    }

    [Fact]
    public async Task DeleteReminder_OtherUserCannotDelete()
    {
        var (_, svc, docId, _) = Setup();
        var r = await svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 7 });
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.DeleteReminderAsync(UserBId, r.Id));
    }

    [Fact]
    public async Task ProcessDueReminders_MarksSent()
    {
        var (ctx, svc, docId, _) = Setup();
        var r = await svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 7 });

        // Make it due
        var entity = ctx.Reminders.Find(r.Id)!;
        entity.ReminderDate = DateTime.UtcNow.AddMinutes(-1);
        ctx.SaveChanges();

        await svc.ProcessDueRemindersAsync();

        var processed = ctx.Reminders.Find(r.Id)!;
        Assert.Equal(ReminderStatus.Sent, processed.Status);
    }

    [Fact]
    public async Task ProcessDueReminders_DoesNotReprocess()
    {
        var (ctx, svc, docId, _) = Setup();
        var r = await svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 7 });

        var entity = ctx.Reminders.Find(r.Id)!;
        entity.ReminderDate = DateTime.UtcNow.AddMinutes(-1);
        ctx.SaveChanges();

        await svc.ProcessDueRemindersAsync();
        await svc.ProcessDueRemindersAsync(); // should be no-op

        Assert.Equal(ReminderStatus.Sent, ctx.Reminders.Find(r.Id)!.Status);
    }

    [Fact]
    public async Task GetDocumentReminders_ReturnsForDocument()
    {
        var (_, svc, docId, _) = Setup();
        await svc.GenerateRemindersAsync(UserAId, new GenerateRemindersDto
        {
            DocumentId = docId, DaysBeforeExpiry = [30, 7]
        });
        var reminders = await svc.GetDocumentRemindersAsync(UserAId, docId);
        Assert.Equal(2, reminders.Count);
    }

    [Fact]
    public async Task DeleteDocument_CascadesReminders()
    {
        var (ctx, svc, docId, _) = Setup();
        await svc.CreateReminderAsync(UserAId, new CreateReminderDto { DocumentId = docId, DaysBeforeExpiry = 7 });

        var doc = ctx.Documents.Find(docId)!;
        ctx.Documents.Remove(doc);
        ctx.SaveChanges();

        Assert.Empty(ctx.Reminders.Where(r => r.DocumentId == docId));
    }
}
