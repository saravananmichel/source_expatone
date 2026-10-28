using System.Security.Claims;
using ExpatOne.Application.Interfaces;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Api.Controllers;

[ApiController, Authorize, Route("api/notification-preferences")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificationPreferencesController(ExpatOneDbContext db, IUserService users) : ControllerBase
{
    public sealed record Input(bool InAppEnabled, bool PushEnabled, bool DocumentEnabled,
        bool ConnectionEnabled, bool CorporateEnabled, bool SharingEnabled,
        bool VisaEnabled, bool OccasionEnabled);

    private async Task<Guid?> CurrentUserId()
    {
        var uid = User.FindFirstValue("firebase_uid");
        if (string.IsNullOrWhiteSpace(uid)) return null;
        var user = await users.FindOrCreateByExternalIdentityAsync("firebase", uid,
            User.FindFirstValue(ClaimTypes.Email) ?? "", User.FindFirstValue(ClaimTypes.Name));
        return user.Id;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var id = await CurrentUserId();
        if (id is null) return Unauthorized();
        var preference = await db.NotificationPreferences.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == id.Value);
        return Ok(preference ?? new NotificationPreference { UserId = id.Value });
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] Input input)
    {
        var id = await CurrentUserId();
        if (id is null) return Unauthorized();
        var preference = await db.NotificationPreferences
            .SingleOrDefaultAsync(x => x.UserId == id.Value);
        if (preference is null)
        {
            preference = new NotificationPreference { UserId = id.Value };
            db.NotificationPreferences.Add(preference);
        }
        preference.InAppEnabled = input.InAppEnabled;
        preference.PushEnabled = input.PushEnabled;
        preference.DocumentEnabled = input.DocumentEnabled;
        preference.ConnectionEnabled = input.ConnectionEnabled;
        preference.CorporateEnabled = input.CorporateEnabled;
        preference.SharingEnabled = input.SharingEnabled;
        preference.VisaEnabled = input.VisaEnabled;
        preference.OccasionEnabled = input.OccasionEnabled;
        preference.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(preference);
    }
}
