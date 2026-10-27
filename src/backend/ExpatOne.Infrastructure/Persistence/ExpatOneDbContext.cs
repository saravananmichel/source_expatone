using ExpatOne.Domain.Common;
using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Infrastructure.Persistence;

public class ExpatOneDbContext : DbContext
{
    public ExpatOneDbContext(DbContextOptions<ExpatOneDbContext> options) : base(options)
    {
    }

    public DbSet<CorporateCompanyProfile> CorporateCompanyProfiles => Set<CorporateCompanyProfile>();
    public DbSet<CorporateBenefit> CorporateBenefits => Set<CorporateBenefit>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<CorporateCompany> CorporateCompanies => Set<CorporateCompany>();
    public DbSet<CorporateIssuedPermit> CorporateIssuedPermits => Set<CorporateIssuedPermit>();
    public DbSet<CorporatePermitAlert> CorporatePermitAlerts => Set<CorporatePermitAlert>();
    public DbSet<CorporateMembership> CorporateMemberships => Set<CorporateMembership>();
    public DbSet<CorporateCase> CorporateCases => Set<CorporateCase>();
    public DbSet<CorporateMemberAudit> CorporateMemberAudits => Set<CorporateMemberAudit>();
    public DbSet<CorporateOfferLetter> CorporateOfferLetters => Set<CorporateOfferLetter>();

    public DbSet<VisaDestination> VisaDestinations => Set<VisaDestination>();
    public DbSet<VisaTemplate> VisaTemplates => Set<VisaTemplate>();
    public DbSet<VisaApplication> VisaApplications => Set<VisaApplication>();

    public DbSet<DocumentAnalysisRun> DocumentAnalysisRuns => Set<DocumentAnalysisRun>();

    public DbSet<FamilyExpiryConfirmation> FamilyExpiryConfirmations => Set<FamilyExpiryConfirmation>();
    public DbSet<FamilyAlertDismissal> FamilyAlertDismissals => Set<FamilyAlertDismissal>();
    public DbSet<FamilyRequestStatus> FamilyRequestStatuses => Set<FamilyRequestStatus>();
    public DbSet<FamilyRequest> FamilyRequests => Set<FamilyRequest>();
    public DbSet<FamilyDocumentGrant> FamilyDocumentGrants => Set<FamilyDocumentGrant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<GovernmentKnowledge> GovernmentKnowledge => Set<GovernmentKnowledge>();
    public DbSet<GovernmentSource> GovernmentSources => Set<GovernmentSource>();
    public DbSet<AIConversation> AIConversations => Set<AIConversation>();
    public DbSet<AIConversationMessage> AIConversationMessages => Set<AIConversationMessage>();
    public DbSet<EmergencyResource> EmergencyResources => Set<EmergencyResource>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentShare> DocumentShares => Set<DocumentShare>();
    public DbSet<DocumentAuditLog> DocumentAuditLogs => Set<DocumentAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (Database.IsNpgsql())
        {
            modelBuilder.HasPostgresExtension("vector");
        }

        modelBuilder.Entity<CorporateCompanyProfile>(e =>
        {
            e.ToTable("corporate_company_profiles");
            e.HasKey(x => x.CompanyId);
            e.Property(x => x.OfferTemplateJson).HasColumnType("jsonb");
            e.HasOne<CorporateCompany>().WithOne().HasForeignKey<CorporateCompanyProfile>(x => x.CompanyId);
        });
        modelBuilder.Entity<CorporateBenefit>(e =>
        {
            e.ToTable("corporate_benefits");
            e.HasKey(x => x.Id);
            e.HasOne<CorporateCompany>().WithMany().HasForeignKey(x => x.CompanyId);
        });
        modelBuilder.Entity<Currency>(e =>
        {
            e.ToTable("currencies");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasColumnType("char(3)");
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExpatOneDbContext).Assembly);

        if (!Database.IsNpgsql())
        {
            modelBuilder.Entity<GovernmentKnowledge>().Ignore(e => e.Embedding);
        }
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
