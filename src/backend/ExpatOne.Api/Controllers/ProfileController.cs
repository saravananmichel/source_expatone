using System.Security.Claims;
using ExpatOne.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly IUserService _userService;

    public ProfileController(IProfileService profileService, IUserService userService)
    {
        _profileService = profileService;
        _userService = userService;
    }

    [HttpGet("options")]
    public IActionResult GetOptions()
    {
        return Ok(_profileService.GetProfileOptions());
    }

    [HttpGet("checklist")]
    [Authorize]
    public async Task<IActionResult> GetChecklist()
    {
        var user = await GetAuthenticatedUserAsync();
        var checklist = await _profileService.GetOnboardingChecklistAsync(user.Id);
        return Ok(checklist);
    }

    private async Task<Application.DTOs.UserDto> GetAuthenticatedUserAsync()
    {
        var firebaseUid = User.FindFirstValue("firebase_uid")
            ?? throw new UnauthorizedAccessException();
        var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
        var displayName = User.FindFirstValue(ClaimTypes.Name);

        return await _userService.FindOrCreateByExternalIdentityAsync(
            "firebase", firebaseUid, email, displayName);
    }
}
