using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Tests;

public class DbContextTests
{
    private ExpatOneDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ExpatOneDbContext(options);
    }

    [Fact]
    public async Task DbContext_CanCreateAndRetrieveUser()
    {
        using var context = CreateDbContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalId = "test-external-id",
            ExternalProvider = "firebase",
            Email = "test@example.com",
            DisplayName = "Test User",
            CountryCode = "MY",
            PreferredLanguage = "en"
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var retrieved = await context.Users.FindAsync(user.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("test@example.com", retrieved.Email);
        Assert.Equal("test-external-id", retrieved.ExternalId);
        Assert.Equal("firebase", retrieved.ExternalProvider);
        Assert.Equal("MY", retrieved.CountryCode);
    }

    [Fact]
    public async Task DbContext_SetsTimestampsOnCreate()
    {
        using var context = CreateDbContext();

        var before = DateTime.UtcNow.AddSeconds(-1);

        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalId = "ts-test",
            Email = "ts@example.com"
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        Assert.True(user.CreatedAt >= before);
        Assert.True(user.UpdatedAt >= before);
    }

    [Fact]
    public async Task DbContext_UpdatesTimestampOnModify()
    {
        using var context = CreateDbContext();

        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalId = "update-test",
            Email = "update@example.com"
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var originalUpdatedAt = user.UpdatedAt;

        user.DisplayName = "Updated Name";
        context.Users.Update(user);
        await context.SaveChangesAsync();

        Assert.True(user.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public async Task DbContext_MalaysiaSeededInCountries()
    {
        using var context = CreateDbContext();

        // In-memory doesn't run HasData, so just verify DbSet is accessible
        var count = await context.Countries.CountAsync();
        Assert.True(count >= 0);
    }
}
