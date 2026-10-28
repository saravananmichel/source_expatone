using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Services;

/// <summary>
/// Opt-in in-app occasion and document reminder delivery. Disabled until the phase 3.3 SQL migration is applied.
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
            try { await DeliverOccasions(stoppingToken); await DeliverDocumentReminders(stoppingToken); }
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
