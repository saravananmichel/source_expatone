using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace ExpatOne.Tests;

[Collection("Integration")]
public class DocumentIntelligenceEndpointTests(AppFactory factory)
{
    [Fact]
    public async Task AsyncRoutesQueueAuthorizeAndReturnHistory()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(x => x.ServiceType == typeof(DbContextOptions<ExpatOneDbContext>));
            services.Remove(descriptor);
            var dbName = Guid.NewGuid().ToString();
            services.AddDbContext<ExpatOneDbContext>(options => options.UseInMemoryDatabase(dbName));
            var storage = new Moq.Mock<IStorageService>();
            storage.Setup(x => x.DownloadFileAsync(Moq.It.IsAny<string>())).ReturnsAsync(() => (Stream)new MemoryStream([1, 2, 3]));
            services.AddSingleton(storage.Object);
            services.AddScoped<IDocumentAnalysisJobs, DocumentAnalysisJobs>();
            services.AddAuthentication(options => {
                options.DefaultAuthenticateScheme = "Test"; options.DefaultChallengeScheme = "Test";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);
        }));
        var client = host.CreateClient();
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/documents/{id}/analysis/status")).StatusCode);
        using (var scope = host.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<IUserService>();
            var owner = await users.FindOrCreateByExternalIdentityAsync("firebase", "engine-owner", "owner@example.test", "Owner");
            var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
            db.Documents.Add(new Document { Id = id, UserId = owner.Id, DocumentTypeId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                DocumentName = "Synthetic", S3ObjectKey = "synthetic.pdf", ContentType = "application/pdf" });
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "uid=engine-owner&email=owner@example.test&name=Owner");
        var response = await client.PostAsJsonAsync($"/api/documents/{id}/analyze", new { forceReanalyze = false });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<AnalysisJobDto>();
        Assert.Equal("QUEUED", job!.Status);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/documents/{id}/analysis/{job.AnalysisId}")).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<List<AnalysisJobDto>>($"/api/documents/{id}/analysis/history"))!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test", "uid=other-engine-user&email=other@example.test&name=Other");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/documents/{id}/analysis/{job.AnalysisId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/documents/{id}/reanalyze", new {})).StatusCode);
    }
}
