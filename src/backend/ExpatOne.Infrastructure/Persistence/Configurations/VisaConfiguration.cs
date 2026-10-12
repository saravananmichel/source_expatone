using ExpatOne.Domain.Entities;
using ExpatOne.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ExpatOne.Infrastructure.Persistence.Configurations;
public class VisaDestinationConfiguration : IEntityTypeConfiguration<VisaDestination>
{
 public void Configure(EntityTypeBuilder<VisaDestination> b) {
  b.ToTable("visa_destinations"); b.HasKey(x=>x.Code); b.Property(x=>x.Code).HasMaxLength(2); b.Property(x=>x.Name).HasMaxLength(80);
  b.HasData(new[]{("SG","Singapore"),("MY","Malaysia"),("IN","India"),("ID","Indonesia"),("CN","China"),("AE","United Arab Emirates"),("SA","Saudi Arabia"),("QA","Qatar"),("KW","Kuwait"),("BH","Bahrain"),("OM","Oman")}.Select(x=>new VisaDestination{Code=x.Item1,Name=x.Item2,Enabled=x.Item1=="SG"}));
 }
}
public class VisaTemplateConfiguration : IEntityTypeConfiguration<VisaTemplate>
{
 public void Configure(EntityTypeBuilder<VisaTemplate> b) {
  b.ToTable("visa_templates"); b.HasKey(x=>x.Id); b.Property(x=>x.Id).HasMaxLength(80); b.Property(x=>x.CountryCode).HasMaxLength(2); b.Property(x=>x.Purpose).HasMaxLength(40); b.Property(x=>x.RulesJson).HasColumnType("text");
  b.HasOne<VisaDestination>().WithMany().HasForeignKey(x=>x.CountryCode).OnDelete(DeleteBehavior.Restrict);
  b.HasData(new VisaTemplate{Id="SG-VISIT-2026-10-12",CountryCode="SG",Purpose="Tourism",RulesJson=VisaRules.SeedJson()});
 }
}
public class VisaApplicationConfiguration : IEntityTypeConfiguration<VisaApplication>
{
 public void Configure(EntityTypeBuilder<VisaApplication> b) {
  b.ToTable("visa_applications"); b.HasKey(x=>x.Id); b.Property(x=>x.TemplateId).HasMaxLength(80); b.Property(x=>x.StateJson).HasColumnType("text"); b.Property(x=>x.Revision).IsConcurrencyToken(); b.HasIndex(x=>new{x.UserId,x.UpdatedAt});
  b.HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Cascade);
  b.HasOne<VisaTemplate>().WithMany().HasForeignKey(x=>x.TemplateId).OnDelete(DeleteBehavior.Restrict);
 }
}
