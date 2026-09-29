using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExpatOne.Tests;

// Shared WebApplicationFactory used by all integration test classes.
//
// xUnit creates one instance of this fixture and shares it across every test
// class in the [Collection("Integration")] collection. That means Program.Main
// (and FirebaseApp.Create) runs exactly once per test run, eliminating the
// intermittent "FirebaseApp already exists" race that occurred when each
// IClassFixture<WebApplicationFactory<Program>> spun up its own app host.
//
// Production authentication is unchanged: the TestAuthHandler scheme is only
// registered in per-test WithWebHostBuilder overrides, never in production.
public class AppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // Replace PostgreSQL with in-memory by default so the factory boots without
        // a real database. Individual test classes override this via WithWebHostBuilder.
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ExpatOneDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<ExpatOneDbContext>(options =>
                options.UseInMemoryDatabase("SharedTestDb"));
        });
    }
}

// One collection definition — all integration test classes opt in with [Collection("Integration")].
// xUnit serialises construction of collection fixtures, so AppFactory.Start() is called once.
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<AppFactory> { }
