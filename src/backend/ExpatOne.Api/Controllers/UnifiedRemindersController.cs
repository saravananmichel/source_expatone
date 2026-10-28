using System.Security.Claims;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Controllers;

[ApiController, Authorize, Route("api/unified-reminders")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class UnifiedRemindersController(ExpatOneDbContext db, IUserService users) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? category = null, [FromQuery] int days = 90)
    {
        category = category?.Trim().ToLowerInvariant();
        if (days < 1 || days > 365) return BadRequest("days must be between 1 and 365.");
        var uid = User.FindFirstValue("firebase_uid") ?? throw new UnauthorizedAccessException();
        var user = await users.FindOrCreateByExternalIdentityAsync("firebase", uid,
            User.FindFirstValue(ClaimTypes.Email) ?? "", User.FindFirstValue(ClaimTypes.Name));
        var now = DateTime.UtcNow;
        var until = now.AddDays(days);
        var items = new List<UnifiedReminderItem>();

        if (category is null or "document")
        {
            var reminders = await db.Reminders.AsNoTracking()
                .Where(r => r.UserId == user.Id && r.Status == ReminderStatus.Active
                    && r.ReminderDate >= now && r.ReminderDate <= until)
                .Select(r => new { r.Id, r.DocumentId, r.Title, r.Description, r.ReminderDate })
                .ToListAsync();
            items.AddRange(reminders.Select(r => new UnifiedReminderItem(
                "document:" + r.Id, "document", r.Title, r.Description, r.ReminderDate, r.DocumentId)));
        }

        if (category is null or "connection")
        {
            var pending = await db.FamilyRequests.AsNoTracking()
                .Where(r => r.RecipientUserId == user.Id && r.StatusId == FamilyRequestStates.Pending)
                .Select(r => new { r.Id, r.CreatedAt, Sender = r.Requester.DisplayName ?? r.Requester.Email })
                .ToListAsync();
            items.AddRange(pending.Select(r => new UnifiedReminderItem(
                "connection:" + r.Id, "connection", "Connection invitation awaiting response",
                "Invitation from " + r.Sender, r.CreatedAt, r.Id)));
        }

        if (category is null or "corporate")
        {
            var tasks = await db.CorporateCases.AsNoTracking()
                .Where(c => (c.EmployeeUserId == user.Id || c.AssignedHrUserId == user.Id)
                    && db.CorporateMemberships.Any(m => m.CompanyId == c.CompanyId && m.UserId == user.Id && m.Status == "Active")
                    && c.DueAt.HasValue && c.DueAt.Value >= now && c.DueAt.Value <= until
                    && c.Status != "Completed" && c.Status != "Cancelled")
                .Select(c => new { c.Id, c.Title, c.Notes, c.DueAt })
                .ToListAsync();
            items.AddRange(tasks.Select(c => new UnifiedReminderItem(
                "corporate:" + c.Id, "corporate", c.Title, c.Notes, c.DueAt!.Value, c.Id)));
        }

        if (category is null or "sharing")
        {
            var shares = await db.FamilyRequests.AsNoTracking()
                .Where(r => r.RecipientUserId == user.Id
                    && r.StatusId == FamilyRequestStates.Accepted
                    && r.AccessExpiresAt.HasValue
                    && r.AccessExpiresAt.Value > now && r.AccessExpiresAt.Value <= until)
                .Select(r => new { r.Id, r.AccessExpiresAt, Contact = r.Requester.DisplayName ?? r.Requester.Email })
                .ToListAsync();
            items.AddRange(shares.Select(r => new UnifiedReminderItem(
                "sharing:" + r.Id, "sharing", "Document sharing expires soon",
                "Shared access for " + r.Contact, r.AccessExpiresAt!.Value, r.Id)));
        }

        if (category is not null && category is not ("document" or "connection" or "corporate" or "sharing"))
            return BadRequest("Unsupported reminder category.");

        return Ok(items.OrderBy(i => i.DueAtUtc).ThenBy(i => i.Id).ToList());
    }

    private sealed record UnifiedReminderItem(
        string Id, string Category, string Title, string? Description, DateTime DueAtUtc, Guid SourceId);
}
