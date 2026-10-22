using ExpatOne.Application.Common;
using ExpatOne.Application.DTOs;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Tests;

public sealed class CorporateMobilityServiceTests : IDisposable
{
    private readonly ExpatOneDbContext db;
    private readonly CorporateMobilityService service;

    public CorporateMobilityServiceTests()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        db = new ExpatOneDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        service = new CorporateMobilityService(db);
    }

    public void Dispose() => db.Dispose();

    private async Task<User> AddUserAsync(
        string email,
        bool isActive = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalId = Guid.NewGuid().ToString(),
            Email = email,
            IsActive = isActive
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private async Task<CorporateCompany> AddCompanyAsync(User owner)
    {
        var company = new CorporateCompany
        {
            Id = Guid.NewGuid(),
            Name = "Example Company",
            CreatedByUserId = owner.Id
        };

        db.CorporateCompanies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    private async Task AddMembershipAsync(
        Guid companyId,
        Guid userId,
        string role,
        string status = "Active")
    {
        db.CorporateMemberships.Add(new CorporateMembership
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            Role = role,
            Status = status
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateCompanyCreatesActiveAdminMembership()
    {
        var owner = await AddUserAsync("owner@example.com");

        var result = await service.CreateCompanyAsync(
            owner.Id,
            new CreateCorporateCompanyDto { Name = "  Acme Mobility  " });

        Assert.Equal("Acme Mobility", result.Name);
        Assert.Equal("Admin", result.Role);

        var company = await db.CorporateCompanies
            .SingleAsync(c => c.Id == result.Id);

        var membership = await db.CorporateMemberships
            .SingleAsync(m => m.CompanyId == result.Id);

        Assert.Equal(owner.Id, company.CreatedByUserId);
        Assert.Equal(owner.Id, membership.UserId);
        Assert.Equal("Admin", membership.Role);
        Assert.Equal("Active", membership.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public async Task CreateCompanyRejectsInvalidNames(string name)
    {
        var owner = await AddUserAsync("owner@example.com");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateCompanyAsync(
                owner.Id,
                new CreateCorporateCompanyDto { Name = name }));

        Assert.Empty(await db.CorporateCompanies.ToListAsync());
        Assert.Empty(await db.CorporateMemberships.ToListAsync());
    }

    [Fact]
    public async Task CreateCompanyRejectsInactiveUser()
    {
        var user = await AddUserAsync(
            "inactive@example.com",
            isActive: false);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CreateCompanyAsync(
                user.Id,
                new CreateCorporateCompanyDto { Name = "Acme Mobility" }));

        Assert.Empty(await db.CorporateCompanies.ToListAsync());
        Assert.Empty(await db.CorporateMemberships.ToListAsync());
    }

    [Fact]
    public async Task ListCompaniesReturnsOnlyActiveMemberships()
    {
        var owner = await AddUserAsync("owner@example.com");
        var employee = await AddUserAsync("employee@example.com");
        var company1 = await AddCompanyAsync(owner);
        var company2 = new CorporateCompany
        {
            Id = Guid.NewGuid(),
            Name = "Second Company",
            CreatedByUserId = owner.Id
        };

        db.CorporateCompanies.Add(company2);
        await db.SaveChangesAsync();

        await AddMembershipAsync(company1.Id, employee.Id, "Employee");
        await AddMembershipAsync(
            company2.Id, employee.Id, "Employee", "Suspended");

        var result = await service.ListCompaniesAsync(employee.Id);

        var listed = Assert.Single(result);
        Assert.Equal(company1.Id, listed.Id);
        Assert.Equal("Employee", listed.Role);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("HR")]
    [InlineData("Viewer")]
    public async Task PermittedRolesCanListCompanyMembers(string role)
    {
        var owner = await AddUserAsync("owner@example.com");
        var member = await AddUserAsync("member@example.com");
        var company = await AddCompanyAsync(owner);

        await AddMembershipAsync(company.Id, owner.Id, role);
        await AddMembershipAsync(company.Id, member.Id, "Employee");

        var result = await service.ListMembersAsync(owner.Id, company.Id);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, m => m.Email == "member@example.com");
    }

    [Fact]
    public async Task EmployeeCannotListCompanyMembers()
    {
        var owner = await AddUserAsync("owner@example.com");
        var company = await AddCompanyAsync(owner);
        await AddMembershipAsync(company.Id, owner.Id, "Employee");

        await Assert.ThrowsAsync<CorporateAuthorizationException>(() =>
            service.ListMembersAsync(owner.Id, company.Id));
    }

    [Fact]
    public async Task SuspendedMemberCannotListCompanyMembers()
    {
        var owner = await AddUserAsync("owner@example.com");
        var company = await AddCompanyAsync(owner);
        await AddMembershipAsync(
            company.Id, owner.Id, "Admin", "Suspended");

        await Assert.ThrowsAsync<CorporateAuthorizationException>(() =>
            service.ListMembersAsync(owner.Id, company.Id));
    }

    [Fact]
    public async Task NonMemberCannotListCompanyMembers()
    {
        var owner = await AddUserAsync("owner@example.com");
        var stranger = await AddUserAsync("stranger@example.com");
        var company = await AddCompanyAsync(owner);

        await Assert.ThrowsAsync<CorporateAuthorizationException>(() =>
            service.ListMembersAsync(stranger.Id, company.Id));
    }

    [Fact]
    public async Task MemberCannotListMembersOfAnotherCompany()
    {
        var firstOwner = await AddUserAsync("first@example.com");
        var secondOwner = await AddUserAsync("second@example.com");
        var firstCompany = await AddCompanyAsync(firstOwner);
        var secondCompany = await AddCompanyAsync(secondOwner);

        await AddMembershipAsync(firstCompany.Id, firstOwner.Id, "Admin");

        await Assert.ThrowsAsync<CorporateAuthorizationException>(() =>
            service.ListMembersAsync(firstOwner.Id, secondCompany.Id));
    }

[Fact]
    public async Task AddMemberCreatesActiveMembership()
    {
        var admin = await AddUserAsync("admin@example.com");
        var member = await AddUserAsync("newmember@example.com");
        var company = await AddCompanyAsync(admin);
        await AddMembershipAsync(company.Id, admin.Id, "Admin");

        var result = await service.AddMemberAsync(
            admin.Id,
            company.Id,
            new AddCorporateMemberDto
            {
                Email = " NEWMEMBER@example.com ",
                Role = "Employee"
            });

        Assert.Equal(member.Id, result.UserId);
        Assert.Equal("Employee", result.Role);
        Assert.Equal("Active", result.Status);

        Assert.True(await db.CorporateMemberships.AnyAsync(m =>
            m.CompanyId == company.Id &&
            m.UserId == member.Id &&
            m.Role == "Employee" &&
            m.Status == "Active"));
    }

    [Fact]
    public async Task HrCannotAddAdmin()
    {
        var hr = await AddUserAsync("hr@example.com");
        var target = await AddUserAsync("target@example.com");
        var company = await AddCompanyAsync(hr);
        await AddMembershipAsync(company.Id, hr.Id, "HR");

        await Assert.ThrowsAsync<CorporateAuthorizationException>(() =>
            service.AddMemberAsync(
                hr.Id,
                company.Id,
                new AddCorporateMemberDto
                {
                    Email = target.Email,
                    Role = "Admin"
                }));

        Assert.False(await db.CorporateMemberships.AnyAsync(m =>
            m.CompanyId == company.Id && m.UserId == target.Id));
    }

    [Fact]
    public async Task EmployeeCannotAddMember()
    {
        var employee = await AddUserAsync("employee@example.com");
        var target = await AddUserAsync("target@example.com");
        var company = await AddCompanyAsync(employee);
        await AddMembershipAsync(company.Id, employee.Id, "Employee");

        await Assert.ThrowsAsync<CorporateAuthorizationException>(() =>
            service.AddMemberAsync(
                employee.Id,
                company.Id,
                new AddCorporateMemberDto
                {
                    Email = target.Email,
                    Role = "Employee"
                }));

        Assert.False(await db.CorporateMemberships.AnyAsync(m =>
            m.CompanyId == company.Id && m.UserId == target.Id));
    }

    [Fact]
    public async Task AdminCanUpdateMemberRoleAndStatus()
    {
        var admin = await AddUserAsync("admin@example.com");
        var member = await AddUserAsync("member@example.com");
        var company = await AddCompanyAsync(admin);
        await AddMembershipAsync(company.Id, admin.Id, "Admin");
        await AddMembershipAsync(company.Id, member.Id, "Employee");

        var membership = await db.CorporateMemberships.SingleAsync(m =>
            m.CompanyId == company.Id && m.UserId == member.Id);

        var roleResult = await service.UpdateMemberRoleAsync(
            admin.Id,
            company.Id,
            membership.Id,
            new UpdateCorporateMemberRoleDto { Role = "Viewer" });

        Assert.Equal("Viewer", roleResult.Role);

        var statusResult = await service.UpdateMemberStatusAsync(
            admin.Id,
            company.Id,
            membership.Id,
            new UpdateCorporateMemberStatusDto { Status = "Suspended" });

        Assert.Equal("Suspended", statusResult.Status);
    }

    [Fact]
    public async Task LastActiveAdminCannotBeDemotedOrSuspended()
    {
        var admin = await AddUserAsync("admin@example.com");
        var company = await AddCompanyAsync(admin);
        await AddMembershipAsync(company.Id, admin.Id, "Admin");

        var membership = await db.CorporateMemberships.SingleAsync(m =>
            m.CompanyId == company.Id && m.UserId == admin.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateMemberRoleAsync(
                admin.Id,
                company.Id,
                membership.Id,
                new UpdateCorporateMemberRoleDto { Role = "Employee" }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateMemberStatusAsync(
                admin.Id,
                company.Id,
                membership.Id,
                new UpdateCorporateMemberStatusDto { Status = "Suspended" }));

        db.ChangeTracker.Clear();

        var unchanged = await db.CorporateMemberships.SingleAsync(m =>
            m.CompanyId == company.Id && m.UserId == admin.Id);

        Assert.Equal("Admin", unchanged.Role);
        Assert.Equal("Active", unchanged.Status);
    }
}
