using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class DocumentType : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool HasExpiry { get; set; }
    public bool IsSystem { get; set; } = true;
}
