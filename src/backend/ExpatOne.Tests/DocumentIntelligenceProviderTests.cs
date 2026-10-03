using System.Net;
using System.Text;
using System.Text.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
namespace ExpatOne.Tests;
public class DocumentIntelligenceProviderTests
{
    private class Handler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public List<(Uri Url,string Body,string? Key)> Requests = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal("127.0.0.1",request.RequestUri!.Host);
            Requests.Add((request.RequestUri, await request.Content!.ReadAsStringAsync(ct),
                request.Headers.TryGetValues("X-Service-Key",out var values)?values.Single():null));
            return new HttpResponseMessage(status) { Content = new StringContent(json,Encoding.UTF8,"application/json") };
        }
    }
    private static IConfiguration Config(string provider="Local",bool fallback=false) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["DocumentIntelligence:Provider"]=provider,["DocumentIntelligence:EnableFallback"]=fallback.ToString(),["DocumentIntelligence:ServiceKey"]="synthetic-key",
        ["Gemini:ApiKey"]="configured-but-never-used" }).Build();
    [Fact]
    public async Task AnalysisPostsOriginalFileOnlyToPrivateDocumentAi()
    {
        var result=DocumentAnalysisServiceTests.Analysis(); result.Statements=[new() { Id="s1",EvidenceIds=["e1"] }];
        var handler=new Handler(HttpStatusCode.OK,JsonSerializer.Serialize(result));
        var service=new DocumentIntelligenceService(new HttpClient(handler) { BaseAddress=new Uri("http://127.0.0.1:8090/") },Config());
        Assert.Equal("Local",(await service.AnalyzeAsync(new MemoryStream(Encoding.UTF8.GetBytes("synthetic-document")),"application/pdf",default)).Provider);
        Assert.Equal("/analyze",handler.Requests.Single().Url.AbsolutePath);
        Assert.Contains("synthetic-document",handler.Requests.Single().Body); Assert.Equal("synthetic-key",handler.Requests.Single().Key);
    }
    [Fact]
    public async Task QAPostsSelectedContextOnlyAndPreservesCitations()
    {
        var handler=new Handler(HttpStatusCode.OK,"{\"answer\":\"RM12,000\",\"grounded\":true,\"evidence\":[{\"id\":\"e1\",\"page\":2,\"sourceText\":\"Salary: RM12,000\"}]}");
        var service=new DocumentIntelligenceService(new HttpClient(handler) { BaseAddress=new Uri("http://127.0.0.1:8090/") },Config());
        var answer=await service.AskAsync(new() { Question="Salary?",Context=[new("e1",2,"Salary: RM12,000")] },default);
        Assert.True(answer.Grounded); Assert.Equal(2,answer.Evidence.Single().Page);
        Assert.Equal("/ask",handler.Requests.Single().Url.AbsolutePath); Assert.DoesNotContain("inline_data",handler.Requests.Single().Body);
    }
    [Fact]
    public async Task LocalFailureCannotFallbackEvenWithGeminiKeyConfigured()
    {
        var handler=new Handler(HttpStatusCode.ServiceUnavailable,"{}");
        var service=new DocumentIntelligenceService(new HttpClient(handler) { BaseAddress=new Uri("http://127.0.0.1:8090/") },Config());
        await Assert.ThrowsAsync<HttpRequestException>(()=>service.AnalyzeAsync(new MemoryStream([1]),"application/pdf",default));
        Assert.Single(handler.Requests);
    }
    [Theory][InlineData("Gemini",false)][InlineData("Hybrid",false)][InlineData("Local",true)]
    public async Task UnsafeConfigAndOldQueuedProvidersAreRejectedBeforeNetwork(string provider,bool fallback)
    {
        var handler=new Handler(HttpStatusCode.OK,"{}");
        var service=new DocumentIntelligenceService(new HttpClient(handler) { BaseAddress=new Uri("http://127.0.0.1:8090/") },Config());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.AnalyzeWithConfigurationAsync(new MemoryStream([1]),"application/pdf",default,provider,fallback));
        Assert.Empty(handler.Requests);
    }
    [Fact]
    public async Task InventedAnswerCitationIsRejected()
    {
        var handler=new Handler(HttpStatusCode.OK,"{\"answer\":\"invented\",\"grounded\":true,\"evidence\":[{\"id\":\"e1\",\"page\":99,\"sourceText\":\"invented\"}]}");
        var service=new DocumentIntelligenceService(new HttpClient(handler) { BaseAddress=new Uri("http://127.0.0.1:8090/") },Config());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.AskAsync(new() { Question="Salary?",Context=[new("e1",2,"Salary: RM12,000")] },default));
    }
}
