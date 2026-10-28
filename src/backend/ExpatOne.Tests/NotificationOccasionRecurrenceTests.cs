using ExpatOne.Api.Services;
using Xunit;

namespace ExpatOne.Tests;

public class NotificationOccasionRecurrenceTests
{
    [Theory]
    [InlineData(1990, 11, 10, 2026, 10, 28, 2026, 11, 10)]
    [InlineData(2020, 3, 10, 2026, 10, 28, 2027, 3, 10)]
    [InlineData(2024, 2, 29, 2026, 1, 1, 2026, 2, 28)]
    public void NextAnnualOccurrenceHandlesPastDatesAndLeapYears(
        int year, int month, int day, int currentYear, int currentMonth,
        int currentDay, int expectedYear, int expectedMonth, int expectedDay)
    {
        var original = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(currentYear, currentMonth, currentDay, 0, 0, 0, DateTimeKind.Utc);
        var actual = NotificationDeliveryWorker.NextOccurrence(original, true, now);
        Assert.Equal(new DateTime(expectedYear, expectedMonth, expectedDay), actual.Date);
    }

    [Fact]
    public void NonRecurringOccasionRemainsUnchanged()
    {
        var original = new DateTime(2024, 6, 5, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(original, NotificationDeliveryWorker.NextOccurrence(original, false, now));
    }
}
