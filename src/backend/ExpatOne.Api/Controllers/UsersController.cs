using System.Security.Claims;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var user = await GetAuthenticatedUserAsync();
        return Ok(user);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateUserDto dto)
    {
        var currentUser = await GetAuthenticatedUserAsync();
        var updated = await _userService.UpdateProfileAsync(currentUser.Id, dto);
        return Ok(updated);
    }

    private async Task<UserDto> GetAuthenticatedUserAsync()
    {
        var firebaseUid = User.FindFirstValue("firebase_uid")
            ?? throw new UnauthorizedAccessException();
        var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
        var displayName = User.FindFirstValue(ClaimTypes.Name);

        return await _userService.FindOrCreateByExternalIdentityAsync(
            "firebase", firebaseUid, email, displayName);
    }
}
