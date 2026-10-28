using System.Security.Claims;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/family-occasions")]
public class FamilyOccasionsController(ExpatOneDbContext db, IUserService users) : ControllerBase
{
    private async Task<Guid?> CurrentUserId()
    {
        var uid = User.FindFirstValue("firebase_uid");
        if (string.IsNullOrWhiteSpace(uid)) return null;
        var user = await users.FindOrCreateByExternalIdentityAsync("firebase", uid,
            User.FindFirstValue(ClaimTypes.Email) ?? "", User.FindFirstValue(ClaimTypes.Name));
        return user.Id;
    }

    public sealed record OccasionInput(string Title, string Kind, DateTime OccasionDate,
        bool RepeatsAnnually, bool RemindersEnabled, int DaysBefore);

    private static string? Validate(OccasionInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 200) return "Invalid title.";
        if (input.Kind is not ("Birthday" or "Anniversary" or "Relocation" or "Custom")) return "Invalid kind.";
        if (input.DaysBefore is not (1 or 3 or 7 or 30)) return "Invalid reminder offset.";
        if (input.OccasionDate.Kind != DateTimeKind.Utc) return "Date must be UTC.";
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = await CurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(await db.FamilyOccasions.AsNoTracking().Where(o => o.UserId == userId)
            .OrderBy(o => o.OccasionDate).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OccasionInput input)
    {
        var error = Validate(input);
        if (error != null) return BadRequest(error);
        var userId = await CurrentUserId();
        var occasion = new FamilyOccasion
        {
            Id = Guid.NewGuid(), UserId = userId.Value, Title = input.Title.Trim(), Kind = input.Kind,
            OccasionDate = input.OccasionDate, RepeatsAnnually = input.RepeatsAnnually,
            RemindersEnabled = input.RemindersEnabled, DaysBefore = input.DaysBefore
        };
        db.FamilyOccasions.Add(occasion);
        await db.SaveChangesAsync();
        return Ok(occasion);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] OccasionInput input)
    {
        var error = Validate(input);
        if (error != null) return BadRequest(error);
        var userId = await CurrentUserId();
        var occasion = await db.FamilyOccasions.SingleOrDefaultAsync(o => o.Id == id && o.UserId == userId);
        if (occasion == null) return NotFound();
        occasion.Title = input.Title.Trim();
        occasion.Kind = input.Kind;
        occasion.OccasionDate = input.OccasionDate;
        occasion.RepeatsAnnually = input.RepeatsAnnually;
        occasion.RemindersEnabled = input.RemindersEnabled;
        occasion.DaysBefore = input.DaysBefore;
        await db.SaveChangesAsync();
        return Ok(occasion);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = await CurrentUserId();
        var occasion = await db.FamilyOccasions.SingleOrDefaultAsync(o => o.Id == id && o.UserId == userId);
        if (occasion == null) return NotFound();
        db.FamilyOccasions.Remove(occasion);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
