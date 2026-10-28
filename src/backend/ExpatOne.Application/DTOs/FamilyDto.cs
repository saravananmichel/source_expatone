using System.ComponentModel.DataAnnotations;
namespace ExpatOne.Application.DTOs;
public record FamilyUserDto(Guid Id, string Email, string? DisplayName);
public record FamilyRequestDto(Guid Id, FamilyUserDto Requester, FamilyUserDto Recipient,
    int StatusId, string Status, bool IsIncoming, Guid Revision, DateTime CreatedAt,
    DateTime? RespondedAt, List<Guid> DocumentIds, bool AllowExpiryReminders = false, DateTime? AccessExpiresAt = null, bool IsExpired = false, string Relationship = "Other", bool AllowVisaApplicationUse = false);
public class SendFamilyRequestDto
{
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = "";
    [Required, MaxLength(40)] public string Relationship { get; set; } = "Other";
}
public class FamilyDecisionDto
{
    public Guid Revision { get; set; }
    public DateTimeOffset? AccessExpiresAt { get; set; }
    public bool UpdateAccessExpiry { get; set; }
    public bool? AllowExpiryReminders { get; set; }
    public bool? AllowVisaApplicationUse { get; set; }
    public bool Accept { get; set; }
    [Required, MaxLength(10000)] public List<Guid> DocumentIds { get; set; } = [];
}
public class FamilyGrantsDto
{
    public Guid Revision { get; set; }
    public DateTimeOffset? AccessExpiresAt { get; set; }
    public bool UpdateAccessExpiry { get; set; }
    public bool? AllowExpiryReminders { get; set; }
    [Required, MaxLength(10000)] public List<Guid> DocumentIds { get; set; } = [];
}
public class FamilyRevisionDto { public Guid Revision { get; set; } }
