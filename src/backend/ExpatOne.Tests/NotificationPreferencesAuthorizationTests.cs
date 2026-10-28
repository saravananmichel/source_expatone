using ExpatOne.Api.Controllers;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ExpatOne.Tests;

public class NotificationPreferencesAuthorizationTests
{
    private static NotificationPreferencesController CreateController()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase("notification-preferences-" + Guid.NewGuid()).Options;
        return new NotificationPreferencesController(
            new ExpatOneDbContext(options), Mock.Of<IUserService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task Get_WithoutFirebaseIdentity_ReturnsUnauthorized()
    {
        Assert.IsType<UnauthorizedResult>(await CreateController().Get());
    }

    [Fact]
    public async Task Put_WithoutFirebaseIdentity_ReturnsUnauthorized()
    {
        var input = new NotificationPreferencesController.Input(
            true, false, true, true, true, true, true, true);
        Assert.IsType<UnauthorizedResult>(await CreateController().Put(input));
    }
}
