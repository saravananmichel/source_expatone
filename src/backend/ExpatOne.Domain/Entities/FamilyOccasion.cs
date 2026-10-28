using ExpatOne.Domain.Common;

namespace ExpatOne.Domain.Entities;

public class FamilyOccasion : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? ConnectionId { get; set; }
    public required string Title { get; set; }
    public string Kind { get; set; } = "Custom";
    public DateTime OccasionDate { get; set; }
    public bool RepeatsAnnually { get; set; } = true;
    public bool RemindersEnabled { get; set; } = true;
    public int DaysBefore { get; set; } = 7;
}
