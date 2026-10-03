using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace ExpatOne.Tests;
public class DocumentAnalysisJobsTests
{
    private static (ExpatOneDbContext db, DocumentAnalysisJobs service, Document document) Create()
    {
        var db = new ExpatOneDbContext(new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var document = new Document { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), DocumentName = "Synthetic",
            S3ObjectKey = "synthetic/v1.pdf", ContentType = "application/pdf", Status = DocumentStatus.Active };
        db.Documents.Add(document); db.SaveChanges();
        return (db, new DocumentAnalysisJobs(db, new ConfigurationBuilder().Build()), document);
    }
    [Fact]
    public async Task QueueIsIdempotentAndOwnerScoped()
    {
        var (db, service, doc) = Create(); using var _ = db;
        var first = await service.EnqueueAsync(doc.UserId, doc.Id, false, default);
        var second = await service.EnqueueAsync(doc.UserId, doc.Id, true, default);
        Assert.Equal(first.AnalysisId, second.AnalysisId);
        Assert.Equal("QUEUED", first.Status);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(Guid.NewGuid(), doc.Id, null, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.EnqueueAsync(Guid.NewGuid(), doc.Id, false, default));
    }
    [Fact]
    public async Task ReanalysisPreservesHistoryAndSnapshotsVersions()
    {
        var (db, service, doc) = Create(); using var _ = db;
        var first = await service.EnqueueAsync(doc.UserId, doc.Id, false, default);
        var run = await db.DocumentAnalysisRuns.FindAsync(first.AnalysisId);
        run!.Status = "COMPLETED"; await db.SaveChangesAsync();
        Assert.Equal(first.AnalysisId, (await service.EnqueueAsync(doc.UserId, doc.Id, false, default)).AnalysisId);
        var second = await service.EnqueueAsync(doc.UserId, doc.Id, true, default);
        Assert.NotEqual(first.AnalysisId, second.AnalysisId);
        doc.S3ObjectKey = "synthetic/v2.pdf"; await db.SaveChangesAsync();
        var third = await service.EnqueueAsync(doc.UserId, doc.Id, false, default);
        Assert.NotEqual(second.AnalysisId, third.AnalysisId);
        Assert.Equal("synthetic/v1.pdf", run.ObjectKey);
        Assert.Equal(3, (await service.HistoryAsync(doc.UserId, doc.Id, default)).Count);
    }
    [Fact]
    public async Task PendingUploadCannotBeAnalyzed()
    {
        var (db, service, doc) = Create(); using var _ = db;
        doc.Status = DocumentStatus.PendingUpload; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnqueueAsync(doc.UserId, doc.Id, false, default));
    }
}
