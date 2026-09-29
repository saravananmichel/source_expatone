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
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IUserService _userService;
    private readonly IDocumentAnalysisService? _documentAnalysisService;
    private readonly IDocumentVersionService? _documentVersionService;
    private readonly IDocumentShareService? _documentShareService;
    private readonly IDocumentAuditService? _documentAuditService;

    public DocumentsController(
        IDocumentService documentService,
        IUserService userService,
        IDocumentAnalysisService? documentAnalysisService = null,
        IDocumentVersionService? documentVersionService = null,
        IDocumentShareService? documentShareService = null,
        IDocumentAuditService? documentAuditService = null)
    {
        _documentService = documentService;
        _userService = userService;
        _documentAnalysisService = documentAnalysisService;
        _documentVersionService = documentVersionService;
        _documentShareService = documentShareService;
        _documentAuditService = documentAuditService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = await GetUserIdAsync();
        var documents = await _documentService.GetUserDocumentsAsync(userId);
        return Ok(documents);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = await GetUserIdAsync();
        var document = await _documentService.GetDocumentAsync(userId, id);
        if (document is null)
            return NotFound();
        return Ok(document);
    }

    [HttpPost("upload-url")]
    public async Task<IActionResult> RequestUpload([FromBody] RequestUploadDto dto)
    {
        var userId = await GetUserIdAsync();
        var result = await _documentService.RequestUploadAsync(userId, dto);
        return Ok(result);
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteUpload(Guid id)
    {
        var userId = await GetUserIdAsync();
        var document = await _documentService.CompleteUploadAsync(userId, id);
        return Ok(document);
    }

    [HttpGet("{id:guid}/access-url")]
    public async Task<IActionResult> GetAccessUrl(Guid id)
    {
        var userId = await GetUserIdAsync();
        var result = await _documentService.GetAccessUrlAsync(userId, id);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = await GetUserIdAsync();
        await _documentService.DeleteDocumentAsync(userId, id);
        return NoContent();
    }

    [HttpPost("{id:guid}/analyze")]
    [EnableRateLimiting("ai-per-user")]
    public async Task<IActionResult> AnalyzeDocument(Guid id, [FromBody] AnalyzeRequestDto? dto = null)
    {
        if (_documentAnalysisService is null)
            return StatusCode(503, new { message = "Document analysis is not available. Gemini API is not configured." });

        var userId = await GetUserIdAsync();
        var result = await _documentAnalysisService.AnalyzeDocumentAsync(userId, id, dto?.ForceReanalyze ?? false);
        return Ok(result);
    }

    [HttpGet("{id:guid}/analysis")]
    public async Task<IActionResult> GetAnalysis(Guid id)
    {
        if (_documentAnalysisService is null)
            return StatusCode(503, new { message = "Document analysis is not available. Gemini API is not configured." });

        var userId = await GetUserIdAsync();
        var result = await _documentAnalysisService.GetDocumentAnalysisAsync(userId, id);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/ask")]
    [EnableRateLimiting("ai-per-user")]
    public async Task<IActionResult> AskDocument(Guid id, [FromBody] DocumentQuestionDto dto)
    {
        if (_documentAnalysisService is null)
            return StatusCode(503, new { message = "Document Q&A is not available. Gemini API is not configured." });

        var userId = await GetUserIdAsync();
        var result = await _documentAnalysisService.AskDocumentAsync(userId, id, dto.Question);
        return Ok(result);
    }

    // --- Version endpoints ---

    [HttpPost("{id:guid}/versions/upload-url")]
    public async Task<IActionResult> RequestVersionUpload(Guid id, [FromBody] UploadVersionDto dto)
    {
        if (_documentVersionService is null)
            return StatusCode(503, new { message = "Document versioning is not available. AWS is not configured." });

        var userId = await GetUserIdAsync();
        var result = await _documentVersionService.RequestVersionUploadAsync(userId, id, dto);
        return Ok(result);
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/complete")]
    public async Task<IActionResult> CompleteVersionUpload(Guid id, Guid versionId)
    {
        if (_documentVersionService is null)
            return StatusCode(503, new { message = "Document versioning is not available. AWS is not configured." });

        var userId = await GetUserIdAsync();
        var result = await _documentVersionService.CompleteVersionUploadAsync(userId, id, versionId);
        return Ok(result);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> GetVersions(Guid id)
    {
        if (_documentVersionService is null)
            return StatusCode(503, new { message = "Document versioning is not available. AWS is not configured." });

        var userId = await GetUserIdAsync();
        var versions = await _documentVersionService.GetVersionsAsync(userId, id);
        return Ok(versions);
    }

    [HttpGet("{id:guid}/versions/{versionId:guid}/access-url")]
    public async Task<IActionResult> GetVersionAccessUrl(Guid id, Guid versionId)
    {
        if (_documentVersionService is null)
            return StatusCode(503, new { message = "Document versioning is not available. AWS is not configured." });

        var userId = await GetUserIdAsync();
        var result = await _documentVersionService.GetVersionAccessUrlAsync(userId, id, versionId);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/versions/{versionId:guid}")]
    public async Task<IActionResult> DeleteVersion(Guid id, Guid versionId)
    {
        if (_documentVersionService is null)
            return StatusCode(503, new { message = "Document versioning is not available. AWS is not configured." });

        var userId = await GetUserIdAsync();
        await _documentVersionService.DeleteVersionAsync(userId, id, versionId);
        return NoContent();
    }

    // --- Share endpoints ---

    [HttpPost("{id:guid}/shares")]
    public async Task<IActionResult> CreateShare(Guid id, [FromBody] CreateShareDto dto)
    {
        if (_documentShareService is null)
            return StatusCode(503, new { message = "Document sharing is not available. AWS is not configured." });

        var userId = await GetUserIdAsync();
        var result = await _documentShareService.CreateShareAsync(userId, id, dto);
        return Ok(result);
    }

    [HttpGet("{id:guid}/shares")]
    public async Task<IActionResult> GetShares(Guid id)
    {
        if (_documentShareService is null)
            return StatusCode(503, new { message = "Document sharing is not available. AWS is not configured." });

        var userId = await GetUserIdAsync();
        var shares = await _documentShareService.GetSharesForDocumentAsync(userId, id);
        return Ok(shares);
    }

    [HttpDelete("{id:guid}/shares/{shareId:guid}")]
    public async Task<IActionResult> RevokeShare(Guid id, Guid shareId)
    {
        if (_documentShareService is null)
            return StatusCode(503, new { message = "Document sharing is not available. AWS is not configured." });

        var userId = await GetUserIdAsync();
        await _documentShareService.RevokeShareAsync(userId, id, shareId);
        return NoContent();
    }

    [HttpGet("shared-with-me")]
    public async Task<IActionResult> GetSharedWithMe()
    {
        if (_documentShareService is null)
            return StatusCode(503, new { message = "Document sharing is not available. AWS is not configured." });

        var userId = await GetUserIdAsync();
        var documents = await _documentShareService.GetDocumentsSharedWithMeAsync(userId);
        return Ok(documents);
    }

    [HttpGet("{id:guid}/shared-access-url")]
    public async Task<IActionResult> GetSharedDocumentAccessUrl(Guid id)
    {
        if (_documentShareService is null)
            return StatusCode(503, new { message = "Document sharing is not available. AWS is not configured." });

        var userId = await GetUserIdAsync();
        var result = await _documentShareService.GetSharedDocumentAccessUrlAsync(userId, id);
        return Ok(result);
    }

    // --- Audit endpoints ---

    [HttpGet("{id:guid}/audit-logs")]
    public async Task<IActionResult> GetAuditLogs(Guid id)
    {
        if (_documentAuditService is null)
            return StatusCode(503, new { message = "Document audit is not available. AWS is not configured." });

        var userId = await GetUserIdAsync();
        var logs = await _documentAuditService.GetAuditLogsAsync(userId, id);
        return Ok(logs);
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
