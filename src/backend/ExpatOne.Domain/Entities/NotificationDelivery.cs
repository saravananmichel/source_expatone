namespace ExpatOne.Domain.Entities;

public class NotificationDelivery
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ReminderKey { get; set; } = string.Empty;
    public string Channel { get; set; } = "InApp";
    public DateOnly OccurrenceDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
}
