using ExpatOne.Domain.Common;
using ExpatOne.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpatOne.Infrastructure.Persistence;

public class ExpatOneDbContext : DbContext
{
    public ExpatOneDbContext(DbContextOptions<ExpatOneDbContext> options) : base(options)
    {
    }

    public DbSet<CorporateCompany> CorporateCompanies => Set<CorporateCompany>();
    public DbSet<CorporateMembership> CorporateMemberships => Set<CorporateMembership>();
    public DbSet<CorporateCase> CorporateCases => Set<CorporateCase>();
    public DbSet<CorporateMemberAudit> CorporateMemberAudits => Set<CorporateMemberAudit>();

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
