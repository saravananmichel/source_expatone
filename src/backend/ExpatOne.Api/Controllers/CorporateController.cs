using Microsoft.EntityFrameworkCore;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Domain.Entities;
using ExpatOne.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpatOne.Api.Controllers;

public record InviteEmployeeRequest([Required, EmailAddress] string Email, string Role = "Employee");
public record CreateCorporateCaseRequest(Guid EmployeeUserId, [Required, MaxLength(180)] string Title, DateTime? DueAt, [MaxLength(2000)] string? Notes, string? CountryCode = null, string? Purpose = null, string Priority = "Normal", Guid? AssignedHrUserId = null);
public record UpdateCorporateCaseRequest([Required] string Status, DateTime? DueAt, [MaxLength(2000)] string? Notes, string? Title = null, string? CountryCode = null, string? Purpose = null, string? Priority = null, Guid? AssignedHrUserId = null);
public record LinkVisaRequest(Guid VisaApplicationId);

[ApiController]
[Authorize]
[Route("api/corporate")]
[EnableRateLimiting("family-per-user")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CorporateController(
    CorporateMobilityService corporate,
    IUserService users,
    ExpatOneDbContext db, IStorageService storage) : ControllerBase
{
    [HttpPost("companies")]
    public async Task<IActionResult> CreateCompany(CreateCorporateCompanyDto dto)
    {
        var userId = await UserId();
        var company = await corporate.CreateCompanyAsync(userId, dto);
        return Created($"/api/corporate/companies/{company.Id}", company);
    }

    [HttpGet("companies")]
    public async Task<IActionResult> ListCompanies()
        => Ok(await corporate.ListCompaniesAsync(await UserId()));

    [HttpGet("companies/{companyId:guid}/members")]
    public async Task<IActionResult> ListMembers(Guid companyId)
        => Ok(await corporate.ListMembersAsync(await UserId(), companyId));


    [HttpPost("companies/{companyId:guid}/members")]
    public async Task<IActionResult> AddMember(
        Guid companyId, AddCorporateMemberDto dto)
    {
        var member = await corporate.AddMemberAsync(
            await UserId(), companyId, dto);

        return Created(
            $"/api/corporate/companies/{companyId}/members/{member.MembershipId}",
            member);
    }

    [HttpPut("companies/{companyId:guid}/members/{membershipId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRole(
        Guid companyId, Guid membershipId, UpdateCorporateMemberRoleDto dto)
    {
        var member = await corporate.UpdateMemberRoleAsync(
            await UserId(), companyId, membershipId, dto);

        return Ok(member);
    }

    [HttpPut("companies/{companyId:guid}/members/{membershipId:guid}/status")]
    public async Task<IActionResult> UpdateMemberStatus(
        Guid companyId, Guid membershipId, UpdateCorporateMemberStatusDto dto)
    {
        var member = await corporate.UpdateMemberStatusAsync(
            await UserId(), companyId, membershipId, dto);

        return Ok(member);
    }


    private async Task<bool> Hr(Guid companyId, Guid userId) =>
        await db.CorporateMemberships.AnyAsync(x =>
            x.CompanyId == companyId && x.UserId == userId &&
            x.Status == "Active" && (x.Role == "Admin" || x.Role == "Owner" || x.Role == "HR"));

    private async Task<bool> Member(Guid companyId, Guid userId) =>
        await db.CorporateMemberships.AnyAsync(x =>
            x.CompanyId == companyId && x.UserId == userId &&
            x.Status == "Active");

    [HttpGet("invitations")]
 public async Task<IActionResult> Invitations() {
  var uid=await UserId();
  return Ok(await db.CorporateMemberships.AsNoTracking().Where(x=>x.UserId==uid && x.Status=="Pending")
   .Join(db.CorporateCompanies,m=>m.CompanyId,c=>c.Id,(m,c)=>new{m.Id,m.CompanyId,c.Name,m.Role}).ToListAsync());
 }
 [HttpPost("companies/{companyId:guid}/invite")]
 public async Task<IActionResult> Invite(Guid companyId, InviteEmployeeRequest request) {
  var uid=await UserId();
  if(!await Hr(companyId,uid)) return Forbid();
  var role=request.Role?.Trim() ?? "Viewer";
  if(role is not ("Employee" or "Viewer" or "HR")) return BadRequest(new{message="Choose Employee, Viewer or HR."});
  if(role=="HR" && !await db.CorporateMemberships.AnyAsync(x=>x.CompanyId==companyId && x.UserId==uid && x.Status=="Active" && (x.Role=="Admin" || x.Role=="Owner"))) return Forbid();
  var email=request.Email.Trim().ToLowerInvariant();
  var target=await db.Users.FirstOrDefaultAsync(x=>x.Email.ToLower()==email && x.IsActive);
  if(target==null) return NotFound(new{message="Employee must register with ExpatOne first."});
  if(target.Id==uid) return BadRequest(new{message="Cannot invite yourself."});
  var existing=await db.CorporateMemberships.FirstOrDefaultAsync(x=>x.CompanyId==companyId && x.UserId==target.Id);
  if(existing is not null) {
   if(existing.Status!="Declined") return Conflict(new{message="Invitation or membership already exists. Edit status to reactivate inactive users."});
   existing.Role=role; existing.Status="Pending";
   await db.SaveChangesAsync(); return Ok(new{existing.Id,existing.Status});
  }
  var invite=new CorporateMembership{Id=Guid.NewGuid(),CompanyId=companyId,UserId=target.Id,Role=role,Status="Pending"};
  db.CorporateMemberships.Add(invite); await db.SaveChangesAsync();
  return Ok(new{invite.Id,invite.Status});
 }
 [HttpPost("invitations/{id:guid}/accept")]
 public async Task<IActionResult> Accept(Guid id) {
  var uid=await UserId();
  var invite=await db.CorporateMemberships.FirstOrDefaultAsync(x=>x.Id==id && x.UserId==uid && x.Status=="Pending");
  if(invite==null) return NotFound();
  invite.Status="Active"; await db.SaveChangesAsync(); return Ok(new{invite.Id,invite.Status});
 }
 [HttpPost("invitations/{id:guid}/decline")]
 public async Task<IActionResult> Decline(Guid id) {
  var uid=await UserId();
  var invite=await db.CorporateMemberships.FirstOrDefaultAsync(x=>x.Id==id && x.UserId==uid && x.Status=="Pending");
  if(invite==null) return NotFound();
  invite.Status="Declined"; await db.SaveChangesAsync(); return Ok(new{invite.Id,invite.Status});
 }
 [HttpGet("companies/{companyId:guid}/employees")]
 public async Task<IActionResult> Employees(Guid companyId) {
  var uid=await UserId(); if(!await Hr(companyId,uid)) return Forbid();
  return Ok(await db.CorporateMemberships.AsNoTracking().Where(x=>x.CompanyId==companyId)
   .Join(db.Users,m=>m.UserId,u=>u.Id,(m,u)=>new{u.Id,u.DisplayName,u.Email,membershipId=m.Id,m.Role,m.Status}).ToListAsync());
 }
 [HttpGet("companies/{companyId:guid}/dashboard")]
 public async Task<IActionResult> Dashboard(Guid companyId) {
  var uid=await UserId(); if(!await Hr(companyId,uid)) return Forbid();
  var active=await db.CorporateMemberships.CountAsync(x=>x.CompanyId==companyId && x.Status=="Active" && (x.Role=="Employee" || x.Role=="Viewer"));
  var open=await db.CorporateCases.CountAsync(x=>x.CompanyId==companyId && x.Status!="Closed");
  var due=await db.CorporateCases.CountAsync(x=>x.CompanyId==companyId && x.Status!="Closed" && x.DueAt!=null && x.DueAt<=DateTime.UtcNow.AddDays(30));
  return Ok(new{activeEmployees=active,openCases=open,dueWithin30Days=due});
 }
 [HttpGet("companies/{companyId:guid}/cases")]
 public async Task<IActionResult> Cases(Guid companyId) {
  var uid=await UserId(); if(!await Member(companyId,uid)) return Forbid();
  var query=db.CorporateCases.AsNoTracking().Where(x=>x.CompanyId==companyId);
  if(!await Hr(companyId,uid)) query=query.Where(x=>x.EmployeeUserId==uid);
  return Ok(await query.OrderBy(x=>x.DueAt).Take(250).Select(x=>new{x.Id,x.EmployeeUserId,x.VisaApplicationId,x.Title,x.Status,x.DueAt,x.Notes,x.CountryCode,x.Purpose,x.Priority,x.AssignedHrUserId}).ToListAsync());
 }
 [HttpPost("companies/{companyId:guid}/cases")]
 public async Task<IActionResult> CreateCase(Guid companyId,CreateCorporateCaseRequest request) {
  var uid=await UserId(); if(!await Hr(companyId,uid)) return Forbid();
  if(!await db.CorporateMemberships.AnyAsync(x=>x.CompanyId==companyId && x.UserId==request.EmployeeUserId && (x.Role=="Employee" || x.Role=="Viewer") && x.Status=="Active"))
   return BadRequest(new{message="Employee has not accepted invitation."});
  if(string.IsNullOrWhiteSpace(request.Title)) return BadRequest();
  if(!await ValidCatalog(request.CountryCode,request.Purpose) || !ValidPriority(request.Priority) || !await ValidHr(companyId,request.AssignedHrUserId)) return BadRequest(new{message="Invalid catalog, priority or HR assignment."});
  var item=new CorporateCase{Id=Guid.NewGuid(),CompanyId=companyId,EmployeeUserId=request.EmployeeUserId,Title=request.Title.Trim(),DueAt=request.DueAt,Notes=request.Notes,CountryCode=request.CountryCode,Purpose=request.Purpose,Priority=request.Priority,AssignedHrUserId=request.AssignedHrUserId};
  db.CorporateCases.Add(item); await db.SaveChangesAsync(); return Ok(new{item.Id,item.Status});
 }
 [HttpPut("companies/{companyId:guid}/cases/{caseId:guid}")]
 public async Task<IActionResult> UpdateCase(Guid companyId,Guid caseId,UpdateCorporateCaseRequest request) {
  var uid=await UserId(); if(!await Hr(companyId,uid)) return Forbid();
  if(!new[]{"Open","InProgress","Blocked","Closed"}.Contains(request.Status)) return BadRequest(new{message="Invalid status"});
  var item=await db.CorporateCases.FirstOrDefaultAsync(x=>x.Id==caseId && x.CompanyId==companyId);
  if(item==null) return NotFound();
  if(!await ValidCatalog(request.CountryCode,request.Purpose) || (request.Priority!=null && !ValidPriority(request.Priority)) || !await ValidHr(companyId,request.AssignedHrUserId)) return BadRequest(new{message="Invalid catalog, priority or HR assignment."});
  item.Status=request.Status;
  item.DueAt=request.DueAt;
  if(request.Notes is not null) item.Notes=request.Notes;
  if(request.Title is not null) { if(string.IsNullOrWhiteSpace(request.Title) || request.Title.Length>180) return BadRequest(); item.Title=request.Title.Trim(); }
  if(request.CountryCode is not null) item.CountryCode=request.CountryCode;
  if(request.Purpose is not null) item.Purpose=request.Purpose;
  if(request.Priority is not null) item.Priority=request.Priority;
  if(request.AssignedHrUserId is not null) item.AssignedHrUserId=request.AssignedHrUserId;
  await db.SaveChangesAsync(); return Ok(new{item.Id,item.Status});
 }
 // Only the visa application's owner can opt into sharing its reference with their employer.
 // The existing visa prefill/export endpoints remain owner-only.
 [HttpPost("companies/{companyId:guid}/cases/{caseId:guid}/link-my-visa")]
 public async Task<IActionResult> LinkMyVisa(Guid companyId,Guid caseId,LinkVisaRequest request) {
  var uid=await UserId(); if(!await Member(companyId,uid)) return Forbid();
  var item=await db.CorporateCases.FirstOrDefaultAsync(x=>x.Id==caseId && x.CompanyId==companyId && x.EmployeeUserId==uid);
  if(item==null) return NotFound();
  if(!await db.VisaApplications.AnyAsync(x=>x.Id==request.VisaApplicationId && x.UserId==uid)) return BadRequest(new{message="Visa application is not owned by current user."});
  item.VisaApplicationId=request.VisaApplicationId; await db.SaveChangesAsync(); return Ok(new{item.Id,item.VisaApplicationId});
 }
 [HttpPost("companies/{companyId:guid}/cases/{caseId:guid}/unlink-my-visa")]
 public async Task<IActionResult> UnlinkMyVisa(Guid companyId,Guid caseId) {
  var uid=await UserId();
  if(!await Member(companyId,uid)) return Forbid();
  var item=await db.CorporateCases.FirstOrDefaultAsync(x=>x.Id==caseId && x.CompanyId==companyId && x.EmployeeUserId==uid);
  if(item==null) return NotFound();
  item.VisaApplicationId=null; await db.SaveChangesAsync(); return NoContent();
 }

    [HttpGet("companies/{companyId:guid}/cases/{caseId:guid}/visa/documents/{documentId:guid}/access-url")]
    public async Task<IActionResult> CorporateDocumentUrl(Guid companyId,Guid caseId,Guid documentId) {
        var uid=await UserId(); if(!await Hr(companyId,uid)) return Forbid();
        var item=await db.CorporateCases.AsNoTracking().FirstOrDefaultAsync(x=>x.CompanyId==companyId && x.Id==caseId && x.VisaApplicationId!=null);
        if(item==null) return NotFound();
        var visa=await db.VisaApplications.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==item.VisaApplicationId && x.UserId==item.EmployeeUserId);
        if(visa==null) return NotFound();
        using var state=System.Text.Json.JsonDocument.Parse(visa.StateJson);
        if(!state.RootElement.TryGetProperty("documents",out var arr) || arr.ValueKind!=System.Text.Json.JsonValueKind.Array || !arr.EnumerateArray().Any(d=>d.TryGetProperty("id",out var v) && Guid.TryParse(v.ToString(),out var id) && id==documentId)) return NotFound();
        var doc=await db.Documents.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==documentId && x.UserId==item.EmployeeUserId && x.Status!=DocumentStatus.PendingUpload);
        if(doc==null) return NotFound();
        var url=await storage.GeneratePresignedUrlAsync(doc.S3ObjectKey,TimeSpan.FromMinutes(5));
        return Ok(new{url,expiresInSeconds=300});
    }

    private static bool ValidPriority(string priority) => priority is "Normal" or "High" or "Urgent";
    private async Task<bool> ValidHr(Guid companyId, Guid? userId) => userId == null || await db.CorporateMemberships.AnyAsync(x => x.CompanyId == companyId && x.UserId == userId && x.Status == "Active" && (x.Role == "Admin" || x.Role == "Owner" || x.Role == "HR"));
    private async Task<bool> ValidCatalog(string? country, string? purpose) {
        if (string.IsNullOrEmpty(country) && string.IsNullOrEmpty(purpose)) return true;
        if (string.IsNullOrEmpty(country) || string.IsNullOrEmpty(purpose)) return false;
        return await db.VisaTemplates.AnyAsync(x=>x.CountryCode==country && x.Purpose==purpose);
    }

    // HR receives the current saved application, never write access or a frozen snapshot.
    [HttpGet("companies/{companyId:guid}/cases/{caseId:guid}/visa")]
    public async Task<IActionResult> CorporateVisa(Guid companyId, Guid caseId) {
        var uid=await UserId(); if(!await Hr(companyId,uid)) return Forbid();
        var item=await db.CorporateCases.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==caseId && x.CompanyId==companyId && x.VisaApplicationId!=null);
        if(item==null) return NotFound();
        var visa=await db.VisaApplications.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==item.VisaApplicationId && x.UserId==item.EmployeeUserId);
        if(visa==null) return NotFound();
        var state=System.Text.Json.JsonDocument.Parse(visa.StateJson);
        using(state) {
            var docs=new List<object>();
            if(state.RootElement.TryGetProperty("documents",out var arr) && arr.ValueKind==System.Text.Json.JsonValueKind.Array) {
                foreach(var doc in arr.EnumerateArray()) {
                    if(!doc.TryGetProperty("id",out var idValue) || !Guid.TryParse(idValue.ToString(),out var docId)) continue;
                    var record=await db.Documents.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==docId && x.UserId==item.EmployeeUserId);
                    if(record!=null) docs.Add(new {id=record.Id,name=record.DocumentName,role=doc.TryGetProperty("role",out var r)?r.ToString():""});
                }
            }
            return Ok(new {visa.Id,visa.TemplateId,visa.Submitted,visa.UpdatedAt,visa.Revision,values=state.RootElement.TryGetProperty("values",out var v)?v.Clone():default(System.Text.Json.JsonElement),documents=docs,readOnly=true});
        }
    }
    private async Task<Guid> UserId()
    {
        var uid = User.FindFirstValue("firebase_uid")
            ?? throw new UnauthorizedAccessException();

        return (await users.FindOrCreateByExternalIdentityAsync(
            "firebase",
            uid,
            User.FindFirstValue(ClaimTypes.Email) ?? "",
            User.FindFirstValue(ClaimTypes.Name))).Id;
    }
}
