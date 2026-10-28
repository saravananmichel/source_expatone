using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExpatOne.Tests;

public class FamilyOccasionTests
{
    [Fact]
    public void NewOccasion_DefaultsToAnnualReminder()
    {
        var occasion = new FamilyOccasion { Title = "Birthday" };
        Assert.True(occasion.RepeatsAnnually);
        Assert.True(occasion.RemindersEnabled);
        Assert.Equal(7, occasion.DaysBefore);
        Assert.Equal("Custom", occasion.Kind);
    }

    [Fact]
    public void Occasion_IsMappedWithOwnerAndOptionalConnection()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase("occasion-model-" + Guid.NewGuid()).Options;
        using var db = new ExpatOneDbContext(options);
        var entity = db.Model.FindEntityType(typeof(FamilyOccasion));
        Assert.NotNull(entity);
        Assert.NotNull(entity!.FindProperty(nameof(FamilyOccasion.UserId)));
        Assert.NotNull(entity.FindProperty(nameof(FamilyOccasion.ConnectionId)));
        Assert.Contains(entity.GetForeignKeys(), fk =>
            fk.Properties.Any(p => p.Name == nameof(FamilyOccasion.UserId)));
    }

    [Fact]
    public async Task Occasions_CanBeScopedToOwner()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase("occasion-owner-" + Guid.NewGuid()).Options;
        await using var db = new ExpatOneDbContext(options);
        var owner = Guid.NewGuid();
        var another = Guid.NewGuid();
        db.FamilyOccasions.AddRange(
            new FamilyOccasion { Id = Guid.NewGuid(), UserId = owner,
                Title = "My birthday", OccasionDate = DateTime.UtcNow },
            new FamilyOccasion { Id = Guid.NewGuid(), UserId = another,
                Title = "Another birthday", OccasionDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var visible = await db.FamilyOccasions.Where(x => x.UserId == owner).ToListAsync();
        Assert.Single(visible);
        Assert.Equal("My birthday", visible[0].Title);
    }
}
