using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ResolveAI.CaseManagement.Persistence;

internal sealed class SupportCase
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Subject { get; set; }
    public required string Description { get; set; }
    public required string Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class CaseCreatedOutbox
{
    public Guid EventId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CaseId { get; set; }
    public required string ActorId { get; set; }
    public required string ActorType { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? TraceParent { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
}

internal sealed class CaseDbContext(DbContextOptions<CaseDbContext> options) : DbContext(options)
{
    public DbSet<SupportCase> Cases => Set<SupportCase>();
    public DbSet<CaseCreatedOutbox> Outbox => Set<CaseCreatedOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("case_management");
        var entity = modelBuilder.Entity<SupportCase>();
        entity.ToTable("cases");
        entity.HasKey(x => new { x.TenantId, x.Id });
        entity.Property(x => x.Subject).HasMaxLength(200);
        entity.Property(x => x.Description).HasMaxLength(10000);
        entity.Property(x => x.Status).HasMaxLength(30);
        var outbox = modelBuilder.Entity<CaseCreatedOutbox>();
        outbox.ToTable("case_created_outbox");
        outbox.HasKey(x => x.EventId);
        outbox.HasIndex(x => x.OccurredAt).HasFilter("\"DeliveredAt\" IS NULL");
        outbox.Property(x => x.ActorId).HasMaxLength(100);
        outbox.Property(x => x.ActorType).HasMaxLength(30);
        outbox.Property(x => x.TraceParent).HasMaxLength(100);
        outbox.HasOne<SupportCase>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.CaseId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CaseDesignFactory : IDesignTimeDbContextFactory<CaseDbContext>
{
    public CaseDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<CaseDbContext>()
        .UseNpgsql("Host=localhost;Database=design_only", x => x.MigrationsHistoryTable("__EFMigrationsHistory", "case_management"))
        .Options);
}
