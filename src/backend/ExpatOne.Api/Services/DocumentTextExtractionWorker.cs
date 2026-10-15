using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Services;

public sealed class DocumentTextExtractionWorker(
    IServiceScopeFactory scopes,
    ILogger<DocumentTextExtractionWorker> logger) : BackgroundService
{
    private const int MaxAttempts = 5;
    private const int MaxBytes = 10 * 1024 * 1024;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ProcessOne(stoppingToken))
                    continue;
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Text extraction worker could not access its queue.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    public async Task<bool> ProcessOne(CancellationToken stoppingToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var now = DateTime.UtcNow;

        var job = await db.Documents.AsNoTracking()
            .Where(d =>
                (d.ExtractionStatus == "PENDING" &&
                 d.ExtractionAttempts < MaxAttempts &&
                 (d.ExtractionLeaseUntil == null || d.ExtractionLeaseUntil < now))
                ||
                (d.ExtractionStatus == "PROCESSING" &&
                 d.ExtractionLeaseUntil < now))
            .OrderBy(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.S3ObjectKey,
                d.FileSizeBytes,
                d.ContentType,
                d.ExtractionAttempts,
                d.ExtractionStatus,
                d.ExtractionLeaseUntil
            })
            .FirstOrDefaultAsync(stoppingToken);

        if (job is null)
            return false;

        // A crashed final attempt must be marked failed without incrementing
        // the attempt count beyond MaxAttempts.
        if (job.ExtractionStatus == "PROCESSING" &&
            job.ExtractionAttempts >= MaxAttempts)
        {
            var recovered = await db.Documents
                .Where(d => d.Id == job.Id &&
                    d.ExtractionStatus == "PROCESSING" &&
                    d.ExtractionAttempts >= MaxAttempts &&
                    d.ExtractionLeaseUntil < now)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(d => d.ExtractionStatus, "FAILED")
                    .SetProperty(d => d.ExtractionLeaseUntil, (DateTime?)null)
                    .SetProperty(d => d.ExtractionLeaseToken, (Guid?)null)
                    .SetProperty(d => d.UpdatedAt, now), stoppingToken);

            return recovered > 0;
        }

        var token = Guid.NewGuid();
        var claimed = await db.Documents
            .Where(d => d.Id == job.Id &&
                ((d.ExtractionStatus == "PENDING" &&
                  d.ExtractionAttempts < MaxAttempts &&
                  (d.ExtractionLeaseUntil == null || d.ExtractionLeaseUntil < now))
                 ||
                 (d.ExtractionStatus == "PROCESSING" &&
                  d.ExtractionAttempts < MaxAttempts &&
                  d.ExtractionLeaseUntil < now)))
            .ExecuteUpdateAsync(set => set
                .SetProperty(d => d.ExtractionStatus, "PROCESSING")
                .SetProperty(d => d.ExtractionLeaseUntil, now.AddMinutes(3))
                .SetProperty(d => d.ExtractionLeaseToken, (Guid?)token)
                .SetProperty(d => d.ExtractionAttempts, d => d.ExtractionAttempts + 1)
                .SetProperty(d => d.UpdatedAt, now), stoppingToken);

        if (claimed == 0)
            return true;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(135));

        try
        {
            if (job.FileSizeBytes > MaxBytes)
                throw new InvalidDataException("Document exceeds extraction size limit.");

            var contentType = job.ContentType ?? "";
            if (contentType is not ("application/pdf" or "image/jpeg" or "image/png"))
                throw new InvalidDataException("Unsupported extraction content type.");

            var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
            await using var input = await storage.DownloadFileAsync(job.S3ObjectKey)
                .WaitAsync(timeout.Token);

            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int count;

            while ((count = await input.ReadAsync(chunk, timeout.Token)) > 0)
            {
                if (buffer.Length + count > MaxBytes)
                    throw new InvalidDataException("Downloaded document exceeds size limit.");

                await buffer.WriteAsync(chunk.AsMemory(0, count), timeout.Token);
            }

            var client = scope.ServiceProvider
                .GetRequiredService<DocumentTextExtractionClient>();

            var text = await client.ExtractAsync(
                buffer.ToArray(), contentType, timeout.Token);

            var completed = await db.Documents
                .Where(d => d.Id == job.Id && d.ExtractionLeaseToken == token)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(d => d.ExtractedText, text)
                    .SetProperty(d => d.ExtractionStatus, "COMPLETED")
                    .SetProperty(d => d.ExtractionLeaseUntil, (DateTime?)null)
                    .SetProperty(d => d.ExtractionLeaseToken, (Guid?)null)
                    .SetProperty(d => d.UpdatedAt, DateTime.UtcNow), stoppingToken);

            if (completed == 1)
                logger.LogInformation("Text extraction completed for document {DocumentId}.", job.Id);
            else
                logger.LogInformation(
                    "Text extraction result discarded because its lease was lost for document {DocumentId}.",
                    job.Id);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            var attempts = job.ExtractionAttempts + 1;
            var retry = attempts < MaxAttempts && IsTransientFailure(ex);
            var failedAt = DateTime.UtcNow;

            await db.Documents
                .Where(d => d.Id == job.Id && d.ExtractionLeaseToken == token)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(d => d.ExtractionStatus, retry ? "PENDING" : "FAILED")
                    .SetProperty(d => d.ExtractionLeaseUntil,
                        retry ? failedAt.AddSeconds(30 * attempts) : (DateTime?)null)
                    .SetProperty(d => d.ExtractionLeaseToken, (Guid?)null)
                    .SetProperty(d => d.UpdatedAt, failedAt), stoppingToken);

            logger.LogWarning(
                ex,
                "Text extraction failed for document {DocumentId}; retry={Retry}, attempt={Attempt}.",
                job.Id, retry, attempts);
        }

        return true;
    }

    private static bool IsTransientFailure(Exception error) => error switch
    {
        OperationCanceledException => true,
        HttpRequestException request =>
            request.StatusCode is null
            || request.StatusCode is System.Net.HttpStatusCode.RequestTimeout
                or System.Net.HttpStatusCode.TooManyRequests
            || (request.StatusCode is { } status && (int)status >= 500),
        _ => false
    };
}
