using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ResolveAI.AI.Persistence;

internal sealed class AgentRun
{
    public Guid RunId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OperationId { get; set; }
    public Guid CaseId { get; set; }
    public Guid AuditEventId { get; set; }
    public required string ActorId { get; set; }
    public required string ActorType { get; set; }
    public required string Fingerprint { get; set; }
    public required string Status { get; set; }
    public required string Provider { get; set; }
    public required string Model { get; set; }
    public required string PromptVersion { get; set; }
    public required string SchemaVersion { get; set; }
    public string? ProposalJson { get; set; }
    public string? FactsJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? TraceParent { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public decimal? EstimatedCostUsd { get; set; }
    public long? LatencyMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LeaseUntil { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? AuditDeliveredAt { get; set; }
}

internal sealed class AiDbContext(DbContextOptions<AiDbContext> options) : DbContext(options)
{
    public DbSet<AgentRun> Runs => Set<AgentRun>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("ai");
        var run = model.Entity<AgentRun>();
        run.ToTable("agent_runs");
        run.HasKey(x => x.RunId);
        run.HasIndex(x => new { x.TenantId, x.OperationId }).IsUnique();
        run.HasIndex(x => new { x.TenantId, x.CaseId });
        run.Property(x => x.Fingerprint).HasMaxLength(64);
        foreach (var property in new[] { "ActorId", "ActorType", "Status", "Provider", "Model", "PromptVersion", "SchemaVersion", "ErrorCode", "TraceParent" })
            run.Property<string>(property).HasMaxLength(150);
        run.Property(x => x.ProposalJson).HasColumnType("jsonb");
        run.Property(x => x.FactsJson).HasColumnType("jsonb");
        run.Property(x => x.EstimatedCostUsd).HasPrecision(18, 10);
    }
}
internal sealed class AiDesignFactory : IDesignTimeDbContextFactory<AiDbContext>
{
    public AiDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AiDbContext>()
        .UseNpgsql("Host=localhost;Database=design_only", pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "ai")).Options);
}
