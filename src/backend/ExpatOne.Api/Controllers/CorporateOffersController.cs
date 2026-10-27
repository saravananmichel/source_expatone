using System.Security.Claims;
using System.Security.Cryptography;
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
        if (request.EmployeeUserId == Guid.Empty)
            return BadRequest(new { message = "Employee ID is required." });
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

        CorporateOfferLetter? previous = null;
        if (request.ReplacesOfferId.HasValue)
        {
            previous = await db.CorporateOfferLetters.AsNoTracking().FirstOrDefaultAsync(x =>
                x.Id == request.ReplacesOfferId.Value && x.CompanyId == companyId &&
                x.EmployeeUserId == request.EmployeeUserId);
            if (previous == null) return NotFound(new { message = "Previous offer not found." });
            if (previous.Status != "Issued")
                return Conflict(new { message = "Only a pending issued offer can be replaced." });
        }
        var selectedBenefitIds = request.BenefitIds ?? Array.Empty<Guid>();
        if (selectedBenefitIds.Length > 30 || selectedBenefitIds.Distinct().Count() != selectedBenefitIds.Length)
            return BadRequest(new { message = "Choose up to 30 unique benefits." });
        var selectedBenefits = await db.CorporateBenefits.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive && selectedBenefitIds.Contains(x.Id))
            .ToListAsync(HttpContext.RequestAborted);
        if (selectedBenefits.Count != selectedBenefitIds.Length)
            return BadRequest(new { message = "One or more benefits are inactive or belong to another company." });
        byte[] pdf;
        try
        {
            var company = await db.CorporateCompanies.AsNoTracking()
                .Where(x => x.Id == companyId).Select(x => x.Name).SingleAsync();
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
            pdf = CorporateOfferPdfRenderer.Render(text, company, contact, logo,
                selectedBenefitIds.Select(id => selectedBenefits.Single(b => b.Id == id))
                    .Select(b => (b.Category, b.Name, b.Description)).ToList());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return BadRequest(new { message = "Unable to render offer PDF." }); }

        var documentId = Guid.NewGuid();
        string? objectKey = null;
        var persisted = false;
        try
        {
            objectKey = await pdfStorage.UploadAsync(companyId, request.EmployeeUserId, documentId,
                pdf, HttpContext.RequestAborted);
            await using var tx = await db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
            if (previous != null)
            {
                var replaced = await db.CorporateOfferLetters
                    .Where(x => x.Id == previous.Id && x.CompanyId == companyId &&
                        x.EmployeeUserId == request.EmployeeUserId && x.Status == "Issued")
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Status, "Superseded")
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow), HttpContext.RequestAborted);
                if (replaced != 1)
                {
                    // The upload already happened: remove it before returning a conflict.
                    await tx.RollbackAsync(HttpContext.RequestAborted);
                    await storage.DeleteFileAsync(objectKey!);
                    objectKey = null;
                    return Conflict(new { message = "Previous offer has already changed." });
                }
            }
            var offer = new CorporateOfferLetter
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                EmployeeUserId = request.EmployeeUserId,
                CreatedByUserId = uid,
                Status = "Issued",
                Version = previous == null ? 1 : previous.Version + 1,
                ReplacesOfferId = previous?.Id,
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
            for (var index = 0; index < selectedBenefitIds.Length; index++)
            {
                var benefit = selectedBenefits.Single(x => x.Id == selectedBenefitIds[index]);
                db.CorporateOfferBenefits.Add(new CorporateOfferBenefit
                {
                    OfferId = offer.Id, BenefitId = benefit.Id, Position = index,
                    CategorySnapshot = benefit.Category,
                    NameSnapshot = benefit.Name,
                    DescriptionSnapshot = benefit.Description
                });
            }
            // Persist the audit entry in the same DB save as the offer and wallet record.
            audit.Stage(documentId, uid, "corporate_offer_issued",
                metadata: JsonSerializer.Serialize(new { offerId = offer.Id, companyId }));
            if (previous?.DocumentId is Guid predecessorDocumentId)
                audit.Stage(predecessorDocumentId, uid, "corporate_offer_superseded",
                    metadata: JsonSerializer.Serialize(new {
                        oldOfferId = previous.Id, replacementOfferId = offer.Id,
                        companyId, replacementVersion = offer.Version
                    }));
            await db.SaveChangesAsync(HttpContext.RequestAborted);
            await tx.CommitAsync(HttpContext.RequestAborted);
            persisted = true;
            return Created($"/api/corporate/offers/{offer.Id}",
                new { offer.Id, offer.CompanyId, offer.EmployeeUserId,
                    offer.DocumentId, offer.Status, offer.IssuedAt,
                    offer.Version, offer.ReplacesOfferId });
        }
        catch
        {
            // A failed database write must not leave an untracked PDF in GCS.
            if (objectKey != null && !persisted)
            {
                try { await storage.DeleteFileAsync(objectKey); }
                catch { /* Preserve the original error; orphan cleanup needs monitoring. */ }
            }
            throw;
        }
    }

    [HttpPost("{offerId:guid}/signature")]
    [RequestSizeLimit(300000)]
    public async Task<IActionResult> Sign(Guid offerId, [FromBody] OfferSignatureRequest request)
    {
        var uid = await UserId();
        var offer = await db.CorporateOfferLetters.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == offerId && x.EmployeeUserId == uid);
        if (offer == null) return NotFound();
        if (offer.Status != "Issued" || offer.DocumentId == null)
            return Conflict(new { message = "Only an unanswered issued offer can be signed." });
        if (!request.Confirm)
            return BadRequest(new { message = "Explicit signature confirmation is required." });
        byte[] signature;
        try { signature = Convert.FromBase64String(request.SignaturePngBase64 ?? ""); }
        catch (FormatException) { return BadRequest(new { message = "Invalid signature image." }); }
        // The signature pad exports a fixed 480 x 200 PNG. Reject unexpected
        // dimensions before passing untrusted image data to the PDF renderer.
        if (signature.Length < 100 || signature.Length > 150000 ||
            !signature.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            signature.Length < 24 ||
            !signature.AsSpan(12, 4).SequenceEqual("IHDR"u8) ||
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(signature.AsSpan(16, 4)) != 480 ||
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(signature.AsSpan(20, 4)) != 200)
            return BadRequest(new { message = "Signature must be a 480 x 200 PNG under 150 KB." });

        var original = await db.Documents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == offer.DocumentId && x.UserId == uid);
        if (original == null) return NotFound();
        byte[] originalPdf;
        await using (var originalStream = await storage.DownloadFileAsync(original.S3ObjectKey))
        {
            using var buffer = new MemoryStream();
            await originalStream.CopyToAsync(buffer, HttpContext.RequestAborted);
            if (buffer.Length > 10_000_000)
                return BadRequest(new { message = "Original offer PDF exceeds signing size limit." });
            originalPdf = buffer.ToArray();
        }
        var originalPdfSha256 = Convert.ToHexString(SHA256.HashData(originalPdf));
        var text = JsonDocument.Parse(offer.TemplateDataJson).RootElement
            .GetProperty("text").GetString() ?? "";
        var company = await db.CorporateCompanies.AsNoTracking()
            .Where(x => x.Id == offer.CompanyId).Select(x => x.Name).SingleAsync();
        var profile = await db.CorporateCompanyProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyId == offer.CompanyId);
        var contact = profile == null ? null : string.Join(" | ",
            new[] { profile.AddressLine1, profile.AddressLine2, profile.City,
                profile.State, profile.PostalCode, profile.CountryCode,
                profile.ContactNumber, profile.ContactEmail, profile.Website }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        byte[]? logo = null;
        if (!string.IsNullOrWhiteSpace(profile?.LogoObjectKey))
        {
            await using var logoStream = await storage.DownloadFileAsync(profile.LogoObjectKey);
            using var logoBuffer = new MemoryStream();
            await logoStream.CopyToAsync(logoBuffer, HttpContext.RequestAborted);
            if (logoBuffer.Length > 2_000_000)
                return BadRequest(new { message = "Company logo is too large." });
            logo = logoBuffer.ToArray();
        }
        var benefits = await db.CorporateOfferBenefits.AsNoTracking()
            .Where(x => x.OfferId == offerId).OrderBy(x => x.Position)
            .Select(x => new { x.CategorySnapshot, x.NameSnapshot, x.DescriptionSnapshot })
            .ToListAsync();
        var signedAt = DateTime.UtcNow;
        byte[] signedPdf;
        try
        {
            signedPdf = CorporateOfferPdfRenderer.Render(text, company, contact, logo,
                benefits.Select(b => (b.CategorySnapshot, b.NameSnapshot, b.DescriptionSnapshot)).ToList(),
                signature, signedAt);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { return BadRequest(new { message = "Unable to render signed offer." }); }

        var signedPdfSha256 = Convert.ToHexString(SHA256.HashData(signedPdf));
        var signatureSha256 = Convert.ToHexString(SHA256.HashData(signature));
        string? signatureKey = null;
        string? signedPdfKey = null;
        try
        {
            var signingAttemptId = Guid.NewGuid();
            await using (var stream = new MemoryStream(signature))
                signatureKey = await storage.UploadFileAsync(stream, $"{signingAttemptId:N}-signature.png", "image/png",
                    $"corporate-offers/{offer.CompanyId}/{offerId}/signatures");
            await using (var stream = new MemoryStream(signedPdf))
                signedPdfKey = await storage.UploadFileAsync(stream, $"{signingAttemptId:N}-signed-offer.pdf", "application/pdf",
                    $"corporate-offers/{offer.CompanyId}/{offerId}/signed");
            var signedDocumentId = Guid.NewGuid();
            await using var tx = await db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
            var changed = await db.CorporateOfferLetters.Where(x => x.Id == offerId &&
                x.EmployeeUserId == uid && x.Status == "Issued")
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, "Accepted")
                    .SetProperty(x => x.Response, "Accepted")
                    .SetProperty(x => x.RespondedAt, signedAt)
                    .SetProperty(x => x.UpdatedAt, signedAt), HttpContext.RequestAborted);
            if (changed != 1)
            {
                await tx.RollbackAsync(HttpContext.RequestAborted);
                return Conflict(new { message = "Offer status changed while signing." });
            }
            db.Documents.Add(new Document
            {
                Id = signedDocumentId, UserId = uid,
                DocumentTypeId = original.DocumentTypeId,
                DocumentName = "Signed Employment Offer Letter",
                OriginalFileName = "signed-employment-offer.pdf",
                S3ObjectKey = signedPdfKey!, FileSizeBytes = signedPdf.Length,
                ContentType = "application/pdf", Status = DocumentStatus.Active,
                ExtractionStatus = "NOT_REQUESTED"
            });
            db.CorporateOfferSignatures.Add(new CorporateOfferSignature
            {
                Id = Guid.NewGuid(), OfferId = offerId, EmployeeUserId = uid,
                SignatureObjectKey = signatureKey!, SignedDocumentId = signedDocumentId,
                SignedAt = signedAt, OfferVersion = offer.Version
            });
            audit.Stage(signedDocumentId, uid, "corporate_offer_signed",
                metadata: JsonSerializer.Serialize(new { offerId, offer.Version,
                    originalDocumentId = offer.DocumentId, signedDocumentId, signedAt,
                    method = "Drawn", originalPdfSha256, signedPdfSha256,
                    signatureSha256 }));
            await db.SaveChangesAsync(HttpContext.RequestAborted);
            await tx.CommitAsync(HttpContext.RequestAborted);
            signatureKey = null;
            signedPdfKey = null;
            return Ok(new { offerId, status = "Accepted", signedDocumentId, signedAt,
                message = "Drawn signature recorded; legal enforceability depends on applicable law." });
        }
        finally
        {
            if (signatureKey != null)
                try { await storage.DeleteFileAsync(signatureKey); } catch { }
            if (signedPdfKey != null)
                try { await storage.DeleteFileAsync(signedPdfKey); } catch { }
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
                x.IssuedAt, x.RespondedAt, x.Response, x.ReplacesOfferId,
                SignedDocumentId = db.CorporateOfferSignatures.Where(sig => sig.OfferId == x.Id)
                    .Select(sig => sig.SignedDocumentId).FirstOrDefault() })
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
                x.DocumentId, x.IssuedAt, x.RespondedAt, x.Response, x.ReplacesOfferId })
            .ToListAsync();
        return Ok(offers);
    }

    [HttpPost("{offerId:guid}/respond")]
    public async Task<IActionResult> Respond(Guid offerId, [FromBody] OfferResponseRequest request)
    {
        if (request.Response == "Accepted")
            return Conflict(new { message = "Accept this offer using the Sign and accept action." });
        if (request.Response != "Declined")
            return BadRequest(new { message = "Response must be Declined." });
        var uid = await UserId();
        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        // Atomic transition: prevents double acknowledgements from concurrent requests.
        var changed = await db.CorporateOfferLetters
            .Where(x => x.Id == offerId && x.EmployeeUserId == uid &&
                x.Status == "Issued" && x.DocumentId != null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, request.Response)
                .SetProperty(x => x.Response, request.Response)
                .SetProperty(x => x.RespondedAt, now)
                .SetProperty(x => x.UpdatedAt, now), HttpContext.RequestAborted);
        if (changed == 0)
        {
            var exists = await db.CorporateOfferLetters.AsNoTracking()
                .AnyAsync(x => x.Id == offerId && x.EmployeeUserId == uid, HttpContext.RequestAborted);
            return exists
                ? Conflict(new { message = "Only an issued, unanswered offer can receive a response." })
                : NotFound();
        }
        var documentId = await db.CorporateOfferLetters.AsNoTracking()
            .Where(x => x.Id == offerId && x.EmployeeUserId == uid)
            .Select(x => x.DocumentId).SingleAsync(HttpContext.RequestAborted);
        audit.Stage(documentId!.Value, uid, "corporate_offer_responded",
            metadata: JsonSerializer.Serialize(new { offerId, response = request.Response }));
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        await tx.CommitAsync(HttpContext.RequestAborted);
        return Ok(new { id = offerId, status = request.Response,
            respondedAt = now, message = "Response recorded; this is not an electronic signature." });
    }
}

public sealed record OfferResponseRequest(string Response);

public sealed record IssueCorporateOfferRequest([Required] Guid EmployeeUserId, [Required] string RenderedText, Guid? ReplacesOfferId = null, Guid[]? BenefitIds = null);

public sealed record OfferSignatureRequest(string? SignaturePngBase64, bool Confirm);
