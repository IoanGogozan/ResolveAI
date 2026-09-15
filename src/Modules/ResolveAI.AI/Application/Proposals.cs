using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResolveAI.AI.Contracts;
using ResolveAI.AI.Persistence;
using ResolveAI.Execution.Contracts;
using ResolveAI.Platform;

namespace ResolveAI.AI.Application;

internal sealed class Proposals(AiDbContext db, IBusinessReads reads, ILlmGateway gateway, AiSettings settings,
    TimeProvider clock) : IProposals
{
    public async Task<ProposalRun> GenerateAsync(ActorContext actor, Guid operationId, ProposalTask task, CancellationToken ct = default)
    {
        Require(actor, "proposals.create");
        if (operationId == Guid.Empty || task.CaseId == Guid.Empty || string.IsNullOrWhiteSpace(task.Subject) ||
            string.IsNullOrWhiteSpace(task.Description) || task.Subject.Length > 200 || task.Description.Length > 10000)
            throw new ProposalRequestException("InvalidProposalRequest");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(task))));
        var runId = Guid.CreateVersion7();
        var auditId = Guid.CreateVersion7();
        var now = clock.GetUtcNow();
        var leaseUntil = now.AddSeconds(settings.TimeoutSeconds + 60);
        using var activity = Telemetry.Source.StartActivity("ai.proposal");
        activity?.SetTag("case.id", task.CaseId);
        activity?.SetTag("tenant.id", actor.TenantId);
        var traceParent = Activity.Current?.Id;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ai.agent_runs ("RunId", "TenantId", "OperationId", "CaseId", "AuditEventId", "ActorId", "ActorType",
                "Fingerprint", "Status", "Provider", "Model", "PromptVersion", "SchemaVersion", "TraceParent", "CreatedAt", "LeaseUntil")
            VALUES ({runId}, {actor.TenantId}, {operationId}, {task.CaseId}, {auditId}, {actor.ActorId}, {actor.ActorType},
                {fingerprint}, 'Running', {gateway.Provider}, {gateway.Model}, {ProposalProtocol.PromptVersion},
                {ProposalProtocol.SchemaVersion}, {traceParent}, {now}, {leaseUntil})
            ON CONFLICT ("TenantId", "OperationId") DO NOTHING
            """, ct);
        if (inserted == 0)
        {
            var existing = await db.Runs.AsNoTracking().SingleAsync(x => x.TenantId == actor.TenantId && x.OperationId == operationId, ct);
            if (existing.Fingerprint != fingerprint) throw new ProposalRequestException("OperationConflict");
            return (await LoadAsync(actor.TenantId, task.CaseId, operationId, ct))!;
        }
        activity?.SetTag("agent_run.id", runId);
        ModelReply? reply = null;
        StructuredProposal? proposal = null;
        string? error = null;
        var status = "Completed";
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        try
        {
            CaseBusinessFacts? facts = null;
            if (task.References is { } references)
                facts = await reads.ResolveAsync(actor, references.CustomerId, references.OrderId, references.PaymentId, timeout.Token);
            var input = new ProposalModelInput(task.Subject, task.Description, facts?.Customer.Id, facts?.Order, facts?.Payment);
            if (ProposalProtocol.ReserveCost(input, settings) > settings.MaxInvocationCostUsd)
                throw new ProposalRequestException("InvocationBudgetExceeded");
            // Store minimal trusted facts for provenance, never unrestricted prompts/customer text.
            var manifest = JsonSerializer.Serialize(new { input.CustomerId, input.Order, input.Payment });
            await db.Runs.Where(x => x.RunId == runId).ExecuteUpdateAsync(s => s.SetProperty(x => x.FactsJson, manifest), timeout.Token);
            reply = await gateway.GenerateAsync(input, timeout.Token);
            if (reply.Outcome != "Completed") throw new ModelGatewayException(reply.Outcome);
            proposal = ProposalProtocol.Validate(reply.Json ?? "", input);
        }
        catch (OperationCanceledException)
        {
            status = "Interrupted";
            error = ct.IsCancellationRequested ? "RequestInterrupted" : "ModelTimeout";
        }
        catch (ModelGatewayException ex) { status = "Failed"; error = ex.Code; }
        catch (BusinessReadException ex) { status = "Failed"; error = "BusinessData" + ex.Failure; }
        catch (ProposalRequestException ex) { status = "Failed"; error = ex.Code; }
        catch (Exception) { status = "Interrupted"; error = "OutcomeUnknown"; }
        stopwatch.Stop();
        var completedAt = clock.GetUtcNow();
        var json = proposal is null ? null : JsonSerializer.Serialize(proposal, ProposalProtocol.JsonOptions);
        int? inputTokens = reply?.InputTokens;
        int? outputTokens = reply?.OutputTokens;
        decimal? cost = inputTokens.HasValue && outputTokens.HasValue
            ? (inputTokens * settings.InputUsdPerMillion + outputTokens * settings.OutputUsdPerMillion) / 1_000_000m : null;
        var model = reply?.Model ?? gateway.Model;
        // Request cancellation must not discard a result already returned by the provider.
        using var saveDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await db.Runs.Where(x => x.RunId == runId && x.Status == "Running" && x.LeaseUntil >= completedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status).SetProperty(x => x.ErrorCode, error)
                .SetProperty(x => x.ProposalJson, json).SetProperty(x => x.InputTokens, inputTokens)
                .SetProperty(x => x.OutputTokens, outputTokens).SetProperty(x => x.EstimatedCostUsd, cost)
                .SetProperty(x => x.Model, model).SetProperty(x => x.LatencyMs, stopwatch.ElapsedMilliseconds)
                .SetProperty(x => x.CompletedAt, completedAt), saveDeadline.Token);
        activity?.SetTag("ai.outcome", status);
        return (await LoadAsync(actor.TenantId, task.CaseId, operationId, saveDeadline.Token))!;
    }

    public Task<ProposalRun?> GetAsync(ActorContext actor, Guid caseId, Guid operationId, CancellationToken ct = default)
    {
        Require(actor, "proposals.read");
        return LoadAsync(actor.TenantId, caseId, operationId, ct);
    }

    private async Task<ProposalRun?> LoadAsync(Guid tenantId, Guid caseId, Guid operationId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var query = db.Runs.Where(x => x.TenantId == tenantId && x.CaseId == caseId && x.OperationId == operationId);
        await query.Where(x => x.Status == "Running" && x.LeaseUntil < now).ExecuteUpdateAsync(s =>
            s.SetProperty(x => x.Status, "Interrupted").SetProperty(x => x.ErrorCode, "OutcomeUnknown")
                .SetProperty(x => x.CompletedAt, now), ct);
        var run = await query.AsNoTracking().SingleOrDefaultAsync(ct);
        return run is null ? null : new(run.RunId, run.OperationId, run.CaseId, run.Status,
            run.ProposalJson is null ? null : JsonSerializer.Deserialize<StructuredProposal>(run.ProposalJson, ProposalProtocol.JsonOptions),
            run.ErrorCode, run.Provider, run.Model, run.PromptVersion, run.SchemaVersion, run.InputTokens, run.OutputTokens,
            run.EstimatedCostUsd, run.LatencyMs, run.CreatedAt, run.CompletedAt);
    }

    private static void Require(ActorContext actor, string permission)
    {
        actor.Require(permission);
        actor.Require("customers.read"); actor.Require("orders.read"); actor.Require("payments.read");
    }
}
