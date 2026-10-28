using ExpatOne.Application.DTOs;
using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using ExpatOne.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
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
    [Theory]
    [InlineData(true, 2, false, true)]
    [InlineData(false, 2, false, false)]
    [InlineData(true, 1, false, false)]
    [InlineData(true, 5, false, false)]
    [InlineData(true, 2, true, false)]
    public async Task SharedVisaDocument_RequiresActiveConsent(
        bool consent, int status, bool expired, bool expected)
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase($"visa-access-{Guid.NewGuid()}").Options;
        using var db = new ExpatOneDbContext(options);
        var requester = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var request = new FamilyRequest {
            Id = Guid.NewGuid(), RequesterUserId = requester, RecipientUserId = owner,
            StatusId = status, AllowVisaApplicationUse = consent,
            AccessExpiresAt = expired ? DateTime.UtcNow.AddMinutes(-5) : null
        };
        db.Users.AddRange(
            new User { Id = requester, ExternalId = "requester", Email = "requester@example.test" },
            new User { Id = owner, ExternalId = "owner", Email = "owner@example.test" });
        db.DocumentTypes.Add(new DocumentType { Id = Guid.NewGuid(), Name = "Test identity" });
        db.Documents.Add(new Document {
            Id = docId, UserId = owner, DocumentTypeId = db.DocumentTypes.Local.Single().Id,
            DocumentName = "Test document", S3ObjectKey = "test/key"
        });
        db.FamilyRequests.Add(request);
        db.FamilyDocumentGrants.Add(new FamilyDocumentGrant {
            FamilyRequestId = request.Id, DocumentId = docId
        });
        await db.SaveChangesAsync();
        var service = new VisaApplicationService(db, new HttpClient(),
            new ConfigurationBuilder().Build());
        Assert.Equal(expected, await service.CanUseDocumentForVisaAsync(requester, docId));
        Assert.True(await service.CanUseDocumentForVisaAsync(owner, docId));
    }
}
