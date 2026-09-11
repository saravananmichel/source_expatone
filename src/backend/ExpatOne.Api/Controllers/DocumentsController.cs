using System.Security.Claims;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IUserService _userService;

    public DocumentsController(IDocumentService documentService, IUserService userService)
    {
        _documentService = documentService;
        _userService = userService;
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
