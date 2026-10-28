using ExpatOne.Api.Controllers;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ExpatOne.Tests;

public class NotificationInboxAuthorizationTests
{
    private static NotificationInboxController Create()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new NotificationInboxController(
            new ExpatOneDbContext(options), Mock.Of<IUserService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task MissingIdentityReturnsUnauthorized()
    {
        Assert.IsType<UnauthorizedResult>(await Create().List());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task InvalidLimitReturnsBadRequest(int limit)
    {
        Assert.IsType<BadRequestObjectResult>(await Create().List(limit));
    }
}
