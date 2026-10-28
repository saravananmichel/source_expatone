using ExpatOne.Application.DTOs;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExpatOne.Tests;

public class FamilyVisaConsentTests
{
    [Fact]
    public void NewConnection_DoesNotGrantVisaUseByDefault()
    {
        var request = new FamilyRequest();
        Assert.False(request.AllowVisaApplicationUse);
        Assert.Equal("Other", request.Relationship);
    }

    [Fact]
    public void MissingConsentInLegacyRequest_RemainsOptedOut()
    {
        var decision = new FamilyDecisionDto { Accept = true };
        var grants = new FamilyGrantsDto();
        Assert.Null(decision.AllowVisaApplicationUse);
        Assert.Null(grants.AllowVisaApplicationUse);
    }

    [Fact]
    public void ConsentIsMappedToFamilyRequest()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase($"family-consent-{Guid.NewGuid()}").Options;
        using var db = new ExpatOneDbContext(options);
        var entity = db.Model.FindEntityType(typeof(FamilyRequest));
        Assert.NotNull(entity);
        var property = entity!.FindProperty(nameof(FamilyRequest.AllowVisaApplicationUse));
        Assert.NotNull(property);
        Assert.Equal(false, property!.GetDefaultValue());
    }
}
