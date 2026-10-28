using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Services;

/// <summary>
/// Opt-in in-app occasion, document, corporate, sharing, visa, and invitation reminder delivery. Disabled until the phase 3.3 SQL migration is applied.
/// Multiple ECS instances may race; the database unique occurrence constraint is authoritative.
/// </summary>
public sealed class NotificationDeliveryWorker(
    IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Notifications:WorkerEnabled", false))
        {
            logger.LogInformation("Notification delivery worker disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try { await DeliverOccasions(stoppingToken); await DeliverDocumentReminders(stoppingToken); await DeliverCorporateTasks(stoppingToken); await DeliverSharingExpiry(stoppingToken); await DeliverVisaDrafts(stoppingToken); await DeliverInvitations(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Notification delivery cycle failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public static DateTime NextOccurrence(DateTime original, bool annually, DateTime now)
    {
        if (!annually) return original;
        var year = Math.Max(now.Year, original.Year);
        var day = Math.Min(original.Day, DateTime.DaysInMonth(year, original.Month));
        var occurrence = new DateTime(year, original.Month, day,
            original.Hour, original.Minute, original.Second, DateTimeKind.Utc);
        if (occurrence.Date < now.Date)
        {
            year++;
            day = Math.Min(original.Day, DateTime.DaysInMonth(year, original.Month));
            occurrence = new DateTime(year, original.Month, day,
                original.Hour, original.Minute, original.Second, DateTimeKind.Utc);
        }
        return occurrence;
    }

    private async Task DeliverInvitations(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var now = DateTime.UtcNow;
        var invitations = await db.FamilyRequests.AsNoTracking()
            .Where(x => x.StatusId == FamilyRequestStates.Pending
                && x.CreatedAt >= now.AddDays(-7))
            .Select(x => new { x.Id, x.RecipientUserId, x.CreatedAt })
            .ToListAsync(ct);

        foreach (var invitation in invitations)
        {
            ct.ThrowIfCancellationRequested();
            var preference = await db.NotificationPreferences.AsNoTracking()
                .SingleOrDefaultAsync(x => x.UserId == invitation.RecipientUserId, ct);
            if (preference is { InAppEnabled: false } || preference is { ConnectionEnabled: false })
                continue;

            var key = "connection:" + invitation.Id;
            var occurrence = DateOnly.FromDateTime(invitation.CreatedAt);
            if (await db.NotificationDeliveries.AnyAsync(x =>
                    x.UserId == invitation.RecipientUserId && x.ReminderKey == key &&
                    x.Channel == "InApp" && x.OccurrenceDate == occurrence, ct))
                continue;

            db.NotificationDeliveries.Add(new NotificationDelivery
            {
                Id = Guid.NewGuid(), UserId = invitation.RecipientUserId,
                ReminderKey = key, Channel = "InApp", OccurrenceDate = occurrence,
                Title = "Connection invitation awaiting response",
                Body = "You have a pending Friends & Family invitation.",
                Status = "Delivered", DeliveredAt = now
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                if (!await db.NotificationDeliveries.AnyAsync(x =>
                        x.UserId == invitation.RecipientUserId && x.ReminderKey == key &&
                        x.Channel == "InApp" && x.OccurrenceDate == occurrence, ct))
                    throw;
            }
        }
    }

    private async Task DeliverVisaDrafts(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var now = DateTime.UtcNow;
        var drafts = await db.VisaApplications.AsNoTracking()
            .Where(x => !x.Submitted && x.UpdatedAt >= now.AddDays(-30)
                && x.UpdatedAt <= now.AddDays(-3))
            .Select(x => new { x.Id, x.UserId, x.TemplateId, x.UpdatedAt })
            .ToListAsync(ct);

        foreach (var draft in drafts)
        {
            ct.ThrowIfCancellationRequested();
            var preference = await db.NotificationPreferences.AsNoTracking()
                .SingleOrDefaultAsync(x => x.UserId == draft.UserId, ct);
            if (preference is { InAppEnabled: false } || preference is { VisaEnabled: false })
                continue;
            var key = "visa:" + draft.Id;
            // A new saved revision can generate a new reminder only after another 3 days.
            var occurrence = DateOnly.FromDateTime(draft.UpdatedAt.AddDays(3));
            if (await db.NotificationDeliveries.AnyAsync(x =>
                    x.UserId == draft.UserId && x.ReminderKey == key &&
                    x.Channel == "InApp" && x.OccurrenceDate == occurrence, ct))
                continue;
            db.NotificationDeliveries.Add(new NotificationDelivery
            {
                Id = Guid.NewGuid(), UserId = draft.UserId, ReminderKey = key,
                Channel = "InApp", OccurrenceDate = occurrence,
                Title = "Visa application draft awaiting completion",
                Body = "Application template: " + draft.TemplateId,
                Status = "Delivered", DeliveredAt = now
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                if (!await db.NotificationDeliveries.AnyAsync(x =>
                        x.UserId == draft.UserId && x.ReminderKey == key &&
                        x.Channel == "InApp" && x.OccurrenceDate == occurrence, ct))
                    throw;
            }
        }
    }

    private async Task DeliverSharingExpiry(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var now = DateTime.UtcNow;
        var shares = await db.FamilyRequests.AsNoTracking()
            .Where(x => x.StatusId == FamilyRequestStates.Accepted
                && x.AllowExpiryReminders && x.AccessExpiresAt.HasValue
                && x.AccessExpiresAt.Value >= now
                && x.AccessExpiresAt.Value <= now.AddDays(7))
            .Select(x => new { x.Id, x.RecipientUserId, x.AccessExpiresAt })
            .ToListAsync(ct);

        foreach (var share in shares)
        {
            ct.ThrowIfCancellationRequested();
            var recipient = share.RecipientUserId;
            var preference = await db.NotificationPreferences.AsNoTracking()
                .SingleOrDefaultAsync(x => x.UserId == recipient, ct);
            if (preference is { InAppEnabled: false } || preference is { SharingEnabled: false })
                continue;
            var key = "sharing:" + share.Id;
            var occurrence = DateOnly.FromDateTime(share.AccessExpiresAt!.Value);
            if (await db.NotificationDeliveries.AnyAsync(x =>
                    x.UserId == recipient && x.ReminderKey == key &&
                    x.Channel == "InApp" && x.OccurrenceDate == occurrence, ct))
                continue;
            db.NotificationDeliveries.Add(new NotificationDelivery
            {
                Id = Guid.NewGuid(), UserId = recipient, ReminderKey = key,
                Channel = "InApp", OccurrenceDate = occurrence,
                Title = "Shared document access expires soon",
                Body = "Access expires on " + share.AccessExpiresAt.Value.ToString("yyyy-MM-dd"),
                Status = "Delivered", DeliveredAt = now
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                if (!await db.NotificationDeliveries.AnyAsync(x =>
                        x.UserId == recipient && x.ReminderKey == key &&
                        x.Channel == "InApp" && x.OccurrenceDate == occurrence, ct))
                    throw;
            }
        }
    }

    private async Task DeliverCorporateTasks(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var now = DateTime.UtcNow;
        var tasks = await db.CorporateCases.AsNoTracking()
            .Where(x => x.DueAt.HasValue && x.DueAt.Value >= now.AddDays(-7)
                && x.DueAt.Value <= now && x.Status != "Completed" && x.Status != "Cancelled")
            .Select(x => new { x.Id, x.CompanyId, x.EmployeeUserId, x.AssignedHrUserId,
                x.Title, x.Notes, x.DueAt })
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            ct.ThrowIfCancellationRequested();
            var recipients = new[] { task.EmployeeUserId, task.AssignedHrUserId ?? Guid.Empty }
                .Where(x => x != Guid.Empty).Distinct();
            foreach (var recipient in recipients)
            {
                // Revoked, inactive, or removed corporate members must not receive task data.
                if (!await db.CorporateMemberships.AsNoTracking().AnyAsync(x =>
                        x.CompanyId == task.CompanyId && x.UserId == recipient
                        && x.Status == "Active", ct))
                    continue;
                var preference = await db.NotificationPreferences.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.UserId == recipient, ct);
                if (preference is { InAppEnabled: false } || preference is { CorporateEnabled: false })
                    continue;

                var key = "corporate:" + task.Id;
                var occurrence = DateOnly.FromDateTime(task.DueAt!.Value);
                if (await db.NotificationDeliveries.AnyAsync(x =>
                        x.UserId == recipient && x.ReminderKey == key &&
                        x.Channel == "InApp" && x.OccurrenceDate == occurrence, ct))
                    continue;

                db.NotificationDeliveries.Add(new NotificationDelivery
                {
                    Id = Guid.NewGuid(), UserId = recipient, ReminderKey = key,
                    Channel = "InApp", OccurrenceDate = occurrence,
                    Title = task.Title, Body = task.Notes,
                    Status = "Delivered", DeliveredAt = now
                });
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException)
                {
                    db.ChangeTracker.Clear();
                    if (!await db.NotificationDeliveries.AnyAsync(x =>
                            x.UserId == recipient && x.ReminderKey == key &&
                            x.Channel == "InApp" && x.OccurrenceDate == occurrence, ct))
                        throw;
                }
            }
        }
    }

    private async Task DeliverDocumentReminders(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var now = DateTime.UtcNow;
        // Bound the catch-up window so enabling the worker does not flood old inboxes.
        var reminders = await db.Reminders.AsNoTracking()
            .Where(x => x.Status == ReminderStatus.Active
                && x.ReminderDate <= now && x.ReminderDate >= now.AddDays(-7))
            .Select(x => new { x.Id, x.UserId, x.Title, x.Description, x.ReminderDate })
            .ToListAsync(ct);

        foreach (var reminder in reminders)
        {
            ct.ThrowIfCancellationRequested();
            var preference = await db.NotificationPreferences.AsNoTracking()
                .SingleOrDefaultAsync(x => x.UserId == reminder.UserId, ct);
            if (preference is { InAppEnabled: false } || preference is { DocumentEnabled: false })
                continue;

            var key = "document:" + reminder.Id;
            var occurrence = DateOnly.FromDateTime(reminder.ReminderDate);
            if (await db.NotificationDeliveries.AnyAsync(x =>
                    x.UserId == reminder.UserId && x.ReminderKey == key &&
                    x.Channel == "InApp" && x.OccurrenceDate == occurrence, ct))
                continue;

            db.NotificationDeliveries.Add(new NotificationDelivery
            {
                Id = Guid.NewGuid(), UserId = reminder.UserId,
                ReminderKey = key, Channel = "InApp",
                OccurrenceDate = occurrence, Title = reminder.Title,
                Body = reminder.Description,
                Status = "Delivered", DeliveredAt = now
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                if (!await db.NotificationDeliveries.AnyAsync(x =>
                        x.UserId == reminder.UserId && x.ReminderKey == key &&
                        x.Channel == "InApp" && x.OccurrenceDate == occurrence, ct))
                    throw;
            }
        }
    }

    private async Task DeliverOccasions(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var now = DateTime.UtcNow;
        var occasions = await db.FamilyOccasions.AsNoTracking()
            .Where(x => x.RemindersEnabled && (x.RepeatsAnnually || (x.OccasionDate >= now.Date && x.OccasionDate <= now.AddDays(30))))
            .ToListAsync(ct);

        foreach (var occasion in occasions)
        {
            ct.ThrowIfCancellationRequested();
            var date = NextOccurrence(occasion.OccasionDate, occasion.RepeatsAnnually, now);
            if (date.Date < now.Date || date.AddDays(-occasion.DaysBefore).Date > now.Date)
                continue;

            var preference = await db.NotificationPreferences.AsNoTracking()
                .SingleOrDefaultAsync(x => x.UserId == occasion.UserId, ct);
            if (preference is { InAppEnabled: false } || preference is { OccasionEnabled: false })
                continue;

            var key = "occasion:" + occasion.Id;
            var occurrence = DateOnly.FromDateTime(date);
            if (await db.NotificationDeliveries.AnyAsync(x =>
                    x.UserId == occasion.UserId && x.ReminderKey == key &&
                    x.Channel == "InApp" && x.OccurrenceDate == occurrence, ct))
                continue;

            db.NotificationDeliveries.Add(new NotificationDelivery
            {
                Id = Guid.NewGuid(), UserId = occasion.UserId,
                ReminderKey = key, Channel = "InApp",
                OccurrenceDate = occurrence,
                Title = occasion.Title,
                Body = occasion.Kind + " on " + date.ToString("yyyy-MM-dd"),
                Status = "Delivered", DeliveredAt = now
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                // Another worker may have inserted the same unique occurrence.
                db.ChangeTracker.Clear();
                if (!await db.NotificationDeliveries.AnyAsync(x =>
                        x.UserId == occasion.UserId && x.ReminderKey == key &&
                        x.Channel == "InApp" && x.OccurrenceDate == occurrence, ct))
                    throw;
            }
        }
    }
}
