using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Controllers;

public sealed record SaveCompanyProfileRequest(
    [MaxLength(250)] string? AddressLine1,
    [MaxLength(250)] string? AddressLine2,
    [MaxLength(120)] string? City,
    [MaxLength(120)] string? State,
    [MaxLength(30)] string? PostalCode,
    [MaxLength(2)] string? CountryCode,
    [MaxLength(40)] string? ContactNumber,
    [EmailAddress, MaxLength(254)] string? ContactEmail,
    [MaxLength(300)] string? Website);

public sealed record SaveOfferTemplateRequest([Required] JsonElement Template);

public sealed record SaveCompanyBenefitRequest(
    [Required, MaxLength(80)] string Category,
    [Required, MaxLength(160)] string Name,
    [MaxLength(4000)] string? Description,
    bool IsActive = true);

[ApiController]
[Authorize]
[Route("api/corporate/companies/{companyId:guid}/settings")]
[EnableRateLimiting("family-per-user")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CorporateSettingsController(ExpatOneDbContext db, IUserService users) : ControllerBase
{
    private async Task<Guid> UserId()
    {
        var firebaseUid = User.FindFirstValue("firebase_uid")
            ?? throw new UnauthorizedAccessException();
        var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
        var name = User.FindFirstValue(ClaimTypes.Name);
        return (await users.FindOrCreateByExternalIdentityAsync("firebase", firebaseUid, email, name)).Id;
    }

    private Task<bool> CanManage(Guid companyId, Guid userId) =>
        db.CorporateMemberships.AnyAsync(x => x.CompanyId == companyId &&
            x.UserId == userId && x.Status == "Active" &&
            (x.Role == "Owner" || x.Role == "Admin" || x.Role == "HR"));

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(Guid companyId)
    {
        if (!await CanManage(companyId, await UserId())) return Forbid();
        var result = await db.CorporateCompanyProfiles.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new { x.CompanyId, x.AddressLine1, x.AddressLine2,
                x.City, x.State, x.PostalCode, x.CountryCode, x.ContactNumber,
                x.ContactEmail, x.Website, x.TemplateVersion, HasLogo = x.LogoObjectKey != null })
            .FirstOrDefaultAsync();
        return Ok(result);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> SaveProfile(Guid companyId, SaveCompanyProfileRequest request)
    {
        if (!await CanManage(companyId, await UserId())) return Forbid();
        if (!await db.CorporateCompanies.AnyAsync(x => x.Id == companyId)) return NotFound();
        var profile = await db.CorporateCompanyProfiles.FindAsync(companyId);
        if (profile == null)
        {
            profile = new ExpatOne.Domain.Entities.CorporateCompanyProfile { CompanyId = companyId };
            db.CorporateCompanyProfiles.Add(profile);
        }
        profile.AddressLine1 = request.AddressLine1?.Trim();
        profile.AddressLine2 = request.AddressLine2?.Trim();
        profile.City = request.City?.Trim();
        profile.State = request.State?.Trim();
        profile.PostalCode = request.PostalCode?.Trim();
        profile.CountryCode = request.CountryCode?.Trim().ToUpperInvariant();
        if (profile.CountryCode is { Length: > 0 } && profile.CountryCode.Length != 2)
            return BadRequest(new { message = "Country code must have two letters." });
        profile.ContactNumber = request.ContactNumber?.Trim();
        profile.ContactEmail = request.ContactEmail?.Trim();
        profile.Website = request.Website?.Trim();
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { profile.CompanyId });
    }

    [HttpGet("offer-template")]
    public async Task<IActionResult> OfferTemplate(Guid companyId)
    {
        if (!await CanManage(companyId, await UserId())) return Forbid();
        var profile = await db.CorporateCompanyProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId);
        if (profile == null) return Ok(new { version = 0, template = new { } });
        return Ok(new
        {
            version = profile.TemplateVersion,
            template = JsonSerializer.Deserialize<JsonElement>(profile.OfferTemplateJson)
        });
    }

    [HttpPut("offer-template")]
    public async Task<IActionResult> SaveOfferTemplate(Guid companyId, SaveOfferTemplateRequest request)
    {
        if (!await CanManage(companyId, await UserId())) return Forbid();
        if (!await db.CorporateCompanies.AnyAsync(x => x.Id == companyId)) return NotFound();
        if (request.Template.ValueKind != JsonValueKind.Object)
            return BadRequest(new { message = "Offer template must be a JSON object." });
        var raw = request.Template.GetRawText();
        if (raw.Length > 20000)
            return BadRequest(new { message = "Template exceeds 20,000 characters." });
        // Only a small, explicit schema is accepted. Never interpret template JSON as executable code.
        if (!request.Template.TryGetProperty("body", out var body) ||
            body.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(body.GetString()))
            return BadRequest(new { message = "Template must contain a nonempty body string." });
        var profile = await db.CorporateCompanyProfiles.FindAsync(companyId);
        if (profile == null)
        {
            profile = new ExpatOne.Domain.Entities.CorporateCompanyProfile
            {
                CompanyId = companyId,
                OfferTemplateJson = raw,
                TemplateVersion = 1
            };
            db.CorporateCompanyProfiles.Add(profile);
        }
        else
        {
            profile.OfferTemplateJson = raw;
            profile.TemplateVersion += 1;
            profile.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return Ok(new { profile.CompanyId, profile.TemplateVersion });
    }

    [HttpGet("benefits")]
    public async Task<IActionResult> Benefits(Guid companyId, [FromQuery] bool includeInactive = false)
    {
        if (!await CanManage(companyId, await UserId())) return Forbid();
        var query = db.CorporateBenefits.AsNoTracking().Where(x => x.CompanyId == companyId);
        if (!includeInactive) query = query.Where(x => x.IsActive);
        return Ok(await query.OrderBy(x => x.Category).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Category, x.Name, x.Description, x.IsActive }).ToListAsync());
    }

    [HttpPost("benefits")]
    public async Task<IActionResult> CreateBenefit(Guid companyId, SaveCompanyBenefitRequest request)
    {
        if (!await CanManage(companyId, await UserId())) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Category))
            return BadRequest(new { message = "Name and category are required." });
        if (!await db.CorporateCompanies.AnyAsync(x => x.Id == companyId)) return NotFound();
        var benefit = new ExpatOne.Domain.Entities.CorporateBenefit
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Category = request.Category.Trim(),
            Name = request.Name.Trim(), Description = request.Description?.Trim(), IsActive = request.IsActive
        };
        db.CorporateBenefits.Add(benefit);
        await db.SaveChangesAsync();
        return Created($"/api/corporate/companies/{companyId}/settings/benefits/{benefit.Id}",
            new { benefit.Id, benefit.Category, benefit.Name, benefit.Description, benefit.IsActive });
    }

    [HttpPut("benefits/{benefitId:guid}")]
    public async Task<IActionResult> UpdateBenefit(Guid companyId, Guid benefitId, SaveCompanyBenefitRequest request)
    {
        if (!await CanManage(companyId, await UserId())) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Category))
            return BadRequest(new { message = "Name and category are required." });
        var benefit = await db.CorporateBenefits.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == benefitId);
        if (benefit == null) return NotFound();
        benefit.Category = request.Category.Trim();
        benefit.Name = request.Name.Trim();
        benefit.Description = request.Description?.Trim();
        benefit.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return Ok(new { benefit.Id, benefit.Category, benefit.Name, benefit.Description, benefit.IsActive });
    }

    // Soft-delete: preserve existing offer references and audit history.
    [HttpDelete("benefits/{benefitId:guid}")]
    public async Task<IActionResult> DeactivateBenefit(Guid companyId, Guid benefitId)
    {
        if (!await CanManage(companyId, await UserId())) return Forbid();
        var benefit = await db.CorporateBenefits.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.Id == benefitId);
        if (benefit == null) return NotFound();
        benefit.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("signatories")]
    public async Task<IActionResult> Signatories(Guid companyId)
    {
        if (!await CanManage(companyId, await UserId())) return Forbid();
        return Ok(await db.CorporateMemberships.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Status == "Active" &&
                (x.Role == "Owner" || x.Role == "Admin" || x.Role == "HR"))
            .Join(db.Users, m => m.UserId, u => u.Id,
                (m, u) => new { UserId = u.Id, Name = u.DisplayName, Title = m.Role })
            .OrderBy(x => x.Name).ToListAsync());
    }
}
