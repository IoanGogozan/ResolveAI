using Microsoft.EntityFrameworkCore;
using ResolveAI.Audit.Contracts;
using ResolveAI.Audit.Persistence;
using ResolveAI.Platform;

namespace ResolveAI.Audit.Application;

internal sealed class AuditLog(AuditDbContext db) : IAuditWriter, IAuditReader
{
    public async Task AppendAsync(AuditFact fact, CancellationToken cancellationToken = default)
    {
        // The unique source identity makes concurrent redelivery safe, even after a crash
        // between this commit and the producer's delivery acknowledgement.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO audit.events ("TenantId", "EventId", "CaseId", "ActorId", "ActorType", "Action", "OccurredAt", "TraceParent")
            VALUES ({fact.TenantId}, {fact.EventId}, {fact.CaseId}, {fact.ActorId}, {fact.ActorType}, {fact.Action}, {fact.OccurredAt}, {fact.TraceParent})
            ON CONFLICT ("TenantId", "EventId") DO NOTHING
            """, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditFact>> GetForCaseAsync(ActorContext actor, Guid caseId,
        CancellationToken cancellationToken = default)
    {
        actor.Require("audit.read");
        return await db.Events.AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId && x.CaseId == caseId)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.EventId)
            .Select(x => new AuditFact(x.EventId, x.TenantId, x.CaseId, x.ActorId,
                x.ActorType, x.Action, x.OccurredAt, x.TraceParent))
            .ToListAsync(cancellationToken);
    }
}
