using System.Security.Claims;
using FirebaseAdmin.Auth;
using System.Net;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
    private readonly ExpatOneDbContext _db;
    private readonly IStorageService _storage;

    public UsersController(IUserService userService, ExpatOneDbContext db, IStorageService storage)
    {
        _userService = userService;
        _db = db;
        _storage = storage;
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

    [HttpPost("me/photo")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0 || file.Length > 2 * 1024 * 1024)
            return BadRequest("Photo must be between 1 byte and 2 MB.");

        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var count = await stream.ReadAsync(header.AsMemory(), ct);
        var isJpeg = count >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = count >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
        var isWebp = count >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF"
            && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP";
        if (!isJpeg && !isPng && !isWebp) return BadRequest("Only JPEG, PNG or WebP images are supported.");
        if (!stream.CanSeek) return BadRequest("Unable to read uploaded image stream.");
        stream.Position = 0;
        var current = await GetAuthenticatedUserAsync();
        var user = await _db.Users.SingleAsync(u => u.Id == current.Id, ct);
        var extension = isJpeg ? "jpg" : isPng ? "png" : "webp";
        var key = await _storage.UploadFileAsync(stream, $"{Guid.NewGuid():N}.{extension}",
            isJpeg ? "image/jpeg" : isPng ? "image/png" : "image/webp", $"profile-photos/{user.Id:N}");
        var oldKey = user.ProfilePhotoKey;
        user.ProfilePhotoKey = key;
        user.ProfilePhotoSource = "upload";
        user.ProfilePhotoUpdatedAt = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            await _storage.DeleteFileAsync(key);
            throw;
        }
        if (oldKey is not null && oldKey.StartsWith($"profile-photos/{user.Id:N}/", StringComparison.Ordinal))
        {
            try { await _storage.DeleteFileAsync(oldKey); }
            catch { /* The new photo is saved; stale object cleanup can be retried. */ }
        }
        return Ok(new { profilePhotoSource = user.ProfilePhotoSource });
    }

    [HttpGet("me/photo")]
    public async Task<IActionResult> GetPhoto(CancellationToken ct)
    {
        var current = await GetAuthenticatedUserAsync();
        var key = await _db.Users.Where(u => u.Id == current.Id).Select(u => u.ProfilePhotoKey).SingleAsync(ct);
        if (string.IsNullOrEmpty(key)) return NotFound();
        return Ok(new { url = await _storage.GeneratePresignedUrlAsync(key, TimeSpan.FromMinutes(5)) });
    }

    [HttpDelete("me/photo")]
    public async Task<IActionResult> DeletePhoto(CancellationToken ct)
    {
        var current = await GetAuthenticatedUserAsync();
        var user = await _db.Users.SingleAsync(u => u.Id == current.Id, ct);
        var oldKey = user.ProfilePhotoKey;
        user.ProfilePhotoKey = null;
        user.ProfilePhotoSource = null;
        user.ProfilePhotoUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (oldKey is not null && oldKey.StartsWith($"profile-photos/{user.Id:N}/", StringComparison.Ordinal))
        {
            try { await _storage.DeleteFileAsync(oldKey); }
            catch { /* The profile no longer references this object. */ }
        }
        return NoContent();
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
