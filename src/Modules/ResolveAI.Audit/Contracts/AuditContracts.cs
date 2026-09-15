using ResolveAI.Platform;

namespace ResolveAI.Audit.Contracts;

public sealed record AuditFact(Guid EventId, Guid TenantId, Guid CaseId, string ActorId,
    string ActorType, string Action, DateTimeOffset OccurredAt, string? TraceParent);

// Trusted module-to-module append port. Never exposed as an HTTP mutation endpoint.
public interface IAuditWriter
{
    Task AppendAsync(AuditFact fact, CancellationToken cancellationToken = default);
}

public interface IAuditReader
{
    Task<IReadOnlyList<AuditFact>> GetForCaseAsync(ActorContext actor, Guid caseId,
        CancellationToken cancellationToken = default);
}
