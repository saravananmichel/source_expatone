using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Moq;
namespace ExpatOne.Tests;

[Collection("Integration")]
public class DocumentIntelligenceTests(AppFactory factory)
{
    private class PrivateHandler : HttpMessageHandler
    {
        public List<Uri> Destinations=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal("127.0.0.1",request.RequestUri!.Host); Assert.Equal("/ask",request.RequestUri.AbsolutePath);
            Assert.Equal("synthetic-key",request.Headers.GetValues("X-Service-Key").Single());
            Destinations.Add(request.RequestUri);
            var payload=await request.Content!.ReadFromJsonAsync<DocumentAskRequestDto>(cancellationToken:ct);
            var source=payload!.Context.First();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content=JsonContent.Create(new DocumentAnswerDto {
                Answer="Salary: RM12,000 per month.",Grounded=true,Evidence=[new() { Id=source.Id,Page=source.Page,SourceText="Salary: RM12,000 per month." }] }) };
        }
    }
    [Theory]
    [InlineData("ready",200)][InlineData("missing",409)][InlineData("other_user",404)][InlineData("unauthorized",401)][InlineData("empty",400)]
    public async Task DocumentQARouteUsesPersistedLocalContextAndNeverGemini(string scenario,int expected)
    {
        var handler=new PrivateHandler(); var gemini=new Mock<IAIService>(MockBehavior.Strict);
        var jobs=new Mock<IDocumentAnalysisJobs>();
        jobs.Setup(x=>x.EnqueueAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<bool>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnalysisJobDto(Guid.NewGuid(),Guid.NewGuid(),null,"QUEUED","Queued",DateTime.UtcNow,null,null));
        using var host=factory.WithWebHostBuilder(builder=>builder.ConfigureServices(services=> {
            services.RemoveAll<DbContextOptions<ExpatOneDbContext>>();
            var name=Guid.NewGuid().ToString(); services.AddDbContext<ExpatOneDbContext>(o=>o.UseInMemoryDatabase(name));
            services.AddSingleton(gemini.Object); services.AddSingleton(jobs.Object);
            services.AddScoped<IDocumentAnalysisService,DocumentAnalysisService>();
            services.AddSingleton<IDocumentIntelligenceService>(new DocumentIntelligenceService(new HttpClient(handler) { BaseAddress=new Uri("http://127.0.0.1:8090/") },
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["DocumentIntelligence:ServiceKey"]="synthetic-key" }).Build()));
            services.AddAuthentication(o=> { o.DefaultAuthenticateScheme="Test";o.DefaultChallengeScheme="Test"; })
                .AddScheme<AuthenticationSchemeOptions,TestAuthHandler>("Test",null);
        }));
        var client=host.CreateClient(); var id=Guid.NewGuid();
        using(var scope=host.Services.CreateScope()) {
            var users=scope.ServiceProvider.GetRequiredService<IUserService>();
            var owner=await users.FindOrCreateByExternalIdentityAsync("firebase","local-doc-owner","owner@example.test","Owner");
            var db=scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
            var doc=new Document { Id=id,UserId=owner.Id,DocumentTypeId=Guid.Parse("10000000-0000-0000-0000-000000000001"),DocumentName="Synthetic",
                S3ObjectKey="synthetic/current.pdf",ContentType="application/pdf" };
            db.Documents.Add(doc);
            if(scenario!="missing") db.DocumentAnalysisRuns.Add(DocumentAnalysisServiceTests.Run(doc));
            await db.SaveChangesAsync();
        }
        if(scenario!="unauthorized") client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Test",
            scenario=="other_user"?"uid=other-local-owner&email=other@example.test&name=Other":"uid=local-doc-owner&email=owner@example.test&name=Owner");
        var response=await client.PostAsJsonAsync($"/api/documents/{id}/ask",new { question=scenario=="empty"?"":"What is the salary?" });
        Assert.Equal((HttpStatusCode)expected,response.StatusCode);
        if(expected==200) { Assert.DoesNotContain("qualityDiagnostics", await response.Content.ReadAsStringAsync()); var answer=await response.Content.ReadFromJsonAsync<DocumentAnswerDto>(); Assert.True(answer!.Grounded);Assert.Equal(2,answer.Evidence.Single().Page);Assert.Single(handler.Destinations); }
        else Assert.Empty(handler.Destinations);
        gemini.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task MissingJobInfrastructureReturns503EvenWithLegacyServiceAndGeminiRegistered()
    {
        var legacy=new Mock<IDocumentAnalysisService>(MockBehavior.Strict); var gemini=new Mock<IAIService>(MockBehavior.Strict);
        using var host=factory.WithWebHostBuilder(builder=>builder.ConfigureServices(services=> {
            services.RemoveAll<IDocumentAnalysisJobs>();services.AddSingleton(legacy.Object);services.AddSingleton(gemini.Object);
            services.AddAuthentication(o=> {o.DefaultAuthenticateScheme="Test";o.DefaultChallengeScheme="Test";})
                .AddScheme<AuthenticationSchemeOptions,TestAuthHandler>("Test",null);
        }));
        var client=host.CreateClient();client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Test","uid=no-jobs-owner&email=owner@example.test&name=Owner");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.PostAsJsonAsync($"/api/documents/{Guid.NewGuid()}/analyze",new {})).StatusCode);
        legacy.VerifyNoOtherCalls();gemini.VerifyNoOtherCalls();
    }
}

[Collection("Integration")]
public class DocumentLocalRegistrationTests(AppFactory factory)
{
    [Fact]
    public void ProgramRegistersDocumentQAEvenWhenGeminiIsNotConfigured()
    {
        using var host=factory.WithWebHostBuilder(builder=> {
            builder.UseEnvironment("Testing"); // Do not load developer user-secrets.
            builder.UseSetting("DocumentIntelligence:Enabled", "true");
            builder.UseSetting("Aws:Region", "ap-southeast-5");
            builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?> {
                ["Gemini:ApiKey"]="", ["Aws:Region"]="ap-southeast-5", ["DocumentIntelligence:Enabled"]="true",
                ["DocumentIntelligence:Provider"]="Local", ["DocumentIntelligence:EnableFallback"]="false",
                ["DocumentIntelligence:ServiceUrl"]="http://127.0.0.1:8090/" }));
            builder.ConfigureServices(services=> {
                // No background work or AWS storage access in this registration test.
                foreach(var descriptor in services.Where(d=>d.ImplementationType==typeof(ExpatOne.Api.Services.DocumentIntelligenceWorker)).ToList())
                    services.Remove(descriptor);
                services.AddSingleton(new Mock<IStorageService>(MockBehavior.Strict).Object);
            });
        });
        using var scope=host.Services.CreateScope();
        Assert.Null(scope.ServiceProvider.GetService<IAIService>());
        Assert.IsType<DocumentAnalysisService>(scope.ServiceProvider.GetRequiredService<IDocumentAnalysisService>());
        Assert.IsType<DocumentIntelligenceService>(scope.ServiceProvider.GetRequiredService<IDocumentIntelligenceService>());
        Assert.IsType<DocumentAnalysisJobs>(scope.ServiceProvider.GetRequiredService<IDocumentAnalysisJobs>());
    }
}
