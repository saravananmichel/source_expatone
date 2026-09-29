using System.Security.Claims;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RemindersController : ControllerBase
{
    private readonly IReminderService _reminderService;
    private readonly IUserService _userService;

    public RemindersController(IReminderService reminderService, IUserService userService)
    {
        _reminderService = reminderService;
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = await GetUserIdAsync();
        var reminders = await _reminderService.GetUserRemindersAsync(userId);
        return Ok(reminders);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = await GetUserIdAsync();
        var reminder = await _reminderService.GetReminderAsync(userId, id);
        if (reminder is null) return NotFound();
        return Ok(reminder);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReminderDto dto)
    {
        var userId = await GetUserIdAsync();
        var reminder = await _reminderService.CreateReminderAsync(userId, dto);
        return CreatedAtAction(nameof(GetById), new { id = reminder.Id }, reminder);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateRemindersDto dto)
    {
        var userId = await GetUserIdAsync();
        var reminders = await _reminderService.GenerateRemindersAsync(userId, dto);
        return Ok(reminders);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReminderDto dto)
    {
        var userId = await GetUserIdAsync();
        var reminder = await _reminderService.UpdateReminderAsync(userId, id, dto);
        return Ok(reminder);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = await GetUserIdAsync();
        await _reminderService.DeleteReminderAsync(userId, id);
        return NoContent();
    }

    [HttpGet("document/{documentId:guid}")]
    public async Task<IActionResult> GetByDocument(Guid documentId)
    {
        var userId = await GetUserIdAsync();
        var reminders = await _reminderService.GetDocumentRemindersAsync(userId, documentId);
        return Ok(reminders);
    }

    private async Task<Guid> GetUserIdAsync()
    {
        var firebaseUid = User.FindFirstValue("firebase_uid")
            ?? throw new UnauthorizedAccessException();
        var email = User.FindFirstValue(ClaimTypes.Email) ?? "";
        var displayName = User.FindFirstValue(ClaimTypes.Name);

        var user = await _userService.FindOrCreateByExternalIdentityAsync(
            "firebase", firebaseUid, email, displayName);
        return user.Id;
    }
}
