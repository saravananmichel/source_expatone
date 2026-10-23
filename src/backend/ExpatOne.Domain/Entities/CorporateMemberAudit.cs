using ExpatOne.Domain.Common;
namespace ExpatOne.Domain.Entities;
public class CorporateMemberAudit : BaseEntity {
 public Guid CompanyId {get;set;}
 public Guid MembershipId {get;set;}
 public Guid ActorUserId {get;set;}
 public string Action {get;set;} = "";
 public string? OldValue {get;set;}
 public string? NewValue {get;set;}
}
