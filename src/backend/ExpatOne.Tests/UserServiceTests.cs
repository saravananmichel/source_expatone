using ExpatOne.Application.DTOs;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Tests;

public class UserServiceTests
{
    private ExpatOneDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ExpatOneDbContext(options);
    }

    [Fact]
    public async Task FindOrCreate_CreatesNewUser()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        var result = await service.FindOrCreateByExternalIdentityAsync(
            "firebase", "uid-123", "user@example.com", "Test User");

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("user@example.com", result.Email);
        Assert.Equal("Test User", result.DisplayName);
        Assert.Equal("firebase", result.ExternalProvider);
        Assert.Equal("MY", result.CountryCode);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task FindOrCreate_ReturnsExistingUser()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        var first = await service.FindOrCreateByExternalIdentityAsync(
            "firebase", "uid-456", "first@example.com", "First");

        var second = await service.FindOrCreateByExternalIdentityAsync(
            "firebase", "uid-456", "first@example.com", "First");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(context.Users);
    }

    [Fact]
    public async Task FindOrCreate_UpdatesEmailIfChanged()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        await service.FindOrCreateByExternalIdentityAsync(
            "firebase", "uid-789", "old@example.com", "User");

        var updated = await service.FindOrCreateByExternalIdentityAsync(
            "firebase", "uid-789", "new@example.com", "User");

        Assert.Equal("new@example.com", updated.Email);
        Assert.Single(context.Users);
    }

    [Fact]
    public async Task FindOrCreate_DifferentProvidersDifferentUsers()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        var firebase = await service.FindOrCreateByExternalIdentityAsync(
            "firebase", "uid-same", "fb@example.com", "FB User");

        var google = await service.FindOrCreateByExternalIdentityAsync(
            "google", "uid-same", "g@example.com", "Google User");

        Assert.NotEqual(firebase.Id, google.Id);
        Assert.Equal(2, context.Users.Count());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUser()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        var created = await service.FindOrCreateByExternalIdentityAsync(
            "firebase", "uid-get", "get@example.com", "Get User");

        var found = await service.GetByIdAsync(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForMissing()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        var found = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public async Task GetByExternalIdAsync_ReturnsUser()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        await service.FindOrCreateByExternalIdentityAsync(
            "firebase", "ext-lookup", "lookup@example.com", null);

        var found = await service.GetByExternalIdAsync("firebase", "ext-lookup");

        Assert.NotNull(found);
        Assert.Equal("lookup@example.com", found.Email);
    }

    [Fact]
    public async Task GetByExternalIdAsync_ReturnsNullForMissing()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        var found = await service.GetByExternalIdAsync("firebase", "does-not-exist");

        Assert.Null(found);
    }

    [Fact]
    public async Task UpdateProfile_UpdatesAllowedFields()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        var user = await service.FindOrCreateByExternalIdentityAsync(
            "firebase", "uid-update", "update@example.com", "Original");

        var updated = await service.UpdateProfileAsync(user.Id, new UpdateUserDto
        {
            DisplayName = "Updated Name",
            PhoneNumber = "+60123456789",
            CountryCode = "SG",
            PreferredLanguage = "ms"
        });

        Assert.Equal("Updated Name", updated.DisplayName);
        Assert.Equal("+60123456789", updated.PhoneNumber);
        Assert.Equal("SG", updated.CountryCode);
        Assert.Equal("ms", updated.PreferredLanguage);
        Assert.Equal(user.Email, updated.Email);
    }

    [Fact]
    public async Task UpdateProfile_ThrowsForMissingUser()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.UpdateProfileAsync(Guid.NewGuid(), new UpdateUserDto { DisplayName = "x" }));
    }

    [Fact]
    public void UserDto_DoesNotExposeExternalId()
    {
        var properties = typeof(UserDto).GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "ExternalId");
    }
}
