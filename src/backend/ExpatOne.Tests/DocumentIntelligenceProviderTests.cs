using System.Net;
using System.Text;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;
namespace ExpatOne.Tests;
public class DocumentIntelligenceProviderTests
{
    private class Handler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
    [Fact]
    public async Task NullListsAreRejectedWithoutCrashing()
    {
        var service = new DocumentIntelligenceService(new HttpClient(new Handler(HttpStatusCode.OK,
            "{\"evidence\":null,\"statements\":null}")) { BaseAddress = new Uri("http://localhost/") },new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync(new MemoryStream([1]), "application/pdf", default));
    }
    [Fact]
    public async Task FallbackIsExplicitAndMarkedForReview()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["DocumentIntelligence:EnableFallback"] = "true", ["Gemini:Model"] = "synthetic-gemini"
        }).Build();
        var gemini = new Mock<IAIService>();
        gemini.Setup(x => x.AnalyzeDocumentAsync(It.IsAny<Stream>(),It.IsAny<string>(),It.IsAny<string?>()))
            .ReturnsAsync(new AIResponse { Content = "{\"summary\":\"Synthetic summary\",\"documentCategory\":\"Passport\"}" });
        var service = new DocumentIntelligenceService(new HttpClient(new Handler(HttpStatusCode.ServiceUnavailable,"{}"))
            { BaseAddress = new Uri("http://localhost/") },config,gemini.Object);
        await Assert.ThrowsAsync<LocalAnalysisFallbackException>(() => service.AnalyzeAsync(new MemoryStream([1]),"application/pdf",default));
        var result = await service.AnalyzeGeminiAsync(new MemoryStream([1]),"application/pdf",default);
        Assert.True(result.RequiresReview); Assert.Equal("low",result.Confidence); Assert.Equal("Gemini",result.Provider);
    }
    [Theory]
    [InlineData("missing", "SUPPORTED")]
    [InlineData("e1", "INVENTED")]
    public async Task V2RejectsInvalidEvidenceOrVerdict(string evidenceId, string verdict)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new {
            summary = "Synthetic", modelVersion = "test", analyzerVersion = "document-engine-v2",
            evidence = new[] { new { id = "e1", page = 1, sourceText = "Source" } },
            statements = new[] { new { id = "s1", supportStatus = verdict, evidenceIds = new[] { evidenceId } } }
        });
        var service = new DocumentIntelligenceService(new HttpClient(new Handler(HttpStatusCode.OK, payload))
            { BaseAddress = new Uri("http://localhost/") }, new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync(new MemoryStream([1]), "application/pdf", default));
    }
}
