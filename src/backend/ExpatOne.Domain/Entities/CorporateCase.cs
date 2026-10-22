using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class CorporateCase : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid EmployeeUserId { get; set; }
    public Guid? VisaApplicationId { get; set; }

    public required string Title { get; set; }

    // Initial supported states: Open, InProgress, Pending, Completed, Cancelled.
    public string Status { get; set; } = "Open";

    public DateTime? DueAt { get; set; }
    public string? Notes { get; set; }
}
