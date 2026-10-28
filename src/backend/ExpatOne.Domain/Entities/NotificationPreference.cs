namespace ExpatOne.Domain.Entities;

public class NotificationPreference
{
    public Guid UserId { get; set; }
    public bool InAppEnabled { get; set; } = true;
    public bool PushEnabled { get; set; }
    public bool DocumentEnabled { get; set; } = true;
    public bool ConnectionEnabled { get; set; } = true;
    public bool CorporateEnabled { get; set; } = true;
    public bool SharingEnabled { get; set; } = true;
    public bool VisaEnabled { get; set; } = true;
    public bool OccasionEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
