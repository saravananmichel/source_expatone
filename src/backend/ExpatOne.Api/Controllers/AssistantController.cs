using System.Security.Claims;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssistantController : ControllerBase
{
    private readonly IAssistantService? _assistantService;
    private readonly IUserService _userService;

    public AssistantController(
        IUserService userService,
        IAssistantService? assistantService = null)
    {
        _userService = userService;
        _assistantService = assistantService;
    }

    [HttpPost("conversations")]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationDto? dto)
    {
        if (_assistantService is null)
            return StatusCode(503, new { message = "AI assistant is not available. Gemini API is not configured." });

        var userId = await GetUserIdAsync();
        var conversation = await _assistantService.CreateConversationAsync(userId, dto?.CountryCode ?? "MY");
        return Ok(conversation);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        if (_assistantService is null)
            return StatusCode(503, new { message = "AI assistant is not available. Gemini API is not configured." });

        var userId = await GetUserIdAsync();
        var conversations = await _assistantService.GetConversationsAsync(userId);
        return Ok(conversations);
    }

    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> GetConversation(Guid id)
    {
        if (_assistantService is null)
            return StatusCode(503, new { message = "AI assistant is not available. Gemini API is not configured." });

        var userId = await GetUserIdAsync();
        var conversation = await _assistantService.GetConversationAsync(id, userId);
        return Ok(conversation);
    }

    [HttpPost("conversations/{id:guid}/messages")]
    [EnableRateLimiting("ai-per-user")]
    public async Task<IActionResult> SendMessage(Guid id, [FromBody] SendMessageDto dto)
    {
        if (_assistantService is null)
            return StatusCode(503, new { message = "AI assistant is not available. Gemini API is not configured." });

        var userId = await GetUserIdAsync();
        var response = await _assistantService.SendMessageAsync(id, userId, dto.Message);
        return Ok(response);
    }

    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> DeleteConversation(Guid id)
    {
        if (_assistantService is null)
            return StatusCode(503, new { message = "AI assistant is not available. Gemini API is not configured." });

        var userId = await GetUserIdAsync();
        await _assistantService.DeleteConversationAsync(id, userId);
        return NoContent();
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
