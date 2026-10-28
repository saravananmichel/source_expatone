using ExpatOne.Api.Controllers;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ExpatOne.Tests;

public class FamilyOccasionAuthorizationTests
{
    private static FamilyOccasionsController CreateController()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase("occasion-auth-" + Guid.NewGuid()).Options;
        var db = new ExpatOneDbContext(options);
        return new FamilyOccasionsController(db, Mock.Of<IUserService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task List_WithoutFirebaseIdentity_ReturnsUnauthorized()
    {
        var result = await CreateController().List();
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Create_WithoutFirebaseIdentity_ReturnsUnauthorized()
    {
        var input = new FamilyOccasionsController.OccasionInput(
            "Birthday", "Birthday", DateTime.UtcNow, true, true, 7);
        var result = await CreateController().Create(input);
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Update_WithoutFirebaseIdentity_ReturnsUnauthorized()
    {
        var input = new FamilyOccasionsController.OccasionInput(
            "Anniversary", "Anniversary", DateTime.UtcNow, true, true, 3);
        var result = await CreateController().Update(Guid.NewGuid(), input);
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Theory]
    [InlineData("", "Birthday", 7)]
    [InlineData("Valid", "Unknown", 7)]
    [InlineData("Valid", "Birthday", 2)]
    public async Task Create_InvalidOccasionInput_ReturnsBadRequest(
        string title, string kind, int daysBefore)
    {
        var input = new FamilyOccasionsController.OccasionInput(
            title, kind, DateTime.UtcNow, true, true, daysBefore);
        var result = await CreateController().Create(input);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_NonUtcOccasionDate_ReturnsBadRequest()
    {
        var input = new FamilyOccasionsController.OccasionInput(
            "Birthday", "Birthday", DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local),
            true, true, 7);
        var result = await CreateController().Create(input);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Delete_WithoutFirebaseIdentity_ReturnsUnauthorized()
    {
        var result = await CreateController().Delete(Guid.NewGuid());
        Assert.IsType<UnauthorizedResult>(result);
    }
}
