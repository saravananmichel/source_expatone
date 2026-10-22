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

public sealed class AddCorporateMemberDto
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";

    [Required, StringLength(20)]
    public string Role { get; set; } = "Employee";
}

public sealed class UpdateCorporateMemberRoleDto
{
    [Required, StringLength(20)]
    public string Role { get; set; } = "";
}

public sealed class UpdateCorporateMemberStatusDto
{
    [Required, StringLength(20)]
    public string Status { get; set; } = "";
}
