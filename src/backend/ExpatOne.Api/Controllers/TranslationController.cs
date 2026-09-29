using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TranslationController : ControllerBase
{
    private readonly ITranslationService? _translationService;

    public TranslationController(ITranslationService? translationService = null)
    {
        _translationService = translationService;
    }

    [HttpPost("translate")]
    [EnableRateLimiting("ai-per-user")]
    public async Task<IActionResult> Translate([FromBody] TranslateRequestDto request)
    {
        if (_translationService is null)
            return StatusCode(503, new { message = "Translation is not available. Gemini API is not configured." });

        var result = await _translationService.TranslateAsync(request);
        return Ok(result);
    }

    [HttpGet("languages")]
    public IActionResult GetSupportedLanguages()
    {
        if (_translationService is null)
            return StatusCode(503, new { message = "Translation is not available. Gemini API is not configured." });

        var languages = _translationService.GetSupportedLanguages();
        return Ok(languages);
    }
}
