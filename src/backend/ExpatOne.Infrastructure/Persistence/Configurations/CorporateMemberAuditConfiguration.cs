using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ExpatOne.Infrastructure.Persistence.Configurations;
public sealed class CorporateMemberAuditConfiguration : IEntityTypeConfiguration<CorporateMemberAudit> {
 public void Configure(EntityTypeBuilder<CorporateMemberAudit> b) {
  b.ToTable("corporate_member_audit"); b.HasKey(x=>x.Id);
  b.Property(x=>x.Id).ValueGeneratedNever();
  b.Property(x=>x.Action).HasMaxLength(30).IsRequired();
  b.Property(x=>x.OldValue).HasMaxLength(50);
  b.Property(x=>x.NewValue).HasMaxLength(50);
  b.HasIndex(x=>new{x.CompanyId,x.MembershipId,x.CreatedAt});
 }
}
