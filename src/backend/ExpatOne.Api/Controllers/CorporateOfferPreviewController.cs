using System.Security.Claims;
using ExpatOne.Api.Services;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Controllers;

/// <summary>Preview only. Does not create an offer or wallet document.</summary>
[ApiController]
[Authorize]
[Route("api/corporate/offer-preview")]
public sealed class CorporateOfferPreviewController(
    ExpatOneDbContext db, IUserService users, IStorageService storage) : ControllerBase
{
    [HttpPost("companies/{companyId:guid}")]
    public async Task<IActionResult> Preview(Guid companyId, PreviewOfferRequest request)
    {
        var firebaseUid = User.FindFirstValue("firebase_uid");
        if (string.IsNullOrWhiteSpace(firebaseUid)) return Unauthorized();
        var user = await users.FindOrCreateByExternalIdentityAsync("firebase", firebaseUid,
            User.FindFirstValue(ClaimTypes.Email) ?? "", User.FindFirstValue(ClaimTypes.Name));
        var allowed = await db.CorporateMemberships.AnyAsync(m =>
            m.CompanyId == companyId && m.UserId == user.Id && m.Status == "Active" &&
            (m.Role == "Owner" || m.Role == "Admin" || m.Role == "HR"));
        if (!allowed) return Forbid();
        var text = request.RenderedText?.Trim() ?? "";
        if (text.Length < 50 || text.Length > 20000 ||
            text.Contains("{{", StringComparison.Ordinal))
            return BadRequest(new { message = "Complete all offer fields before PDF preview." });
        var company = await db.CorporateCompanies.AsNoTracking()
            .Where(x => x.Id == companyId).Select(x => x.Name).FirstOrDefaultAsync();
        if (company == null) return NotFound();
        var profile = await db.CorporateCompanyProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == companyId);
        var contact = profile == null ? null : string.Join(" | ",
            new[] { profile.AddressLine1, profile.AddressLine2, profile.City,
                profile.State, profile.PostalCode, profile.CountryCode,
                profile.ContactNumber, profile.ContactEmail, profile.Website }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        byte[]? logo = null;
        if (!string.IsNullOrWhiteSpace(profile?.LogoObjectKey))
        {
            await using var stream = await storage.DownloadFileAsync(profile.LogoObjectKey);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, HttpContext.RequestAborted);
            if (buffer.Length > 2_000_000)
                return BadRequest(new { message = "Company logo is too large." });
            logo = buffer.ToArray();
        }
        try
        {
            var pdf = CorporateOfferPdfRenderer.Render(text, company, contact, logo);
            Response.Headers.CacheControl = "no-store";
            return File(pdf, "application/pdf", "offer-preview.pdf");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { message = "Could not render preview PDF." });
        }
    }
}

public sealed record PreviewOfferRequest(string? RenderedText);
