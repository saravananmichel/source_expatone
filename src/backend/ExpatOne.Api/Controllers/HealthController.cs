using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ExpatOneDbContext _dbContext;

    public HealthController(ExpatOneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        bool databaseConnected;
        try
        {
            databaseConnected = await _dbContext.Database.CanConnectAsync();
        }
        catch
        {
            databaseConnected = false;
        }

        var status = databaseConnected ? "ok" : "degraded";

        return Ok(new { status });
    }
}
