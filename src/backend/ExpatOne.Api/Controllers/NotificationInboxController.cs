using System.Security.Claims;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Controllers;

[ApiController, Authorize, Route("api/notification-inbox")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificationInboxController(ExpatOneDbContext db, IUserService users) : ControllerBase
{
    private async Task<Guid?> CurrentUserId()
    {
        var uid = User.FindFirstValue("firebase_uid");
        if (string.IsNullOrWhiteSpace(uid)) return null;
        var user = await users.FindOrCreateByExternalIdentityAsync("firebase", uid,
            User.FindFirstValue(ClaimTypes.Email) ?? "", User.FindFirstValue(ClaimTypes.Name));
        return user.Id;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 50)
    {
        if (limit is < 1 or > 100) return BadRequest("limit must be between 1 and 100.");
        var id = await CurrentUserId();
        if (!id.HasValue) return Unauthorized();

        var messages = await db.NotificationDeliveries.AsNoTracking()
            .Where(x => x.UserId == id.Value && x.Channel == "InApp" && x.Status == "Delivered")
            .OrderByDescending(x => x.DeliveredAt)
            .ThenByDescending(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new
            {
                x.Id, x.ReminderKey, x.Title, x.Body, x.OccurrenceDate, x.DeliveredAt
            })
            .ToListAsync();
        return Ok(messages);
    }
}
