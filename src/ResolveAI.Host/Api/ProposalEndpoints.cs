using ResolveAI.AI.Contracts;
using ResolveAI.CaseManagement.Contracts;
using ResolveAI.Host.Security;

namespace ResolveAI.Host.Api;

internal static class ProposalEndpoints
{
    internal sealed record GenerateRequest(Guid OperationId);
    public static void MapProposalEndpoints(this WebApplication app)
    {
        app.MapPost("/api/cases/{id:guid}/proposals", async (Guid id, GenerateRequest request, HttpContext http,
            ICaseService cases, IProposals proposals, CancellationToken ct) =>
        {
            var actor = http.User.ToActor();
            var item = await cases.GetAsync(actor, id, ct);
            if (item is null) return Results.NotFound();
            var input = new ProposalTask(item.Id, item.Subject, item.Description, item.References is not { } refs
                ? null : new ProposalReferences(refs.CustomerId, refs.OrderId, refs.PaymentId));
            var run = await proposals.GenerateAsync(actor, request.OperationId, input, ct);
            var location = $"/api/cases/{id}/proposals/{request.OperationId}";
            return run.Status == "Running" ? Results.Accepted(location, run) : Results.Ok(run);
        }).RequireAuthorization("cases.read", "proposals.create", "customers.read", "orders.read", "payments.read");

        app.MapGet("/api/cases/{id:guid}/proposals", async (Guid id, HttpContext http,
            ICaseService cases, IProposals proposals, CancellationToken ct) =>
        {
            var actor = http.User.ToActor();
            if (await cases.GetAsync(actor, id, ct) is null) return Results.NotFound();
            return Results.Ok(await proposals.ListAsync(actor, id, ct));
        }).RequireAuthorization("cases.read", "proposals.read", "customers.read", "orders.read", "payments.read");

        app.MapGet("/api/cases/{id:guid}/proposals/{operationId:guid}", async (Guid id, Guid operationId, HttpContext http,
            ICaseService cases, IProposals proposals, CancellationToken ct) =>
        {
            var actor = http.User.ToActor();
            if (await cases.GetAsync(actor, id, ct) is null) return Results.NotFound();
            var result = await proposals.GetAsync(actor, id, operationId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization("cases.read", "proposals.read", "customers.read", "orders.read", "payments.read");
    }
}
