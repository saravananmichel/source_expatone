using ExpatOne.Domain.Common;
namespace ExpatOne.Domain.Entities;

public class VisaDestination
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool Enabled { get; set; }
}
public class VisaTemplate
{
    public required string Id { get; set; }
    public required string CountryCode { get; set; }
    public required string Purpose { get; set; }
    public required string RulesJson { get; set; }
}
public class VisaApplication : BaseEntity
{
    public Guid UserId { get; set; }
    public required string TemplateId { get; set; }
    public required string StateJson { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public bool Submitted { get; set; }
}
