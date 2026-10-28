using ExpatOne.Api.Controllers;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ExpatOne.Tests;

public class UnifiedReminderAuthorizationTests
{
    private static UnifiedRemindersController CreateController()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase("unified-auth-" + Guid.NewGuid()).Options;
        return new UnifiedRemindersController(
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
        var result = await CreateController().Get();
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task Get_InvalidDayRange_ReturnsBadRequest(int days)
    {
        var result = await CreateController().Get(days: days);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Get_UnknownCategory_ReturnsBadRequest()
    {
        var result = await CreateController().Get(category: "unknown");
        Assert.IsType<BadRequestObjectResult>(result);
    }
}
