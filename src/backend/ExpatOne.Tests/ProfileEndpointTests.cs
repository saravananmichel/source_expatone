using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpatOne.Application.DTOs;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExpatOne.Tests;

[Collection("Integration")]
public class ProfileEndpointTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProfileEndpointTests(AppFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ExpatOneDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                var dbName = $"ProfileTests_{Guid.NewGuid()}";
                services.AddDbContext<ExpatOneDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);
            });
        });
    }

    private static AuthenticationHeaderValue Auth(string uid, string email, string name) =>
        new("Test", $"uid={uid}&email={Uri.EscapeDataString(email)}&name={Uri.EscapeDataString(name)}");

    [Fact]
    public async Task GetProfileOptions_ReturnsOptions_WithoutAuth()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/profile/options");
        response.EnsureSuccessStatusCode();

        var options = await response.Content.ReadFromJsonAsync<ProfileOptionsDto>();
        Assert.NotNull(options);
        Assert.NotEmpty(options.VisaPassTypes);
        Assert.NotEmpty(options.EmploymentStatuses);
        Assert.NotEmpty(options.FamilyStatuses);
        Assert.NotEmpty(options.SupportedLanguages);
    }

    [Fact]
    public async Task GetProfileOptions_ContainsExpectedValues()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/profile/options");
        var options = await response.Content.ReadFromJsonAsync<ProfileOptionsDto>();

        Assert.Contains(options!.VisaPassTypes, v => v.Value == "EmploymentPass");
        Assert.Contains(options.VisaPassTypes, v => v.Value == "MM2H");
        Assert.Contains(options.EmploymentStatuses, v => v.Value == "Employed");
        Assert.Contains(options.FamilyStatuses, v => v.Value == "Single");
        Assert.Contains(options.SupportedLanguages, v => v.Value == "en");
        Assert.Contains(options.SupportedLanguages, v => v.Value == "ms");
    }

    [Fact]
    public async Task GetChecklist_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/profile/checklist");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetChecklist_WithAuth_ReturnsChecklist()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("checklist-user", "cl@test.com", "CL User");

        var response = await client.GetAsync("/api/profile/checklist");
        response.EnsureSuccessStatusCode();

        var checklist = await response.Content.ReadFromJsonAsync<List<ChecklistItemDto>>();
        Assert.NotNull(checklist);
        Assert.NotEmpty(checklist);
        Assert.Contains(checklist, c => c.Id == "passport");
        Assert.Contains(checklist, c => c.Id == "explore-assistant");
        Assert.Contains(checklist, c => c.Id == "setup-reminders");
    }

    [Fact]
    public async Task GetMe_ReturnsNewProfileFields()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("profile-fields", "pf@test.com", "PF User");

        var response = await client.GetAsync("/api/users/me");
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(user);
        Assert.Null(user.Nationality);
        Assert.Null(user.VisaPassType);
        Assert.Null(user.EmploymentStatus);
        Assert.Null(user.FamilyStatus);
        Assert.Null(user.HasChildren);
        Assert.Null(user.NumberOfChildren);
        Assert.False(user.OnboardingCompleted);
    }

    [Fact]
    public async Task PutMe_UpdatesAllProfileFields()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("profile-update", "pu@test.com", "PU User");

        await client.GetAsync("/api/users/me");

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            Nationality = "GB",
            ResidenceLocation = "Kuala Lumpur",
            VisaPassType = "EmploymentPass",
            EmploymentStatus = "Employed",
            FamilyStatus = "Married",
            HasChildren = true,
            NumberOfChildren = 2,
            PreferredLanguage = "ms",
        });
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("GB", updated!.Nationality);
        Assert.Equal("Kuala Lumpur", updated.ResidenceLocation);
        Assert.Equal("EmploymentPass", updated.VisaPassType);
        Assert.Equal("Employed", updated.EmploymentStatus);
        Assert.Equal("Married", updated.FamilyStatus);
        Assert.True(updated.HasChildren);
        Assert.Equal(2, updated.NumberOfChildren);
        Assert.Equal("ms", updated.PreferredLanguage);
    }

    [Fact]
    public async Task PutMe_ProfileUpdatePersists()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("persist-user", "persist@test.com", "Persist");

        await client.GetAsync("/api/users/me");

        await client.PutAsJsonAsync("/api/users/me", new
        {
            VisaPassType = "MM2H",
            Nationality = "AU",
        });

        var getResponse = await client.GetAsync("/api/users/me");
        var fetched = await getResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("MM2H", fetched!.VisaPassType);
        Assert.Equal("AU", fetched.Nationality);
    }

    [Fact]
    public async Task PutMe_EmailCannotBeChanged()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("email-immutable", "original@test.com", "Email User");

        await client.GetAsync("/api/users/me");

        await client.PutAsJsonAsync("/api/users/me", new
        {
            DisplayName = "Changed Name",
        });

        var getResponse = await client.GetAsync("/api/users/me");
        var user = await getResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("original@test.com", user!.Email);
    }

    [Fact]
    public async Task PutMe_InvalidVisaType_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("bad-visa", "bv@test.com", "BV User");

        await client.GetAsync("/api/users/me");

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            VisaPassType = "InvalidType",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_InvalidLanguage_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("bad-lang", "bl@test.com", "BL User");

        await client.GetAsync("/api/users/me");

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            PreferredLanguage = "xx",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_InvalidEmploymentStatus_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("bad-emp", "be@test.com", "BE User");

        await client.GetAsync("/api/users/me");

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            EmploymentStatus = "FakeStatus",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_InvalidFamilyStatus_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("bad-fam", "bf@test.com", "BF User");

        await client.GetAsync("/api/users/me");

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            FamilyStatus = "Complicated",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_InvalidNationality_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("bad-nat", "bn@test.com", "BN User");

        await client.GetAsync("/api/users/me");

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            Nationality = "X",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_NumberOfChildrenTooHigh_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("bad-kids", "bk@test.com", "BK User");

        await client.GetAsync("/api/users/me");

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            NumberOfChildren = 25,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_NumberOfChildrenNegative_Returns400()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("neg-kids", "nk@test.com", "NK User");

        await client.GetAsync("/api/users/me");

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            NumberOfChildren = -1,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PutMe_OnboardingCompleted_Persists()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("onboard-user", "ob@test.com", "OB User");

        await client.GetAsync("/api/users/me");

        var response = await client.PutAsJsonAsync("/api/users/me", new
        {
            OnboardingCompleted = true,
        });
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.True(updated!.OnboardingCompleted);

        var getResponse = await client.GetAsync("/api/users/me");
        var fetched = await getResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.True(fetched!.OnboardingCompleted);
    }

    [Fact]
    public async Task PutMe_PartialOnboarding_SavesSomeFields()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("partial-user", "part@test.com", "Part User");

        await client.GetAsync("/api/users/me");

        await client.PutAsJsonAsync("/api/users/me", new
        {
            Nationality = "IN",
            VisaPassType = "StudentPass",
        });

        var getResponse = await client.GetAsync("/api/users/me");
        var user = await getResponse.Content.ReadFromJsonAsync<UserDto>();

        Assert.Equal("IN", user!.Nationality);
        Assert.Equal("StudentPass", user.VisaPassType);
        Assert.Null(user.EmploymentStatus);
        Assert.Null(user.FamilyStatus);
        Assert.False(user.OnboardingCompleted);
    }

    [Fact]
    public async Task NewUser_DefaultsOnboardingFalse()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("new-default", "nd@test.com", "New Default");

        var response = await client.GetAsync("/api/users/me");
        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.False(user!.OnboardingCompleted);
        Assert.Equal("en", user.PreferredLanguage);
        Assert.Equal("MY", user.CountryCode);
    }

    [Fact]
    public async Task GetChecklist_WithVisaType_IncludesVisaItem()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("visa-cl-user", "vcl@test.com", "VCL User");

        await client.GetAsync("/api/users/me");
        await client.PutAsJsonAsync("/api/users/me", new
        {
            VisaPassType = "EmploymentPass",
        });

        var response = await client.GetAsync("/api/profile/checklist");
        var checklist = await response.Content.ReadFromJsonAsync<List<ChecklistItemDto>>();

        Assert.Contains(checklist!, c => c.Id == "visa-document");
    }

    [Fact]
    public async Task GetChecklist_WithEmployed_IncludesEmploymentItem()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("emp-cl-user", "ecl@test.com", "ECL User");

        await client.GetAsync("/api/users/me");
        await client.PutAsJsonAsync("/api/users/me", new
        {
            EmploymentStatus = "Employed",
        });

        var response = await client.GetAsync("/api/profile/checklist");
        var checklist = await response.Content.ReadFromJsonAsync<List<ChecklistItemDto>>();

        Assert.Contains(checklist!, c => c.Id == "employment-docs");
    }

    [Fact]
    public async Task GetChecklist_WithChildren_IncludesFamilyItem()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = Auth("fam-cl-user", "fcl@test.com", "FCL User");

        await client.GetAsync("/api/users/me");
        await client.PutAsJsonAsync("/api/users/me", new
        {
            HasChildren = true,
            NumberOfChildren = 1,
        });

        var response = await client.GetAsync("/api/profile/checklist");
        var checklist = await response.Content.ReadFromJsonAsync<List<ChecklistItemDto>>();

        Assert.Contains(checklist!, c => c.Id == "family-docs");
    }
}
