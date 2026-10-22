using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class CorporateMembership : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }

    // Values are validated by the application service.
    // Initial supported roles: Admin, HR, Viewer, Employee.
    public string Role { get; set; } = "Employee";

    // Initial supported states: Active, Suspended.
    public string Status { get; set; } = "Active";
}
