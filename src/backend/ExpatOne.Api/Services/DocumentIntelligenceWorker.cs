using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
namespace ExpatOne.Api.Services;

public class DocumentIntelligenceWorker(IServiceScopeFactory scopes, ILogger<DocumentIntelligenceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await ProcessOne(stoppingToken)) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Document worker could not access its queue."); }
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
    public async Task<bool> ProcessOne(CancellationToken stoppingToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var now = DateTime.UtcNow;
        var run = await db.DocumentAnalysisRuns.AsNoTracking().Where(x => (x.Status == "QUEUED" && (x.LeaseUntil == null || x.LeaseUntil < now)) ||
            (x.Status == "PROCESSING" && x.LeaseUntil < now)).OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(stoppingToken);
        if (run == null) return false;
        var token = Guid.NewGuid();
        var claimed = await db.DocumentAnalysisRuns.Where(x => x.Id == run.Id && ((x.Status == "QUEUED" && (x.LeaseUntil == null || x.LeaseUntil < now)) ||
            (x.Status == "PROCESSING" && x.LeaseUntil < now))).ExecuteUpdateAsync(set => set
                .SetProperty(x => x.Status, "PROCESSING").SetProperty(x => x.Stage, "Extracting and understanding")
                .SetProperty(x => x.LeaseUntil, now.AddMinutes(18)).SetProperty(x => x.LeaseToken, token).SetProperty(x => x.UpdatedAt, now)
                .SetProperty(x => x.Attempts, x => x.Attempts + 1), stoppingToken);
        if (claimed == 0) return true;
        var duration = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(17));
        try
        {
            if (run.Attempts >= 3) throw new InvalidOperationException("Retry limit.");
            var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
            // Bound actual storage bytes independently of upload metadata; always clean the temp file.
            var path = Path.GetTempFileName();
            try
            {
                await using var temp = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete,
                    81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                await using (var input = await storage.DownloadFileAsync(run.ObjectKey).WaitAsync(timeout.Token))
                {
                    var buffer = new byte[81920];
                    int count;
                    while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0)
                    {
                        if (temp.Length + count > 10 * 1024 * 1024) throw new InvalidDataException();
                        await temp.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
                    }
                }
                temp.Position = 0;
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(temp, timeout.Token));
                if (run.ContentHash != null && run.ContentHash != hash) throw new InvalidDataException("Document changed.");
                var storageMs = duration.ElapsedMilliseconds;
                temp.Position = 0;
                var provider = scope.ServiceProvider.GetRequiredService<DocumentIntelligenceService>();
                ExpatOne.Application.DTOs.DocumentAnalysisDto result;
                // Keep temp open when the HTTP content disposes its stream.
                await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var execution = DocumentAnalysisConfiguration.Decode(run.ConfigurationVersion);
                result = await provider.AnalyzeWithConfigurationAsync(source, run.ContentType, timeout.Token, execution.Provider, execution.Fallback);
                var quality = result.QualityDiagnostics is { ValueKind: JsonValueKind.Object } q
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(q.GetRawText())! : new Dictionary<string, object>();
                quality["storageMs"] = storageMs;
                quality["workerMs"] = duration.ElapsedMilliseconds;
                // This is measured before persistence; database duration is deliberately not fabricated.
                result.QualityDiagnostics = JsonSerializer.SerializeToElement(quality);
                result.DocumentId = run.DocumentId;
                result.AnalysisId = run.Id;
                result.ConfigurationVersion = run.ConfigurationVersion;
                result.DocumentVersionId = run.DocumentVersionId;
                result.AnalyzedAt = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(result);
                await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);
                var saved = await db.DocumentAnalysisRuns.Where(x => x.Id == run.Id && x.LeaseToken == token).ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.Status, result.RequiresReview ? "REQUIRES_REVIEW" : "COMPLETED")
                    .SetProperty(x => x.Stage, "Finished").SetProperty(x => x.UpdatedAt, DateTime.UtcNow).SetProperty(x => x.ResultJson, json)
                    .SetProperty(x => x.ContentHash, hash).SetProperty(x => x.CompletedAt, DateTime.UtcNow)
                    .SetProperty(x => x.LeaseUntil, (DateTime?)null), stoppingToken);
                if (saved == 0) return true;
                scope.ServiceProvider.GetService<IDocumentAuditService>()?.Stage(run.DocumentId, run.UserId,
                    "analysis_completed", targetVersionId: run.DocumentVersionId,
                    metadata: JsonSerializer.Serialize(new { analysisId = run.Id }));
                await db.SaveChangesAsync(stoppingToken);
                // Never let an older version overwrite the current document's cached display.
                var legacyJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                await db.Documents.Where(x => x.Id == run.DocumentId && x.S3ObjectKey == run.ObjectKey)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.ExtractedMetadata, legacyJson), stoppingToken);
                await transaction.CommitAsync(stoppingToken);
                logger.LogInformation("Document analysis {AnalysisId} completed using {Provider} in {DurationMs} ms", run.Id, result.Provider, duration.ElapsedMilliseconds);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            var retry = run.Attempts < 2 && IsTransientFailure(ex);
            await db.DocumentAnalysisRuns.Where(x => x.Id == run.Id && x.LeaseToken == token).ExecuteUpdateAsync(set => set
                .SetProperty(x => x.Status, retry ? "QUEUED" : "FAILED").SetProperty(x => x.Stage, retry ? "Retrying" : "Failed")
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow).SetProperty(x => x.ErrorCategory, ex is OperationCanceledException ? "timeout" : "processing_failed")
                .SetProperty(x => x.LeaseUntil, retry ? DateTime.UtcNow.AddSeconds(30 * (run.Attempts + 1)) : (DateTime?)null), stoppingToken);
            logger.LogWarning("Document analysis {AnalysisId} failed; retry={Retry}", run.Id, retry);
        }
        return true;
    }

    private static bool IsTransientFailure(Exception error) => error switch
    {
        OperationCanceledException => true,
        HttpRequestException request => request.StatusCode is null
            || request.StatusCode is System.Net.HttpStatusCode.RequestTimeout
                or System.Net.HttpStatusCode.TooManyRequests
            || (int)request.StatusCode >= 500,
        _ => false
    };
}
