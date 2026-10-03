using System.Text.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
namespace ExpatOne.Tests;

public class DocumentAnalysisServiceTests
{
    internal static DocumentAnalysisDto Analysis() => new() {
        Provider = "Local", ModelVersion = "qwen3:4b@synthetic-digest", DocumentCategory = "Employment Contract",
        Summary = "Synthetic contract", SemanticDocument = JsonSerializer.SerializeToElement(new {
            pages = new[] { new { page = 2, text = "Salary: RM12,000 per month. Notice is seven days.",
                blocks = new[] { new { text = "Salary: RM12,000 per month." } } } } }),
        Evidence = [new() { Id = "e1", Page = 2, SourceText = "Salary: RM12,000 per month." }]
    };
    internal static DocumentAnalysisRun Run(Document document, DocumentAnalysisDto? analysis = null) => new() {
        DocumentId = document.Id, UserId = document.UserId, ObjectKey = document.S3ObjectKey,
        ContentType = "application/pdf", ConfigurationVersion = "synthetic:Local:0", Status = "REQUIRES_REVIEW",
        ResultJson = JsonSerializer.Serialize(analysis ?? Analysis())
    };
    private static (ExpatOneDbContext Db, Document Doc, DocumentAnalysisService Service, Mock<IDocumentAnalysisJobs> Jobs,
        Mock<IDocumentIntelligenceService> Local) Fixture(bool persisted = true)
    {
        var db = new ExpatOneDbContext(new DbContextOptionsBuilder<ExpatOneDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var doc = new Document { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), DocumentTypeId = Guid.NewGuid(),
            DocumentName = "Synthetic", S3ObjectKey = "synthetic/current.pdf", ContentType = "application/pdf", Status = DocumentStatus.Active };
        db.Documents.Add(doc);
        if (persisted) db.DocumentAnalysisRuns.Add(Run(doc));
        db.SaveChanges();
        var jobs = new Mock<IDocumentAnalysisJobs>(MockBehavior.Strict);
        jobs.Setup(x => x.EnqueueAsync(doc.UserId, doc.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnalysisJobDto(Guid.NewGuid(), doc.Id, null, "QUEUED", "Queued", DateTime.UtcNow, null, null));
        var local = new Mock<IDocumentIntelligenceService>(MockBehavior.Strict);
        local.Setup(x => x.AskAsync(It.IsAny<DocumentAskRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentAnswerDto { Answer = "RM12,000 per month", Grounded = true,
                Evidence = [new() { Id = "e1", Page = 2, SourceText = "Salary: RM12,000 per month." }] });
        return (db, doc, new DocumentAnalysisService(db, jobs.Object, local.Object), jobs, local);
    }
    [Fact]
    public async Task ExistingCurrentQwenAnalysisIsReusedWithoutStorageOrExtraction()
    {
        var (db, doc, service, jobs, local) = Fixture();
        var answer = await service.AskDocumentAsync(doc.UserId, doc.Id, "What is the salary?");
        Assert.True(answer.Grounded); Assert.Equal(2, answer.Evidence.Single().Page);
        Assert.Equal(doc.Id, answer.DocumentId);
        jobs.VerifyNoOtherCalls();
        local.Verify(x => x.AskAsync(It.Is<DocumentAskRequestDto>(r => r.Context.Count <= 6 &&
            r.Context.Sum(c => c.Text.Length) <= 6000 && r.Context.Any(c => c.Text.Contains("12,000"))), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Theory]
    [InlineData("missing")][InlineData("Gemini")][InlineData("stale")][InlineData("wrong_model")][InlineData("corrupt")][InlineData("version")]
    public async Task InvalidAnalysisQueuesLocalAndDoesNotAskAnyProvider(string reason)
    {
        var (db, doc, service, jobs, local) = Fixture(reason != "missing");
        if (reason != "missing") {
            var run = db.DocumentAnalysisRuns.Single();
            if (reason == "stale") run.ObjectKey = "old-version.pdf";
            if (reason == "corrupt") run.ResultJson = "{";
            if (reason == "version") db.DocumentVersions.Add(new DocumentVersion { DocumentId = doc.Id, VersionNumber = 2,
                S3ObjectKey = doc.S3ObjectKey, OriginalFileName = "new.pdf", ContentType = "application/pdf", IsCurrent = true });
            if (reason is "Gemini" or "wrong_model") {
                var analysis = Analysis();
                if (reason == "Gemini") analysis.Provider = "Gemini"; else analysis.ModelVersion = "other-model";
                run.ResultJson = JsonSerializer.Serialize(analysis);
            }
            db.SaveChanges();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AskDocumentAsync(doc.UserId, doc.Id, "What is the salary?"));
        jobs.Verify(x => x.EnqueueAsync(doc.UserId, doc.Id, true, It.IsAny<CancellationToken>()), Times.Once);
        local.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task UnrelatedQuestionReturnsInsufficientEvidenceWithoutInference()
    {
        var (_, doc, service, jobs, local) = Fixture();
        var answer = await service.AskDocumentAsync(doc.UserId, doc.Id, "Which volcano erupted yesterday?");
        Assert.False(answer.Grounded); Assert.Empty(answer.Evidence);
        jobs.VerifyNoOtherCalls(); local.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task OtherUserCannotReadOrAskOrEnqueue()
    {
        var (_, doc, service, jobs, local) = Fixture();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AskDocumentAsync(Guid.NewGuid(), doc.Id, "Salary?"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AnalyzeDocumentAsync(Guid.NewGuid(), doc.Id));
        jobs.VerifyNoOtherCalls(); local.VerifyNoOtherCalls();
    }
    [Theory][InlineData(0)][InlineData(2001)]
    public async Task QuestionLengthIsValidated(int length)
    {
        var (_, doc, service, jobs, local) = Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => service.AskDocumentAsync(doc.UserId, doc.Id, new string('x',length)));
        jobs.VerifyNoOtherCalls(); local.VerifyNoOtherCalls();
    }
    [Fact]
    public void RetrievalBoundsLongDocumentsAndFindsRelevantLaterPage()
    {
        var analysis = Analysis();
        analysis.SemanticDocument = JsonSerializer.SerializeToElement(new { pages = Enumerable.Range(1,40).Select(i => new {
            page=i, text = i==39 ? "Termination notice is seven days." : new string('x',5000), blocks=Array.Empty<object>() }) });
        var context = DocumentContextSelector.Select(analysis,"What is the termination notice?");
        Assert.Contains(context,c => c.Page==39); Assert.True(context.Sum(c=>c.Text.Length)<=6000); Assert.True(context.Count<=6);
    }
}
