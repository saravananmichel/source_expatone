using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Services;

/// <summary>
/// Opt-in in-app occasion delivery. Disabled until the phase 3.3 SQL migration is applied.
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
            try { await DeliverOccasions(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Notification delivery cycle failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeliverOccasions(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var now = DateTime.UtcNow;
        var occasions = await db.FamilyOccasions.AsNoTracking()
            .Where(x => x.RemindersEnabled && (x.RepeatsAnnually || x.OccasionDate <= now.AddDays(30)))
            .ToListAsync(ct);

        foreach (var occasion in occasions)
        {
            ct.ThrowIfCancellationRequested();
            var date = occasion.OccasionDate;
            if (occasion.RepeatsAnnually)
            {
                var year = Math.Max(now.Year, date.Year);
                var day = Math.Min(date.Day, DateTime.DaysInMonth(year, date.Month));
                date = new DateTime(year, date.Month, day, date.Hour, date.Minute, date.Second, DateTimeKind.Utc);
                if (date.Date < now.Date)
                {
                    year++;
                    day = Math.Min(occasion.OccasionDate.Day, DateTime.DaysInMonth(year, date.Month));
                    date = new DateTime(year, date.Month, day, date.Hour, date.Minute, date.Second, DateTimeKind.Utc);
                }
            }

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
