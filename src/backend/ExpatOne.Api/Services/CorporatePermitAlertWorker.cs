using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Services;

/// <summary>
/// Periodically creates durable HR inbox alerts. No email/push is sent.
/// PostgreSQL unique index makes this safe across multiple ECS replicas.
/// </summary>
public sealed class CorporatePermitAlertWorker(
    IServiceScopeFactory scopes, ILogger<CorporatePermitAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
                var today = DateTime.UtcNow.Date;
                // Only the most urgent applicable threshold is emitted at a time.
                // Acknowledgement is kept for audit, and an expiry date change
                // creates a distinct alert series without overwriting old alerts.
                var inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO corporate_permit_alerts
    (""Id"", ""CompanyId"", ""PermitId"", ""ThresholdDays"", ""ExpiryDate"",
     ""CreatedAt"", ""UpdatedAt"")
SELECT gen_random_uuid(), p.""CompanyId"", p.""Id"",
    CASE
        WHEN p.""ExpiresOn"" <= {today} THEN 0
        WHEN p.""ExpiresOn"" <= {today.AddDays(7)} THEN 7
        WHEN p.""ExpiresOn"" <= {today.AddDays(30)} THEN 30
        WHEN p.""ExpiresOn"" <= {today.AddDays(60)} THEN 60
        ELSE 90
    END,
    p.""ExpiresOn"", now(), now()
FROM corporate_issued_permits p
WHERE p.""Status"" = 'Active'
  AND p.""ExpiresOn"" <= {today.AddDays(90)}
ON CONFLICT (""PermitId"", ""ThresholdDays"", ""ExpiryDate"") DO NOTHING",
                    stoppingToken);
                logger.LogInformation("Permit alert scan completed; inserted {Count} alerts", inserted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Corporate permit alert scan failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
