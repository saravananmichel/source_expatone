using Microsoft.EntityFrameworkCore;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpatOne.Api.Controllers;

public record InviteEmployeeRequest([Required, EmailAddress] string Email);
public record CreateCorporateCaseRequest(Guid EmployeeUserId, [Required, MaxLength(180)] string Title, DateTime? DueAt, [MaxLength(2000)] string? Notes);
public record UpdateCorporateCaseRequest([Required] string Status, DateTime? DueAt, [MaxLength(2000)] string? Notes);
public record LinkVisaRequest(Guid VisaApplicationId);

[ApiController]
[Authorize]
[Route("api/corporate")]
[EnableRateLimiting("family-per-user")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CorporateController(
    CorporateMobilityService corporate,
    IUserService users,
    ExpatOneDbContext db) : ControllerBase
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
            x.Status == "Active" && (x.Role == "Admin" || x.Role == "Owner"));

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
  var email=request.Email.Trim().ToLowerInvariant();
  var target=await db.Users.FirstOrDefaultAsync(x=>x.Email.ToLower()==email && x.IsActive);
  if(target==null) return NotFound(new{message="Employee must register with ExpatOne first."});
  if(target.Id==uid) return BadRequest(new{message="Cannot invite yourself."});
  var existing=await db.CorporateMemberships.FirstOrDefaultAsync(x=>x.CompanyId==companyId && x.UserId==target.Id);
  if(existing is not null) {
   if(existing.Status!="Declined") return Conflict(new{message="Invitation or membership already exists."});
   existing.Role="Employee"; existing.Status="Pending";
   await db.SaveChangesAsync(); return Ok(new{existing.Id,existing.Status});
  }
  var invite=new CorporateMembership{Id=Guid.NewGuid(),CompanyId=companyId,UserId=target.Id,Role="Employee",Status="Pending"};
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
   .Join(db.Users,m=>m.UserId,u=>u.Id,(m,u)=>new{u.Id,u.DisplayName,u.Email,m.Role,m.Status}).ToListAsync());
 }
 [HttpGet("companies/{companyId:guid}/dashboard")]
 public async Task<IActionResult> Dashboard(Guid companyId) {
  var uid=await UserId(); if(!await Hr(companyId,uid)) return Forbid();
  var active=await db.CorporateMemberships.CountAsync(x=>x.CompanyId==companyId && x.Status=="Active" && x.Role=="Employee");
  var open=await db.CorporateCases.CountAsync(x=>x.CompanyId==companyId && x.Status!="Closed");
  var due=await db.CorporateCases.CountAsync(x=>x.CompanyId==companyId && x.Status!="Closed" && x.DueAt!=null && x.DueAt<=DateTime.UtcNow.AddDays(30));
  return Ok(new{activeEmployees=active,openCases=open,dueWithin30Days=due});
 }
 [HttpGet("companies/{companyId:guid}/cases")]
 public async Task<IActionResult> Cases(Guid companyId) {
  var uid=await UserId(); if(!await Member(companyId,uid)) return Forbid();
  var query=db.CorporateCases.AsNoTracking().Where(x=>x.CompanyId==companyId);
  if(!await Hr(companyId,uid)) query=query.Where(x=>x.EmployeeUserId==uid);
  return Ok(await query.OrderBy(x=>x.DueAt).Take(250).Select(x=>new{x.Id,x.EmployeeUserId,x.VisaApplicationId,x.Title,x.Status,x.DueAt,x.Notes}).ToListAsync());
 }
 [HttpPost("companies/{companyId:guid}/cases")]
 public async Task<IActionResult> CreateCase(Guid companyId,CreateCorporateCaseRequest request) {
  var uid=await UserId(); if(!await Hr(companyId,uid)) return Forbid();
  if(!await db.CorporateMemberships.AnyAsync(x=>x.CompanyId==companyId && x.UserId==request.EmployeeUserId && x.Role=="Employee" && x.Status=="Active"))
   return BadRequest(new{message="Employee has not accepted invitation."});
  if(string.IsNullOrWhiteSpace(request.Title)) return BadRequest();
  var item=new CorporateCase{Id=Guid.NewGuid(),CompanyId=companyId,EmployeeUserId=request.EmployeeUserId,Title=request.Title.Trim(),DueAt=request.DueAt,Notes=request.Notes};
  db.CorporateCases.Add(item); await db.SaveChangesAsync(); return Ok(new{item.Id,item.Status});
 }
 [HttpPut("companies/{companyId:guid}/cases/{caseId:guid}")]
 public async Task<IActionResult> UpdateCase(Guid companyId,Guid caseId,UpdateCorporateCaseRequest request) {
  var uid=await UserId(); if(!await Hr(companyId,uid)) return Forbid();
  if(!new[]{"Open","InProgress","Blocked","Closed"}.Contains(request.Status)) return BadRequest(new{message="Invalid status"});
  var item=await db.CorporateCases.FirstOrDefaultAsync(x=>x.Id==caseId && x.CompanyId==companyId);
  if(item==null) return NotFound();
  item.Status=request.Status; if (request.DueAt.HasValue) item.DueAt=request.DueAt.Value; if (request.Notes is not null) item.Notes=request.Notes;
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
