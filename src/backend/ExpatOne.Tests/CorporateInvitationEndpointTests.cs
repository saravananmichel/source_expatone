using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ExpatOne.Tests;

[Collection("Integration")]
public sealed class CorporateInvitationEndpointTests
{
    private readonly AppFactory _baseFactory;

    public CorporateInvitationEndpointTests(AppFactory factory)
    {
        _baseFactory = factory;
    }

    private sealed class Scenario : IDisposable
    {
        public required WebApplicationFactory<Program> Factory { get; init; }
        public required HttpClient Owner { get; init; }
        public required HttpClient Employee { get; init; }
        public required Guid CompanyId { get; init; }
        public required Guid OwnerId { get; init; }
        public required Guid EmployeeId { get; init; }

        public void Dispose()
        {
            Owner.Dispose();
            Employee.Dispose();
            Factory.Dispose();
        }
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        var databaseName = $"CorporateInvitationTests_{Guid.NewGuid()}";

        return _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ExpatOneDbContext>>();

                services.AddDbContext<ExpatOneDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    "Test", _ => { });
            });
        });
    }

    private static HttpClient CreateAuthClient(
        WebApplicationFactory<Program> factory,
        string uid,
        string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Test", $"uid={uid}&email={email}&name=CorporateTest");

        return client;
    }

    private static async Task<HttpClient> CreateProvisionedClient(
        WebApplicationFactory<Program> factory,
        string uid,
        string email)
    {
        var client = CreateAuthClient(factory, uid, email);
        var response = await client.GetAsync("/api/users/me");

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            client.Dispose();
            throw new InvalidOperationException(
                $"User provisioning failed: {(int)response.StatusCode} {body}");
        }

        return client;
    }

    private async Task<Scenario> CreateScenarioAsync()
    {
        var factory = CreateFactory();

        try
        {
            var owner = await CreateProvisionedClient(
                factory, "corporate-owner-uid", "owner@example.com");

            var employee = await CreateProvisionedClient(
                factory, "corporate-employee-uid", "employee@example.com");

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();

            var ownerUser = await db.Users.SingleAsync(
                x => x.ExternalProvider == "firebase"
                  && x.ExternalId == "corporate-owner-uid");

            var employeeUser = await db.Users.SingleAsync(
                x => x.ExternalProvider == "firebase"
                  && x.ExternalId == "corporate-employee-uid");

            var companyId = Guid.NewGuid();

            db.CorporateCompanies.Add(new CorporateCompany
            {
                Id = companyId,
                Name = "Corporate Invitation Test",
                CreatedByUserId = ownerUser.Id
            });

            db.CorporateMemberships.Add(new CorporateMembership
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = ownerUser.Id,
                Role = "Admin",
                Status = "Active"
            });

            await db.SaveChangesAsync();

            return new Scenario
            {
                Factory = factory,
                Owner = owner,
                Employee = employee,
                CompanyId = companyId,
                OwnerId = ownerUser.Id,
                EmployeeId = employeeUser.Id
            };
        }
        catch
        {
            factory.Dispose();
            throw;
        }
    }

    [Fact]
    public async Task CasePagination_IsStable_AndKeepsEmployeeScope()
    {
        using var s = await CreateScenarioAsync();
        using (var scope = s.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
            db.CorporateMemberships.Add(new CorporateMembership {
                Id = Guid.NewGuid(), CompanyId = s.CompanyId, UserId = s.EmployeeId,
                Role = "Viewer", Status = "Active"
            });
            for (var i = 0; i < 251; i++)
                db.CorporateCases.Add(new CorporateCase {
                    Id = Guid.NewGuid(), CompanyId = s.CompanyId,
                    EmployeeUserId = s.EmployeeId, Title = $"Task {i}",
                    DueAt = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });
            db.CorporateCases.Add(new CorporateCase {
                Id = Guid.NewGuid(), CompanyId = s.CompanyId,
                EmployeeUserId = s.OwnerId, Title = "Other employee task"
            });
            await db.SaveChangesAsync();
        }
        var url = $"/api/corporate/companies/{s.CompanyId}/cases";
        var first = await s.Employee.GetFromJsonAsync<System.Text.Json.JsonElement[]>(url);
        var second = await s.Employee.GetFromJsonAsync<System.Text.Json.JsonElement[]>(url + "?skip=250");
        Assert.Equal(250, first!.Length);
        Assert.Single(second!);
        var ids = first.Concat(second!).Select(x => x.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(251, ids.Distinct().Count());
        Assert.All(first.Concat(second!), x => Assert.Equal(s.EmployeeId, x.GetProperty("employeeUserId").GetGuid()));
        var ownerSecond = await s.Owner.GetFromJsonAsync<System.Text.Json.JsonElement[]>(url + "?skip=250");
        Assert.Equal(2, ownerSecond!.Length);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Owner.GetAsync(url + "?skip=-1")).StatusCode);
    }

    private static async Task<Guid> InviteEmployeeAsync(Scenario s)
    {
        var response = await s.Owner.PostAsJsonAsync(
            $"/api/corporate/companies/{s.CompanyId}/invite",
            new { email = "  EMPLOYEE@example.com  " });

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<InvitationResult>();
        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);

        return result.Id;
    }

    private sealed record InvitationResult(Guid Id, string Status);

    [Fact]
    public async Task Invite_CreatesPendingInvitation_AndEmployeeCanSeeIt()
    {
        using var s = await CreateScenarioAsync();

        var invitationId = await InviteEmployeeAsync(s);

        var response = await s.Employee.GetAsync("/api/corporate/invitations");

        response.EnsureSuccessStatusCode();

        var invitations =
            await response.Content.ReadFromJsonAsync<List<InvitationListItem>>();

        Assert.NotNull(invitations);
        var invitation = Assert.Single(invitations);
        Assert.Equal(invitationId, invitation.Id);
        Assert.Equal(s.CompanyId, invitation.CompanyId);
        Assert.Equal("Corporate Invitation Test", invitation.Name);
        Assert.Equal("Employee", invitation.Role);
    }

    [Fact]
    public async Task Accept_ActivatesInvitation_ForInvitedEmployee()
    {
        using var s = await CreateScenarioAsync();
        var invitationId = await InviteEmployeeAsync(s);

        var response = await s.Employee.PostAsync(
            $"/api/corporate/invitations/{invitationId}/accept", null);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<InvitationResult>();
        Assert.NotNull(result);
        Assert.Equal(invitationId, result.Id);
        Assert.Equal("Active", result.Status);

        using var scope = s.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
        var membership = await db.CorporateMemberships.SingleAsync(
            x => x.Id == invitationId);

        Assert.Equal("Active", membership.Status);
        Assert.Equal(s.EmployeeId, membership.UserId);
    }

    [Fact]
    public async Task Decline_RemovesInvitationFromPendingList()
    {
        using var s = await CreateScenarioAsync();
        var invitationId = await InviteEmployeeAsync(s);

        var response = await s.Employee.PostAsync(
            $"/api/corporate/invitations/{invitationId}/decline", null);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<InvitationResult>();
        Assert.NotNull(result);
        Assert.Equal("Declined", result.Status);

        var listResponse = await s.Employee.GetAsync("/api/corporate/invitations");
        listResponse.EnsureSuccessStatusCode();

        var invitations =
            await listResponse.Content.ReadFromJsonAsync<List<InvitationListItem>>();

        Assert.NotNull(invitations);
        Assert.Empty(invitations);
    }

    [Fact]
    public async Task DuplicateInvite_ReturnsConflict()
    {
        using var s = await CreateScenarioAsync();
        await InviteEmployeeAsync(s);

        var response = await s.Owner.PostAsJsonAsync(
            $"/api/corporate/companies/{s.CompanyId}/invite",
            new { email = "employee@example.com" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeclinedInvitation_CanBeSentAgain()
    {
        using var s = await CreateScenarioAsync();
        var firstId = await InviteEmployeeAsync(s);

        var declineResponse = await s.Employee.PostAsync(
            $"/api/corporate/invitations/{firstId}/decline", null);

        declineResponse.EnsureSuccessStatusCode();

        var secondResponse = await s.Owner.PostAsJsonAsync(
            $"/api/corporate/companies/{s.CompanyId}/invite",
            new { email = "employee@example.com" });

        secondResponse.EnsureSuccessStatusCode();

        var result = await secondResponse.Content.ReadFromJsonAsync<InvitationResult>();
        Assert.NotNull(result);
        Assert.Equal(firstId, result.Id);
        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task NonHrMember_CannotInviteEmployee()
    {
        using var s = await CreateScenarioAsync();

        // Promote the test employee to an active ordinary Employee membership.
        using (var scope = s.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ExpatOneDbContext>();
            db.CorporateMemberships.Add(new CorporateMembership
            {
                Id = Guid.NewGuid(),
                CompanyId = s.CompanyId,
                UserId = s.EmployeeId,
                Role = "Employee",
                Status = "Active"
            });
            await db.SaveChangesAsync();
        }

        var response = await s.Employee.PostAsJsonAsync(
            $"/api/corporate/companies/{s.CompanyId}/invite",
            new { email = "owner@example.com" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnotherUser_CannotAcceptSomeoneElsesInvitation()
    {
        using var s = await CreateScenarioAsync();
        var invitationId = await InviteEmployeeAsync(s);

        var response = await s.Owner.PostAsync(
            $"/api/corporate/invitations/{invitationId}/accept", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record InvitationListItem(
        Guid Id,
        Guid CompanyId,
        string Name,
        string Role);
}
