using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class CorporateMembership : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }

    // Values are validated by the application service.
    // Initial supported roles: Admin, HR, Viewer, Employee.
    public string Role { get; set; } = "Employee";

    // Membership access states: Active, Suspended.
    // Invitation lifecycle states: Pending, Declined.
    // Invitation endpoints transition Pending to Active or Declined.
    public string Status { get; set; } = "Active";

    // Company-specific employment metadata; never inferred from nationality.
    public string WorkforceClassification { get; set; } = "Unknown";
    public string? Nationality { get; set; }
    public string? EmploymentCountry { get; set; }
    public DateTime? EmploymentStartDate { get; set; }
}
