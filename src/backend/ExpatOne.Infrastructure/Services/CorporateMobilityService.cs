using ExpatOne.Application.Common;
using ExpatOne.Application.DTOs;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Infrastructure.Services;

public sealed class CorporateMobilityService(ExpatOneDbContext db)
{
    private static readonly string[] MemberListRoles = ["Admin", "HR", "Viewer"];
    private static readonly string[] SupportedRoles = ["Admin", "HR", "Viewer", "Employee"];

    public async Task<CorporateCompanyDto> CreateCompanyAsync(
        Guid userId, CreateCorporateCompanyDto dto)
    {
        var name = dto.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || name.Length > 180)
            throw new ArgumentException("Company name must contain between 2 and 180 characters.");

        var userExists = await db.Users.AnyAsync(u => u.Id == userId && u.IsActive);
        if (!userExists)
            throw new KeyNotFoundException("The active user account was not found.");

        var company = new CorporateCompany
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedByUserId = userId
        };

        var membership = new CorporateMembership
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            UserId = userId,
            Role = "Admin",
            Status = "Active"
        };

        // Both records are persisted in the same SaveChanges operation.
        db.CorporateCompanies.Add(company);
        db.CorporateMemberships.Add(membership);
        await db.SaveChangesAsync();

        return new(company.Id, company.Name, membership.Role, company.CreatedAt);
    }

    public async Task<List<CorporateCompanyDto>> ListCompaniesAsync(Guid userId)
    {
        return await (
            from membership in db.CorporateMemberships.AsNoTracking()
            join company in db.CorporateCompanies.AsNoTracking()
                on membership.CompanyId equals company.Id
            where membership.UserId == userId
                && membership.Status == "Active"
            orderby company.Name
            select new CorporateCompanyDto(
                company.Id, company.Name, membership.Role, company.CreatedAt)
        ).ToListAsync();
    }

    public async Task<List<CorporateMemberDto>> ListMembersAsync(
        Guid userId, Guid companyId)
    {
        var caller = await db.CorporateMemberships.AsNoTracking()
            .SingleOrDefaultAsync(m =>
                m.CompanyId == companyId &&
                m.UserId == userId &&
                m.Status == "Active");

        if (caller is null)
            throw new CorporateAuthorizationException();

        if (!MemberListRoles.Contains(caller.Role))
            throw new CorporateAuthorizationException();

        var companyExists = await db.CorporateCompanies
            .AnyAsync(c => c.Id == companyId);

        if (!companyExists)
            throw new KeyNotFoundException("Company was not found.");

        return await (
            from membership in db.CorporateMemberships.AsNoTracking()
            join user in db.Users.AsNoTracking()
                on membership.UserId equals user.Id
            where membership.CompanyId == companyId
            orderby user.Email
            select new CorporateMemberDto(
                membership.Id,
                user.Id,
                user.Email,
                user.DisplayName,
                membership.Role,
                membership.Status,
                membership.CreatedAt)
        ).ToListAsync();
    }
}
