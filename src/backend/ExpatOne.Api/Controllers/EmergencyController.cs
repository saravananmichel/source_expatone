using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmergencyController : ControllerBase
{
    private readonly IEmergencyAssistService? _emergencyAssistService;

    public EmergencyController(IEmergencyAssistService? emergencyAssistService = null)
    {
        _emergencyAssistService = emergencyAssistService;
    }

    // Only the AI assistance endpoint is rate-limited.
    // The deterministic CALL 999 flow is Flutter-native and has no backend dependency.
    [HttpPost("assist")]
    [EnableRateLimiting("emergency-ai-per-user")]
    public async Task<IActionResult> Assist([FromBody] EmergencyAssistRequestDto request)
    {
        if (_emergencyAssistService is null)
            return StatusCode(503, new { message = "Emergency AI assistance is not available. Gemini API is not configured." });

        var result = await _emergencyAssistService.AssistAsync(request);
        return Ok(result);
    }
}
