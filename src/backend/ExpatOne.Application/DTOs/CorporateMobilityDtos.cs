using System.ComponentModel.DataAnnotations;

namespace ExpatOne.Application.DTOs;

public sealed class CreateCorporateCompanyDto
{
    [Required, StringLength(180, MinimumLength = 2)]
    public string Name { get; set; } = "";
}

public sealed record CorporateCompanyDto(
    Guid Id, string Name, string Role, DateTime CreatedAt);

public sealed record CorporateMemberDto(
    Guid MembershipId, Guid UserId, string Email, string? DisplayName,
    string Role, string Status, DateTime JoinedAt);
