using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Controllers;

public sealed record RecordIssuedPermitRequest(
    [Required] Guid EmployeeUserId,
    [Required, StringLength(8, MinimumLength = 2)] string CountryCode,
    [Required, StringLength(100, MinimumLength = 2)] string PermitType,
    DateTime? IssuedOn,
    DateTime ExpiresOn,
    [StringLength(120)] string? Reference);

[ApiController]
[Authorize]
[Route("api/corporate/companies/{companyId:guid}/permits")]
[EnableRateLimiting("family-per-user")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CorporatePermitsController(ExpatOneDbContext db, IUserService users) : ControllerBase
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

    private Task<bool> IsHr(Guid companyId, Guid uid) =>
        db.CorporateMemberships.AnyAsync(m => m.CompanyId == companyId &&
            m.UserId == uid && m.Status == "Active" &&
            (m.Role == "Owner" || m.Role == "Admin" || m.Role == "HR"));

    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts(Guid companyId)
    {
        var uid = await UserId();
        if (!await IsHr(companyId, uid)) return Forbid();
        var alerts = await (
            from a in db.CorporatePermitAlerts.AsNoTracking()
            join p in db.CorporateIssuedPermits.AsNoTracking() on a.PermitId equals p.Id
            where a.CompanyId == companyId && p.CompanyId == companyId &&
                a.AcknowledgedAt == null && p.Status == "Active" &&
                a.ExpiryDate == p.ExpiresOn
            orderby a.CreatedAt descending
            select new {
                a.Id, a.PermitId, a.ThresholdDays, a.ExpiryDate, a.CreatedAt,
                p.EmployeeUserId, p.CountryCode, p.PermitType
            }).Take(200).ToListAsync(HttpContext.RequestAborted);
        return Ok(alerts);
    }

    [HttpPost("alerts/{alertId:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeAlert(Guid companyId, Guid alertId)
    {
        var uid = await UserId();
        if (!await IsHr(companyId, uid)) return Forbid();
        var changed = await db.CorporatePermitAlerts
            .Where(a => a.CompanyId == companyId && a.Id == alertId && a.AcknowledgedAt == null)
            .ExecuteUpdateAsync(x => x
                .SetProperty(a => a.AcknowledgedAt, DateTime.UtcNow)
                .SetProperty(a => a.AcknowledgedByUserId, uid)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow), HttpContext.RequestAborted);
        if (changed == 1) return NoContent();
        return NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid companyId, [FromQuery] int days = 90)
    {
        var uid = await UserId();
        if (!await IsHr(companyId, uid)) return Forbid();
        if (days is < 1 or > 365) return BadRequest(new { message = "Days must be between 1 and 365." });
        var today = DateTime.UtcNow.Date;
        var until = today.AddDays(days);
        var rows = await db.CorporateIssuedPermits.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderBy(p => p.ExpiresOn).Take(500)
            .Select(p => new { p.Id, p.EmployeeUserId, p.CountryCode,
                p.PermitType, p.Status, p.IssuedOn, p.ExpiresOn, p.Reference })
            .ToListAsync();
        return Ok(rows.Select(p => new {
            p.Id, p.EmployeeUserId, p.CountryCode, p.PermitType, p.Status,
            p.IssuedOn, p.ExpiresOn, p.Reference,
            daysRemaining = (p.ExpiresOn.Date - today).Days,
            expiryCategory = p.Status != "Active" ? "Inactive"
                : p.ExpiresOn.Date < today ? "Overdue"
                : p.ExpiresOn.Date <= until ? "DueSoon" : "Valid"
        }));
    }

    [HttpPut("{permitId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid companyId, Guid permitId, [FromBody] ChangePermitStatusRequest request)
    {
        var uid = await UserId();
        if (!await IsHr(companyId, uid)) return Forbid();
        if (request.Status is not ("Cancelled" or "Superseded"))
            return BadRequest(new { message = "Only cancellation or superseding is supported." });
        var affected = await db.CorporateIssuedPermits
            .Where(p => p.CompanyId == companyId && p.Id == permitId && p.Status == "Active")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Status, request.Status)
                .SetProperty(p => p.UpdatedAt, DateTime.UtcNow),
                HttpContext.RequestAborted);
        if (affected == 1) return Ok(new { id = permitId, status = request.Status });
        var exists = await db.CorporateIssuedPermits.AsNoTracking()
            .AnyAsync(p => p.CompanyId == companyId && p.Id == permitId);
        return exists
            ? Conflict(new { message = "Permit is already inactive." })
            : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Record(Guid companyId, [FromBody] RecordIssuedPermitRequest request)
    {
        var uid = await UserId();
        if (!await IsHr(companyId, uid)) return Forbid();
        if (request.EmployeeUserId == Guid.Empty ||
            request.ExpiresOn == default ||
            (request.IssuedOn != null && request.IssuedOn.Value.Date > request.ExpiresOn.Date))
            return BadRequest(new { message = "Valid employee and permit dates are required." });
        var active = await db.CorporateMemberships.AnyAsync(m =>
            m.CompanyId == companyId && m.UserId == request.EmployeeUserId &&
            m.Status == "Active" && (m.Role == "Employee" || m.Role == "Viewer"));
        if (!active) return BadRequest(new { message = "Employee is not active in this company." });
        var country = request.CountryCode.Trim().ToUpperInvariant();
        if (country.Length != 2 || !country.All(c => c is >= 'A' and <= 'Z'))
            return BadRequest(new { message = "Country must be a two-letter ISO code." });
        var permit = new CorporateIssuedPermit {
            Id = Guid.NewGuid(), CompanyId = companyId,
            EmployeeUserId = request.EmployeeUserId, RecordedByUserId = uid,
            CountryCode = country, PermitType = request.PermitType.Trim(),
            IssuedOn = request.IssuedOn?.Date, ExpiresOn = request.ExpiresOn.Date,
            Reference = request.Reference?.Trim(), Status = "Active"
        };
        db.CorporateIssuedPermits.Add(permit);
        await db.SaveChangesAsync(HttpContext.RequestAborted);
        return Created($"/api/corporate/companies/{companyId}/permits", new {
            permit.Id, permit.EmployeeUserId, permit.CountryCode,
            permit.PermitType, permit.ExpiresOn, permit.Status
        });
    }
}

public sealed record ChangePermitStatusRequest([Required] string Status);
