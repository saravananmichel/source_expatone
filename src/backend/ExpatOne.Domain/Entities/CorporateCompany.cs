using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class CorporateCompany : BaseEntity
{
    public required string Name { get; set; }
    public Guid CreatedByUserId { get; set; }
}
