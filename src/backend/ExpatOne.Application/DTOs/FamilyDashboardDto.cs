using System.ComponentModel.DataAnnotations;
namespace ExpatOne.Application.DTOs;

public record FamilyDashboardDocumentDto(Guid Id, string Name, string DocumentType,
    FamilyUserDto Owner, bool IsOwned, DateTime? ExpiryDate, int? DaysRemaining,
    string ExpiryStatus, bool ExpiryConfirmed, bool RemindersAllowed, string Fingerprint);
public record FamilyAlertDto(string Id, string Kind, string Title, string Message,
    Guid? DocumentId, Guid? RequestId, DateTime? ExpiryDate);
public class ConfirmFamilyExpiryDto
{
    [Required, StringLength(64, MinimumLength = 64)] public string Fingerprint { get; set; } = "";
}
