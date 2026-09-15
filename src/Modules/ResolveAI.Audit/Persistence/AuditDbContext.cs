using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ResolveAI.Audit.Persistence;

internal sealed class AuditEvent
{
    public Guid EventId { get; set; }
    public Guid TenantId { get; set; }
    public Guid CaseId { get; set; }
    public required string ActorId { get; set; }
    public required string ActorType { get; set; }
    public required string Action { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? TraceParent { get; set; }
}

internal sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditEvent> Events => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("audit");
        var entity = modelBuilder.Entity<AuditEvent>();
        entity.ToTable("events");
        entity.HasKey(x => new { x.TenantId, x.EventId });
        entity.HasIndex(x => new { x.TenantId, x.CaseId, x.OccurredAt });
        entity.Property(x => x.ActorId).HasMaxLength(100);
        entity.Property(x => x.ActorType).HasMaxLength(30);
        entity.Property(x => x.Action).HasMaxLength(100);
        entity.Property(x => x.TraceParent).HasMaxLength(100);
    }
}

internal sealed class AuditDesignFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AuditDbContext>()
        .UseNpgsql("Host=localhost;Database=design_only", x => x.MigrationsHistoryTable("__EFMigrationsHistory", "audit"))
        .Options);
}
