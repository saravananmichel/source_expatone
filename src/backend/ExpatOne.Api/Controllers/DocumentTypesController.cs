using ExpatOne.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Route("api/document-types")]
public class DocumentTypesController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentTypesController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var types = await _documentService.GetDocumentTypesAsync();
        return Ok(types);
    }
}
