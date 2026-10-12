namespace ExpatOne.Domain.Entities;

// Confirmation applies only to this document file and its current expiry date.
public class FamilyExpiryConfirmation
{
    public Guid DocumentId { get; set; }
    public required string Fingerprint { get; set; }
    public DateTime ConfirmedAt { get; set; }
    public Document Document { get; set; } = null!;
}

// Stores no document contents. Alerts themselves are always computed from live permissions.
public class FamilyAlertDismissal
{
    public Guid UserId { get; set; }
    public required string AlertId { get; set; }
    public DateTime DismissedAt { get; set; }
    public User User { get; set; } = null!;
}
