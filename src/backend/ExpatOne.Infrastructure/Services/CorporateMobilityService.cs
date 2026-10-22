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

    private static readonly string[] SupportedStatuses = ["Active", "Suspended"];

    public async Task<CorporateMemberDto> AddMemberAsync(
        Guid userId, Guid companyId, AddCorporateMemberDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var caller = await RequireManagerAsync(userId, companyId);

        var email = dto.Email?.Trim();
        var role = dto.Role?.Trim();

        if (string.IsNullOrWhiteSpace(email) || email.Length > 256)
            throw new ArgumentException("A valid email address is required.");

        try
        {
            var parsed = new System.Net.Mail.MailAddress(email);
            if (!string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A valid email address is required.");
        }
        catch (FormatException)
        {
            throw new ArgumentException("A valid email address is required.");
        }

        if (string.IsNullOrWhiteSpace(role) || !SupportedRoles.Contains(role))
            throw new ArgumentException("Unsupported corporate member role.");

        if (caller.Role == "HR" && role is not ("Employee" or "Viewer"))
            throw new CorporateAuthorizationException();

        var user = await db.Users.SingleOrDefaultAsync(u =>
            u.IsActive && u.Email.ToLower() == email.ToLower());

        if (user is null)
            throw new KeyNotFoundException(
                "No active ExpatOne user account was found for that email.");

        var existing = await db.CorporateMemberships.AnyAsync(m =>
            m.CompanyId == companyId && m.UserId == user.Id);

        if (existing)
            throw new InvalidOperationException(
                "This user already has a membership in the company.");

        var membership = new CorporateMembership
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = user.Id,
            Role = role,
            Status = "Active"
        };

        db.CorporateMemberships.Add(membership);
        await db.SaveChangesAsync();

        return new CorporateMemberDto(
            membership.Id, user.Id, user.Email, user.DisplayName,
            membership.Role, membership.Status, membership.CreatedAt);
    }

    public async Task<CorporateMemberDto> UpdateMemberRoleAsync(
        Guid userId, Guid companyId, Guid membershipId,
        UpdateCorporateMemberRoleDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await RequireAdminAsync(userId, companyId);

        var role = dto.Role?.Trim();
        if (string.IsNullOrWhiteSpace(role) || !SupportedRoles.Contains(role))
            throw new ArgumentException("Unsupported corporate member role.");

        var membership = await db.CorporateMemberships.SingleOrDefaultAsync(m =>
            m.Id == membershipId && m.CompanyId == companyId);

        if (membership is null)
            throw new KeyNotFoundException("Corporate membership was not found.");

        if (membership.Status == "Active" &&
            membership.Role == "Admin" && role != "Admin")
        {
            await EnsureAnotherActiveAdminAsync(companyId, membership.Id);
        }

        membership.Role = role;
        await db.SaveChangesAsync();

        return await MapMemberAsync(membership);
    }

    public async Task<CorporateMemberDto> UpdateMemberStatusAsync(
        Guid userId, Guid companyId, Guid membershipId,
        UpdateCorporateMemberStatusDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await RequireAdminAsync(userId, companyId);

        var status = dto.Status?.Trim();
        if (string.IsNullOrWhiteSpace(status) || !SupportedStatuses.Contains(status))
            throw new ArgumentException("Unsupported corporate member status.");

        var membership = await db.CorporateMemberships.SingleOrDefaultAsync(m =>
            m.Id == membershipId && m.CompanyId == companyId);

        if (membership is null)
            throw new KeyNotFoundException("Corporate membership was not found.");

        if (membership.Status == "Active" &&
            membership.Role == "Admin" && status == "Suspended")
        {
            await EnsureAnotherActiveAdminAsync(companyId, membership.Id);
        }

        membership.Status = status;
        await db.SaveChangesAsync();

        return await MapMemberAsync(membership);
    }

    private async Task<CorporateMembership> RequireManagerAsync(
        Guid userId, Guid companyId)
    {
        var companyExists = await db.CorporateCompanies
            .AnyAsync(c => c.Id == companyId);

        if (!companyExists)
            throw new KeyNotFoundException("Company was not found.");

        var caller = await db.CorporateMemberships.SingleOrDefaultAsync(m =>
            m.CompanyId == companyId &&
            m.UserId == userId &&
            m.Status == "Active");

        if (caller is null || caller.Role is not ("Admin" or "HR"))
            throw new CorporateAuthorizationException();

        return caller;
    }

    private async Task RequireAdminAsync(Guid userId, Guid companyId)
    {
        var companyExists = await db.CorporateCompanies
            .AnyAsync(c => c.Id == companyId);

        if (!companyExists)
            throw new KeyNotFoundException("Company was not found.");

        var isAdmin = await db.CorporateMemberships.AnyAsync(m =>
            m.CompanyId == companyId &&
            m.UserId == userId &&
            m.Status == "Active" &&
            m.Role == "Admin");

        if (!isAdmin)
            throw new CorporateAuthorizationException();
    }

    private async Task EnsureAnotherActiveAdminAsync(
        Guid companyId, Guid membershipId)
    {
        var otherActiveAdminExists = await db.CorporateMemberships.AnyAsync(m =>
            m.CompanyId == companyId &&
            m.Id != membershipId &&
            m.Status == "Active" &&
            m.Role == "Admin");

        if (!otherActiveAdminExists)
            throw new InvalidOperationException(
                "The company's last active Admin cannot be demoted or suspended.");
    }

    private async Task<CorporateMemberDto> MapMemberAsync(
        CorporateMembership membership)
    {
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == membership.UserId);

        if (user is null)
            throw new KeyNotFoundException("Member user account was not found.");

        return new CorporateMemberDto(
            membership.Id, user.Id, user.Email, user.DisplayName,
            membership.Role, membership.Status, membership.CreatedAt);
    }
}
