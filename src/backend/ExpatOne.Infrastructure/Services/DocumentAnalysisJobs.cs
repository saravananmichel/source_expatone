using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace ExpatOne.Infrastructure.Services;
public class DocumentAnalysisJobs(ExpatOneDbContext db, IConfiguration config, IStorageService? storage = null, IDocumentAuditService? audit = null) : IDocumentAnalysisJobs
{
    public async Task<AnalysisJobDto> EnqueueAsync(Guid userId, Guid documentId, bool force, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(x => x.Id == documentId && x.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Document not found.");
        if (document.Status == DocumentStatus.PendingUpload || string.IsNullOrEmpty(document.S3ObjectKey))
            throw new InvalidOperationException("Complete the upload first.");
        var fingerprint = DocumentAnalysisConfiguration.Fingerprint(config);
        string? contentHash = null;
        if (storage != null)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await using var input = await storage.DownloadFileAsync(document.S3ObjectKey).WaitAsync(timeout.Token);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920]; long total = 0; int count;
            while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0)
            {
                total += count;
                if (total > 10 * 1024 * 1024) throw new InvalidOperationException("Document exceeds the processing size limit.");
                hash.AppendData(buffer, 0, count);
            }
            contentHash = Convert.ToHexString(hash.GetHashAndReset());
        }
        var prior = await db.DocumentAnalysisRuns.Where(x => x.DocumentId == documentId &&
            x.ObjectKey == document.S3ObjectKey && x.ConfigurationVersion == fingerprint && x.ContentHash == contentHash &&
            (x.Status == "QUEUED" || x.Status == "PROCESSING" || (!force && (x.Status == "COMPLETED" || x.Status == "REQUIRES_REVIEW"))))
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (prior != null) return Map(prior);
        var version = await db.DocumentVersions.FirstOrDefaultAsync(x => x.DocumentId == documentId && x.S3ObjectKey == document.S3ObjectKey && (x.Sha256Hash == contentHash || (x.IsCurrent && x.Sha256Hash == null)), ct);
        if (version != null && version.Sha256Hash == null) version.Sha256Hash = contentHash;
        DocumentVersion? snapshot = null;
        if (version == null)
        {
            var nextVersion = (await db.DocumentVersions.Where(x => x.DocumentId == documentId)
                .Select(x => (int?)x.VersionNumber).MaxAsync(ct) ?? 0) + 1;
            snapshot = new DocumentVersion { Id = Guid.NewGuid(), DocumentId = documentId,
                VersionNumber = nextVersion, S3ObjectKey = document.S3ObjectKey, OriginalFileName = document.OriginalFileName,
                ContentType = document.ContentType, FileSizeBytes = document.FileSizeBytes, Sha256Hash = contentHash,
                IsCurrent = !await db.DocumentVersions.AnyAsync(x => x.DocumentId == documentId && x.IsCurrent, ct), UploadedByUserId = userId };
            db.DocumentVersions.Add(snapshot);
            version = snapshot;
        }
        var run = new DocumentAnalysisRun {
            Id = Guid.NewGuid(), DocumentId = documentId, UserId = userId, DocumentVersionId = version?.Id,
            ObjectKey = document.S3ObjectKey, ContentType = document.ContentType ?? "", ConfigurationVersion = fingerprint, ContentHash = contentHash
        };
        db.DocumentAnalysisRuns.Add(run);
        audit?.Stage(documentId, userId, "analysis_queued", targetVersionId: run.DocumentVersionId,
            metadata: JsonSerializer.Serialize(new { analysisId = run.Id }));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(run).State = EntityState.Detached;
            if (snapshot != null) db.Entry(snapshot).State = EntityState.Detached;
            var existing = await db.DocumentAnalysisRuns.FirstOrDefaultAsync(x => x.DocumentId == documentId &&
                x.ObjectKey == document.S3ObjectKey && x.ConfigurationVersion == fingerprint &&
                (x.Status == "QUEUED" || x.Status == "PROCESSING"), ct);
            if (existing == null) throw;
            return Map(existing);
        }
        return Map(run);
    }
    public async Task<AnalysisJobDto?> GetAsync(Guid userId, Guid documentId, Guid? analysisId, CancellationToken ct)
    {
        await Authorize(userId, documentId, ct);
        var query = db.DocumentAnalysisRuns.Where(x => x.DocumentId == documentId && x.UserId == userId);
        if (analysisId.HasValue) query = query.Where(x => x.Id == analysisId.Value);
        var run = await query.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        return run == null ? null : Map(run);
    }
    public async Task<List<AnalysisJobDto>> HistoryAsync(Guid userId, Guid documentId, CancellationToken ct)
    {
        await Authorize(userId, documentId, ct);
        var runs = await db.DocumentAnalysisRuns.Where(x => x.DocumentId == documentId && x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        return runs.Select(Map).ToList();
    }
    private async Task Authorize(Guid userId, Guid documentId, CancellationToken ct)
    {
        if (!await db.Documents.AnyAsync(x => x.Id == documentId && x.UserId == userId, ct))
            throw new KeyNotFoundException("Document not found.");
    }
    public static AnalysisJobDto Map(DocumentAnalysisRun run) => new(run.Id, run.DocumentId, run.DocumentVersionId,
        run.Status, run.Stage, run.CreatedAt, run.ErrorCategory,
        run.ResultJson == null ? null : JsonSerializer.Deserialize<DocumentAnalysisDto>(run.ResultJson));
}
