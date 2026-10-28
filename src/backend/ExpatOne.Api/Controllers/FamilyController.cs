using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace ExpatOne.Api.Controllers;

[ApiController, Authorize, Route("api/family")]
[EnableRateLimiting("family-per-user")]
public class FamilyController(FamilyService family, IUserService users) : ControllerBase
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
