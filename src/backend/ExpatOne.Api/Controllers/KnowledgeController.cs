using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KnowledgeController : ControllerBase
{
    private readonly IKnowledgeSearchService? _searchService;
    private readonly IKnowledgeIngestionService? _ingestionService;
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;

    public KnowledgeController(
        IUserService userService,
        IConfiguration configuration,
        IKnowledgeSearchService? searchService = null,
        IKnowledgeIngestionService? ingestionService = null)
    {
        _userService = userService;
        _configuration = configuration;
        _searchService = searchService;
        _ingestionService = ingestionService;
    }

    private const int MaxSearchResults = 20;

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string countryCode = "MY", [FromQuery] int maxResults = 5)
    {
        if (_searchService is null)
            return StatusCode(503, new { message = "Knowledge search is not available. Gemini API is not configured." });

        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { message = "Search query 'q' is required." });

        if (maxResults <= 0)
            return BadRequest(new { message = "maxResults must be a positive integer." });

        var clampedMax = Math.Min(maxResults, MaxSearchResults);

        await GetUserIdAsync();

        var results = await _searchService.SearchAsync(q, countryCode, clampedMax);
        return Ok(results);
    }

    [HttpGet("sources")]
    public async Task<IActionResult> GetSources([FromQuery] string countryCode = "MY")
    {
        if (!IsAdminAuthorized())
            return StatusCode(403, new { message = "Admin access required." });

        if (_ingestionService is null)
            return StatusCode(503, new { message = "Knowledge ingestion is not available." });

        await GetUserIdAsync();

        var sources = await _ingestionService.GetSourcesAsync(countryCode);
        return Ok(sources);
    }

    [HttpPost("sources")]
    public async Task<IActionResult> RegisterSource([FromBody] RegisterSourceDto dto)
    {
        if (!IsAdminAuthorized())
            return StatusCode(403, new { message = "Admin access required." });

        if (_ingestionService is null)
            return StatusCode(503, new { message = "Knowledge ingestion is not available." });

        await GetUserIdAsync();

        var source = await _ingestionService.RegisterSourceAsync(dto);
        return Ok(source);
    }

    [HttpGet("sources/{id:guid}")]
    public async Task<IActionResult> GetSource(Guid id)
    {
        if (!IsAdminAuthorized())
            return StatusCode(403, new { message = "Admin access required." });

        if (_ingestionService is null)
            return StatusCode(503, new { message = "Knowledge ingestion is not available." });

        await GetUserIdAsync();

        var source = await _ingestionService.GetSourceAsync(id);
        if (source is null)
            return NotFound();
        return Ok(source);
    }

    [HttpDelete("sources/{id:guid}")]
    public async Task<IActionResult> DeactivateSource(Guid id)
    {
        if (!IsAdminAuthorized())
            return StatusCode(403, new { message = "Admin access required." });

        if (_ingestionService is null)
            return StatusCode(503, new { message = "Knowledge ingestion is not available." });

        await GetUserIdAsync();

        await _ingestionService.DeactivateSourceAsync(id);
        return NoContent();
    }

    [HttpPost("sources/{id:guid}/ingest")]
    public async Task<IActionResult> IngestSource(Guid id, [FromBody] IngestContentDto dto)
    {
        if (!IsAdminAuthorized())
            return StatusCode(403, new { message = "Admin access required." });

        if (_ingestionService is null)
            return StatusCode(503, new { message = "Knowledge ingestion is not available." });

        await GetUserIdAsync();

        var result = await _ingestionService.IngestSourceAsync(id, dto);
        return Ok(result);
    }

    [HttpPost("embeddings/generate")]
    public async Task<IActionResult> GenerateEmbeddings()
    {
        if (!IsAdminAuthorized())
            return StatusCode(403, new { message = "Admin access required." });

        if (_ingestionService is null)
            return StatusCode(503, new { message = "Knowledge ingestion is not available." });

        await GetUserIdAsync();

        var result = await _ingestionService.GenerateEmbeddingsAsync();
        return Ok(result);
    }

    private bool IsAdminAuthorized()
    {
        var configuredKey = _configuration["Knowledge:AdminKey"];
        if (string.IsNullOrEmpty(configuredKey))
            return false;

        var providedKey = Request.Headers["X-Admin-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(providedKey))
            return false;

        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        // Reject if lengths differ — FixedTimeEquals requires equal-length spans
        if (configuredBytes.Length != providedBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
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
