using Microsoft.EntityFrameworkCore;
using ResolveAI.AI.Contracts;
using ResolveAI.AI.Persistence;
using ResolveAI.Audit.Contracts;

namespace ResolveAI.AI.Application;

internal sealed class ProposalAuditDelivery(AiDbContext db, IAuditWriter audit, TimeProvider clock) : IProposalAuditDelivery
{
    public async Task<int> DeliverBatchAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        await db.Runs.Where(x => x.Status == "Running" && x.LeaseUntil < now).ExecuteUpdateAsync(s =>
            s.SetProperty(x => x.Status, "Interrupted").SetProperty(x => x.ErrorCode, "OutcomeUnknown")
                .SetProperty(x => x.CompletedAt, now), ct);
        var pending = await db.Runs.AsNoTracking().Where(x => x.Status != "Running" && x.AuditDeliveredAt == null)
            .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(ct);
        foreach (var run in pending)
        {
            await audit.AppendAsync(new(run.AuditEventId, run.TenantId, run.CaseId, run.ActorId, run.ActorType,
                "ai.proposal." + run.Status.ToLowerInvariant(), run.CompletedAt!.Value, run.TraceParent), ct);
            await db.Runs.Where(x => x.RunId == run.RunId && x.AuditDeliveredAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AuditDeliveredAt, clock.GetUtcNow()), ct);
        }
        return pending.Count;
    }
}
