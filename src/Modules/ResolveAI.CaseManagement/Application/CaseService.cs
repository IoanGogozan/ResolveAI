using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResolveAI.CaseManagement.Contracts;
using ResolveAI.CaseManagement.Persistence;
using ResolveAI.Platform;

namespace ResolveAI.CaseManagement.Application;

internal sealed class CaseService(CaseDbContext db, TimeProvider clock, ILogger<CaseService> logger) : ICaseService
{
    public async Task<CaseSummary> CreateAsync(ActorContext actor, CreateCaseRequest request, CancellationToken ct = default)
    {
        actor.Require("cases.create");
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 200 ||
            string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 10000)
            throw new CaseValidationException("Subject must contain 1–200 characters and description 1–10000 characters.");

        using var activity = Telemetry.Source.StartActivity("case.create");
        var item = new SupportCase
        {
            Id = Guid.CreateVersion7(), TenantId = actor.TenantId,
            Subject = request.Subject.Trim(), Description = request.Description.Trim(),
            Status = "Created", CreatedAt = clock.GetUtcNow()
        };
        activity?.SetTag("tenant.id", actor.TenantId);
        activity?.SetTag("case.id", item.Id);
        db.Cases.Add(item);
        db.Outbox.Add(new CaseCreatedOutbox
        {
            EventId = Guid.CreateVersion7(), TenantId = actor.TenantId, CaseId = item.Id,
            ActorId = actor.ActorId, ActorType = actor.ActorType, OccurredAt = item.CreatedAt,
            TraceParent = Activity.Current?.Id
        });
        // A single SaveChanges transaction commits the case and audit intent together.
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Case {CaseId} created for tenant {TenantId}", item.Id, actor.TenantId);
        return ToSummary(item);
    }

    public async Task<CaseSummary?> GetAsync(ActorContext actor, Guid id, CancellationToken ct = default)
    {
        actor.Require("cases.read");
        using var activity = Telemetry.Source.StartActivity("case.get");
        activity?.SetTag("tenant.id", actor.TenantId);
        activity?.SetTag("case.id", id);
        var item = await db.Cases.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.Id == id, ct);
        return item is null ? null : ToSummary(item);
    }

    private static CaseSummary ToSummary(SupportCase x) => new(x.Id, x.Subject, x.Description, x.Status, x.CreatedAt);
}
