using ExpatOne.Api.Services.Destinations;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/destinations")]
public sealed class DestinationsController(ExpatOneDbContext db, DestinationCatalog catalog,
    DestinationWeatherService weather) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var countries = await db.Countries.AsNoTracking().Where(c => c.IsActive)
            .OrderBy(c => c.Name).Select(c => new { c.Id, c.Code, c.Name }).ToListAsync(ct);
        return Ok(countries.Select(c =>
        {
            var code = c.Code.ToUpperInvariant();
            var place = catalog.Find(code);
            return new { c.Id, Code = code, c.Name, WeatherCity = place?.City,
                Landmark = place?.Landmark, BannerImageUrl = catalog.ImageUrl(code) };
        }));
    }

    [HttpGet("{code}/weather")]
    public async Task<IActionResult> GetWeather(string code, CancellationToken ct)
    {
        code = code.Trim().ToUpperInvariant();
        if (code.Length != 2 || code.Any(c => c < 'A' || c > 'Z')) return BadRequest();
        if (!await db.Countries.AsNoTracking().AnyAsync(c => c.Code == code && c.IsActive, ct))
            return NotFound(new { message = "Destination is unavailable" });
        var place = catalog.Find(code);
        if (place is null) return Ok(new DestinationWeather(code, "", false, null, null, null, null, null));
        return Ok(await weather.GetAsync(code, place, ct));
    }
}
