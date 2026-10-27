using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/corporate/offer-catalog")]
public sealed class CorporateOfferCatalogController(ExpatOneDbContext db) : ControllerBase
{
    [HttpGet("countries")]
    public async Task<IActionResult> Countries()
        => Ok(await db.Countries.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Code, x.Name }).ToListAsync());

    [HttpGet("currencies")]
    public async Task<IActionResult> Currencies()
        => Ok(await db.Currencies.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new { x.Code, x.Name, x.MinorUnits }).ToListAsync());

    [HttpGet("employment-types")]
    public IActionResult EmploymentTypes() => Ok(new[]
    {
        "Permanent", "Fixed-term Contract", "Temporary", "Part-time", "Internship", "Consultant"
    });

    [HttpGet("pay-frequencies")]
    public IActionResult PayFrequencies() => Ok(new[] { "Weekly", "Bi-Weekly", "Monthly" });
}
