using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Services;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace ExpatOne.Api.Controllers;

[ApiController, Authorize, Route("api/family")]
[EnableRateLimiting("family-per-user")]
public class FamilyController(FamilyService family, IUserService users, ExpatOneDbContext db, IStorageService storage) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery, Required, EmailAddress, MaxLength(256)] string email) =>
        Ok(await family.SearchAsync(await UserId(), email));
    [HttpGet("requests")]
    public async Task<IActionResult> List() => Ok(await family.ListAsync(await UserId()));
    [HttpPost("requests")]
    public async Task<IActionResult> Send(SendFamilyRequestDto dto) => Ok(await family.SendAsync(await UserId(), dto.Email, dto.Relationship));
    [HttpPost("requests/{id:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, FamilyDecisionDto dto) => Ok(await family.RespondAsync(await UserId(), id, dto));
    [HttpPut("requests/{id:guid}/documents")]
    public async Task<IActionResult> Grants(Guid id, FamilyGrantsDto dto) => Ok(await family.UpdateGrantsAsync(await UserId(), id, dto));
    [HttpPost("requests/{id:guid}/end")]
    public async Task<IActionResult> End(Guid id, FamilyRevisionDto dto) => Ok(await family.EndAsync(await UserId(), id, dto.Revision));
    [HttpGet("members/{memberId:guid}/photo")]
    public async Task<IActionResult> MemberPhoto(Guid memberId, CancellationToken ct)
    {
        var callerId = await UserId();
        var permitted = memberId == callerId || await db.FamilyRequests.AsNoTracking().AnyAsync(
            r => r.StatusId == 2 && (r.AccessExpiresAt == null || r.AccessExpiresAt > DateTime.UtcNow) &&
                 ((r.RequesterUserId == callerId && r.RecipientUserId == memberId) ||
                  (r.RecipientUserId == callerId && r.RequesterUserId == memberId)), ct);
        if (!permitted) return NotFound();
        var key = await db.Users.AsNoTracking().Where(u => u.Id == memberId && u.IsActive)
            .Select(u => u.ProfilePhotoKey).SingleOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(key)) return NotFound();
        if (!key.StartsWith($"profile-photos/{memberId:N}/", StringComparison.Ordinal))
            return NotFound();
        return Ok(new { url = await storage.GeneratePresignedUrlAsync(key, TimeSpan.FromMinutes(5)) });
    }

    // Historical events are derived only from persisted timestamps; no inferred document-grant history.
    [HttpGet("activity")]
    public async Task<IActionResult> Activity([FromQuery] int limit = 30, CancellationToken ct = default)
    {
        var callerId = await UserId();
        limit = Math.Clamp(limit, 1, 100);
        var requests = await db.FamilyRequests.AsNoTracking()
            .Where(r => r.RequesterUserId == callerId || r.RecipientUserId == callerId)
            .Select(r => new {
                r.Id, r.RequesterUserId, r.RecipientUserId, r.Relationship,
                r.StatusId, r.CreatedAt, r.RespondedAt,
                RequesterName = r.Requester.DisplayName, RequesterEmail = r.Requester.Email,
                RecipientName = r.Recipient.DisplayName, RecipientEmail = r.Recipient.Email
            }).ToListAsync(ct);
        var occasions = await db.FamilyOccasions.AsNoTracking()
            .Where(o => o.UserId == callerId)
            .OrderByDescending(o => o.CreatedAt).Take(limit)
            .Select(o => new { o.Id, o.Title, o.CreatedAt, o.ConnectionId })
            .ToListAsync(ct);
        var events = new List<(DateTime At, object Data)>();
        foreach (var r in requests)
        {
            var incoming = r.RecipientUserId == callerId;
            var otherId = incoming ? r.RequesterUserId : r.RecipientUserId;
            var otherName = incoming ? r.RequesterName ?? r.RequesterEmail : r.RecipientName ?? r.RecipientEmail;
            events.Add((r.CreatedAt, new {
                id = $"request:{r.Id}:created", kind = "invitation", occurredAt = r.CreatedAt,
                memberId = otherId, memberName = otherName,
                title = incoming ? "Invitation received" : "Invitation sent",
                detail = r.Relationship
            }));
            if (r.RespondedAt.HasValue)
            {
                var title = r.StatusId switch {
                    2 => "Connection accepted",
                    3 => "Invitation declined",
                    4 => "Invitation cancelled",
                    5 => "Connection ended",
                    _ => "Invitation updated"
                };
                events.Add((r.RespondedAt.Value, new {
                    id = $"request:{r.Id}:response", kind = "connection", occurredAt = r.RespondedAt.Value,
                    memberId = otherId, memberName = otherName, title, detail = r.Relationship
                }));
            }
        }
        foreach (var o in occasions)
            events.Add((o.CreatedAt, new {
                id = $"occasion:{o.Id}:created", kind = "occasion", occurredAt = o.CreatedAt,
                memberId = o.ConnectionId, memberName = o.Title,
                title = "Occasion added", detail = o.Title
            }));
        return Ok(events.OrderByDescending(e => e.At).Take(limit).Select(e => e.Data));
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard() => Ok(await family.DashboardAsync(await UserId()));
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts() => Ok(await family.AlertsAsync(await UserId()));
    [HttpPost("documents/{id:guid}/confirm-expiry")]
    public async Task<IActionResult> ConfirmExpiry(Guid id, ConfirmFamilyExpiryDto dto)
    {
        await family.ConfirmExpiryAsync(await UserId(), id, dto);
        return NoContent();
    }
    [HttpPost("alerts/{id}/dismiss")]
    public async Task<IActionResult> DismissAlert(string id)
    {
        await family.DismissAlertAsync(await UserId(), id);
        return NoContent();
    }
    private async Task<Guid> UserId()
    {
        var uid = User.FindFirstValue("firebase_uid") ?? throw new UnauthorizedAccessException();
        return (await users.FindOrCreateByExternalIdentityAsync("firebase", uid,
            User.FindFirstValue(ClaimTypes.Email) ?? "", User.FindFirstValue(ClaimTypes.Name))).Id;
    }
}
