using System.Security.Claims;
using System.Text.Json;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/corporate/offer-drafts")]
public sealed class CorporateOfferDraftsController(ExpatOneDbContext db, IUserService users) : ControllerBase
{
    private async Task<Guid> UserId()
    {
        var uid = User.FindFirstValue("firebase_uid") ?? throw new UnauthorizedAccessException();
        var user = await users.FindOrCreateByExternalIdentityAsync("firebase", uid,
            User.FindFirstValue(ClaimTypes.Email) ?? "", User.FindFirstValue(ClaimTypes.Name));
        return user.Id;
    }

    private Task<bool> CanManage(Guid companyId, Guid uid) =>
        db.CorporateMemberships.AnyAsync(m => m.CompanyId == companyId &&
            m.UserId == uid && m.Status == "Active" &&
            (m.Role == "Owner" || m.Role == "Admin" || m.Role == "HR"));

    [HttpPost("companies/{companyId:guid}")]
    public async Task<IActionResult> Save(Guid companyId, SaveOfferDraftRequest request)
    {
        var uid = await UserId();
        if (!await CanManage(companyId, uid)) return Forbid();
        if (request.EmployeeUserId == Guid.Empty ||
            !await db.CorporateMemberships.AnyAsync(m => m.CompanyId == companyId &&
                m.UserId == request.EmployeeUserId && m.Status == "Active" &&
                (m.Role == "Employee" || m.Role == "Viewer")))
            return BadRequest(new { message = "Select an active employee." });
        var text = request.RenderedText?.Trim() ?? "";
        if (text.Length > 20000)
            return BadRequest(new { message = "Draft exceeds 20,000 characters." });

        CorporateOfferLetter offer;
        if (request.DraftId is Guid draftId)
        {
            var existing = await db.CorporateOfferLetters.FirstOrDefaultAsync(x =>
                x.Id == draftId && x.CompanyId == companyId &&
                x.EmployeeUserId == request.EmployeeUserId &&
                x.CreatedByUserId == uid && x.Status == "Draft");
            if (existing == null) return NotFound();
            offer = existing;
        }
        else
        {
            offer = new CorporateOfferLetter
            {
                Id = Guid.NewGuid(), CompanyId = companyId,
                EmployeeUserId = request.EmployeeUserId,
                CreatedByUserId = uid, Status = "Draft"
            };
            db.CorporateOfferLetters.Add(offer);
        }
        offer.TemplateDataJson = JsonSerializer.Serialize(new { text });
        offer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(new { offer.Id, offer.Status, offer.UpdatedAt });
    }

    [HttpGet("companies/{companyId:guid}")]
    public async Task<IActionResult> List(Guid companyId, [FromQuery] Guid employeeUserId)
    {
        var uid = await UserId();
        if (!await CanManage(companyId, uid)) return Forbid();
        return Ok(await db.CorporateOfferLetters.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.EmployeeUserId == employeeUserId &&
                x.CreatedByUserId == uid && x.Status == "Draft")
            .OrderByDescending(x => x.UpdatedAt).Take(30)
            .Select(x => new { x.Id, x.CreatedAt, x.UpdatedAt, x.TemplateDataJson })
            .ToListAsync(HttpContext.RequestAborted));
    }
}

public sealed record SaveOfferDraftRequest(Guid EmployeeUserId, string? RenderedText, Guid? DraftId = null);
