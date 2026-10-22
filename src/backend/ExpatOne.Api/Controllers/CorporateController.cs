using System.Security.Claims;
using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;
using ExpatOne.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExpatOne.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/corporate")]
[EnableRateLimiting("family-per-user")]
public sealed class CorporateController(
    CorporateMobilityService corporate,
    IUserService users) : ControllerBase
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
