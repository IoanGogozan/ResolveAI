using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResolveAI.Audit.Contracts;
using ResolveAI.CaseManagement.Contracts;
using ResolveAI.CaseManagement.Persistence;
using ResolveAI.Platform;

namespace ResolveAI.CaseManagement.Application;

internal sealed class CaseAuditDelivery(CaseDbContext db, IAuditWriter audit, TimeProvider clock,
    ILogger<CaseAuditDelivery> logger) : ICaseAuditDelivery
{
    public async Task<int> DeliverBatchAsync(CancellationToken ct = default)
    {
        var pending = await db.Outbox.AsNoTracking().Where(x => x.DeliveredAt == null)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.EventId).Take(100).ToListAsync(ct);
        foreach (var message in pending)
        {
            ActivityContext.TryParse(message.TraceParent, null, out var parent);
            using var activity = Telemetry.Source.StartActivity("audit.deliver", ActivityKind.Consumer, parent);
            activity?.SetTag("tenant.id", message.TenantId);
            activity?.SetTag("case.id", message.CaseId);
            await audit.AppendAsync(new AuditFact(message.EventId, message.TenantId, message.CaseId,
                message.ActorId, message.ActorType, "case.created", message.OccurredAt, message.TraceParent), ct);
            // Ack only AFTER the audit commit. Concurrent delivery is safe at the sink.
            await db.Outbox.Where(x => x.EventId == message.EventId && x.DeliveredAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.DeliveredAt, clock.GetUtcNow()), ct);
            logger.LogInformation("Audit {EventId} delivered for case {CaseId} tenant {TenantId}",
                message.EventId, message.CaseId, message.TenantId);
        }
        return pending.Count;
    }
}
