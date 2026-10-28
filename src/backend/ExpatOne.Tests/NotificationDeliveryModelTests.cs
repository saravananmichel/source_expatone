using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ExpatOne.Tests;

public class NotificationDeliveryModelTests
{
    [Fact]
    public void DeliveryLedger_HasUniqueOccurrenceIndex()
    {
        var options = new DbContextOptionsBuilder<ExpatOneDbContext>()
            .UseInMemoryDatabase("notification-model-" + Guid.NewGuid()).Options;
        using var db = new ExpatOneDbContext(options);
        var entity = db.Model.FindEntityType(typeof(NotificationDelivery));
        Assert.NotNull(entity);
        Assert.Contains(entity!.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(NotificationDelivery.UserId),
                nameof(NotificationDelivery.ReminderKey),
                nameof(NotificationDelivery.Channel),
                nameof(NotificationDelivery.OccurrenceDate)
            }));
    }

    [Fact]
    public void Preferences_DefaultToInAppOnly()
    {
        var preference = new NotificationPreference();
        Assert.True(preference.InAppEnabled);
        Assert.False(preference.PushEnabled);
        Assert.True(preference.OccasionEnabled);
    }
}
