namespace ExpatOne.Application.DTOs;

public class ProfileOptionsDto
{
    public List<ProfileOptionItem> VisaPassTypes { get; set; } = [];
    public List<ProfileOptionItem> EmploymentStatuses { get; set; } = [];
    public List<ProfileOptionItem> FamilyStatuses { get; set; } = [];
    public List<ProfileOptionItem> SupportedLanguages { get; set; } = [];
}

public class ProfileOptionItem
{
    public required string Value { get; set; }
    public required string Label { get; set; }
}

public class ChecklistItemDto
{
    public required string Id { get; set; }
    public required string Category { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public bool IsCompleted { get; set; }
    public string? Action { get; set; }
}
