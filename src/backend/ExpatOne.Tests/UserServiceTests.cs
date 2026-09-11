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
    public async Task CreateAsync_ReturnsUserDto()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        var dto = new CreateUserDto
        {
            ExternalId = "firebase-123",
            Email = "user@example.com",
            DisplayName = "Test User"
        };

        var result = await service.CreateAsync(dto);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("user@example.com", result.Email);
        Assert.Equal("Test User", result.DisplayName);
        Assert.Equal("MY", result.CountryCode);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUser()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        var created = await service.CreateAsync(new CreateUserDto
        {
            ExternalId = "fb-456",
            Email = "find@example.com"
        });

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

        await service.CreateAsync(new CreateUserDto
        {
            ExternalId = "external-789",
            Email = "external@example.com"
        });

        var found = await service.GetByExternalIdAsync("external-789");

        Assert.NotNull(found);
        Assert.Equal("external@example.com", found.Email);
    }

    [Fact]
    public async Task GetByExternalIdAsync_ReturnsNullForMissing()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        var found = await service.GetByExternalIdAsync("does-not-exist");

        Assert.Null(found);
    }

    [Fact]
    public async Task CreateAsync_DoesNotExposeExternalId()
    {
        using var context = CreateDbContext();
        var service = new UserService(context);

        var result = await service.CreateAsync(new CreateUserDto
        {
            ExternalId = "secret-id",
            Email = "dto@example.com"
        });

        // UserDto should not contain ExternalId — verify the type
        var properties = typeof(UserDto).GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "ExternalId");
    }
}
