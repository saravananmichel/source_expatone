using System.Security.Claims;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/corporate/offers")]
[EnableRateLimiting("family-per-user")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CorporateOffersController(ExpatOneDbContext db, IUserService users) : ControllerBase
{
    private async Task<Guid> UserId()
    {
        var firebaseUid = User.FindFirstValue("firebase_uid")
            ?? throw new UnauthorizedAccessException();
        var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
        var name = User.FindFirstValue(ClaimTypes.Name);
        var user = await users.FindOrCreateByExternalIdentityAsync("firebase", firebaseUid, email, name);
        return user.Id;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        var uid = await UserId();
        var offers = await db.CorporateOfferLetters.AsNoTracking()
            .Where(x => x.EmployeeUserId == uid && x.Status != "Draft")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.CompanyId, x.Version, x.Status, x.DocumentId,
                x.IssuedAt, x.RespondedAt, x.Response })
            .ToListAsync();
        return Ok(offers);
    }

    [HttpGet("companies/{companyId:guid}")]
    public async Task<IActionResult> CompanyOffers(Guid companyId)
    {
        var uid = await UserId();
        var allowed = await db.CorporateMemberships.AnyAsync(m =>
            m.CompanyId == companyId && m.UserId == uid && m.Status == "Active" &&
            (m.Role == "Admin" || m.Role == "Owner" || m.Role == "HR"));
        if (!allowed) return Forbid();
        var offers = await db.CorporateOfferLetters.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.CreatedAt).Take(200)
            .Select(x => new { x.Id, x.EmployeeUserId, x.Version, x.Status,
                x.DocumentId, x.IssuedAt, x.RespondedAt, x.Response })
            .ToListAsync();
        return Ok(offers);
    }

    [HttpPost("{offerId:guid}/respond")]
    public async Task<IActionResult> Respond(Guid offerId, [FromBody] OfferResponseRequest request)
    {
        if (request.Response is not ("Accepted" or "Declined"))
            return BadRequest(new { message = "Response must be Accepted or Declined." });
        var uid = await UserId();
        var now = DateTime.UtcNow;
        // Atomic transition: prevents double acknowledgements from concurrent requests.
        var changed = await db.CorporateOfferLetters
            .Where(x => x.Id == offerId && x.EmployeeUserId == uid &&
                x.Status == "Issued" && x.DocumentId != null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, request.Response)
                .SetProperty(x => x.Response, request.Response)
                .SetProperty(x => x.RespondedAt, now)
                .SetProperty(x => x.UpdatedAt, now));
        if (changed == 0)
        {
            var exists = await db.CorporateOfferLetters.AsNoTracking()
                .AnyAsync(x => x.Id == offerId && x.EmployeeUserId == uid);
            return exists
                ? Conflict(new { message = "Only an issued, unanswered offer can receive a response." })
                : NotFound();
        }
        return Ok(new { id = offerId, status = request.Response,
            respondedAt = now, message = "Response recorded; this is not an electronic signature." });
    }
}

public sealed record OfferResponseRequest(string Response);
