using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Api.Services;
using ExpatOne.Infrastructure.Services;
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
public sealed class CorporateOffersController(ExpatOneDbContext db, IUserService users,
    CorporateOfferPdfStorage pdfStorage, IStorageService storage,
    IDocumentAuditService audit) : ControllerBase
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

    [HttpPost("companies/{companyId:guid}/issue")]
    public async Task<IActionResult> Issue(Guid companyId, [FromBody] IssueCorporateOfferRequest request)
    {
        var uid = await UserId();
        var allowed = await db.CorporateMemberships.AnyAsync(m =>
            m.CompanyId == companyId && m.UserId == uid && m.Status == "Active" &&
            (m.Role == "Admin" || m.Role == "Owner" || m.Role == "HR"));
        if (!allowed) return Forbid();
        var employee = await db.CorporateMemberships.AnyAsync(m =>
            m.CompanyId == companyId && m.UserId == request.EmployeeUserId &&
            m.Status == "Active" && (m.Role == "Employee" || m.Role == "Viewer"));
        if (!employee) return BadRequest(new { message = "Employee must be an active company employee." });
        var text = request.RenderedText?.Trim() ?? "";
        if (text.Length < 50 || text.Length > 20000 || text.Contains("{{", StringComparison.Ordinal))
            return BadRequest(new { message = "Provide a complete reviewed offer (50–20,000 characters) with no unresolved placeholders." });
        var documentTypeId = await db.DocumentTypes.AsNoTracking()
            .Where(x => x.Name == "Employment Contract")
            .Select(x => x.Id).FirstOrDefaultAsync();
        if (documentTypeId == Guid.Empty)
            return StatusCode(503, new { message = "Employment Contract document type is not configured." });

        byte[] pdf;
        try { pdf = CorporateOfferPdfRenderer.Render(text); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return BadRequest(new { message = "Unable to render offer PDF." }); }

        var documentId = Guid.NewGuid();
        string? objectKey = null;
        try
        {
            objectKey = await pdfStorage.UploadAsync(companyId, request.EmployeeUserId, documentId,
                pdf, HttpContext.RequestAborted);
            var offer = new CorporateOfferLetter
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                EmployeeUserId = request.EmployeeUserId,
                CreatedByUserId = uid,
                Status = "Issued",
                Version = 1,
                TemplateDataJson = JsonSerializer.Serialize(new { text }),
                DocumentId = documentId,
                IssuedAt = DateTime.UtcNow
            };
            var walletDocument = new Document
            {
                Id = documentId,
                UserId = request.EmployeeUserId,
                DocumentTypeId = documentTypeId,
                DocumentName = "Employment Offer Letter",
                OriginalFileName = "employment-offer.pdf",
                S3ObjectKey = objectKey,
                FileSizeBytes = pdf.Length,
                ContentType = "application/pdf",
                Status = DocumentStatus.Active,
                ExtractionStatus = "NOT_REQUESTED"
            };
            db.Documents.Add(walletDocument);
            db.CorporateOfferLetters.Add(offer);
            await db.SaveChangesAsync(HttpContext.RequestAborted);
            await audit.LogAsync(documentId, uid, "corporate_offer_issued");
            return Created($"/api/corporate/offers/{offer.Id}",
                new { offer.Id, offer.CompanyId, offer.EmployeeUserId,
                    offer.DocumentId, offer.Status, offer.IssuedAt });
        }
        catch
        {
            // A failed database write must not leave an untracked PDF in GCS.
            if (objectKey != null)
            {
                try { await storage.DeleteFileAsync(objectKey); }
                catch { /* Preserve the original error; orphan cleanup needs monitoring. */ }
            }
            throw;
        }
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

public sealed record IssueCorporateOfferRequest([Required] Guid EmployeeUserId, [Required] string RenderedText);
