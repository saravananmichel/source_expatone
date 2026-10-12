using System.Security.Claims;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace ExpatOne.Api.Controllers;

[ApiController, Authorize, Route("api/visa-applications")]
[EnableRateLimiting("family-per-user")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[RequestSizeLimit(131072)]
public class VisaApplicationsController(VisaApplicationService service,IUserService users):ControllerBase
{
    [HttpGet("catalog")] public async Task<IActionResult> Catalog()=>Ok(await service.CatalogAsync());
    [HttpGet] public async Task<IActionResult> List()=>Ok(await service.ListAsync(await UserId()));
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id)=>Ok(await service.GetAsync(await UserId(),id));
    [HttpPost] public async Task<IActionResult> Create(VisaCreateDto dto)=>Ok(await service.CreateAsync(await UserId(),dto));
    [HttpPut("{id:guid}")] public async Task<IActionResult> Save(Guid id,VisaUpdateDto dto)=>Ok(await service.UpdateAsync(await UserId(),id,dto));
    [HttpPut("{id:guid}/documents")] public async Task<IActionResult> Documents(Guid id,VisaDocumentsDto dto)=>Ok(await service.DocumentsAsync(await UserId(),id,dto));
    [HttpPost("{id:guid}/prefill")] public async Task<IActionResult> Prefill(Guid id,VisaRevisionDto dto)=>Ok(await service.PrefillAsync(await UserId(),id,dto.Revision));
    [HttpPost("{id:guid}/export/{form}")] public async Task<IActionResult> Export(Guid id,string form,VisaRevisionDto dto,CancellationToken ct)=>Ok(await service.ExportAsync(await UserId(),id,dto.Revision,form,ct));
    // This records the applicant's own submission confirmation; it does not submit to ICA.
    [HttpPost("{id:guid}/mark-submitted")] public async Task<IActionResult> Submitted(Guid id,VisaRevisionDto dto)=>Ok(await service.SubmitAsync(await UserId(),id,dto.Revision));
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id,[FromQuery] Guid revision) { await service.DeleteAsync(await UserId(),id,revision);return Ok(new{deleted=true}); }
    private async Task<Guid> UserId() {
        var uid=User.FindFirstValue("firebase_uid")??throw new UnauthorizedAccessException();
        return (await users.FindOrCreateByExternalIdentityAsync("firebase",uid,User.FindFirstValue(ClaimTypes.Email)??"",User.FindFirstValue(ClaimTypes.Name))).Id;
    }
}
