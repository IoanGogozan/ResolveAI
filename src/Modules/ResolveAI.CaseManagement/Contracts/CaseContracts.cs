using ResolveAI.Platform;

namespace ResolveAI.CaseManagement.Contracts;

public sealed record CaseBusinessReferences(string CustomerId, string OrderId, string PaymentId);
public sealed record CreateCaseRequest(string Subject, string Description, CaseBusinessReferences? References = null);
public sealed record CaseSummary(Guid Id, string Subject, string Description, string Status, DateTimeOffset CreatedAt,
    CaseBusinessReferences? References = null);

// NextCursor is null on the last page; callers treat it as an opaque continuation token.
public sealed record CasePage(IReadOnlyList<CaseSummary> Items, string? NextCursor);

public interface ICaseService
{
    Task<CaseSummary> CreateAsync(ActorContext actor, CreateCaseRequest request, CancellationToken ct = default);
    Task<CaseSummary?> GetAsync(ActorContext actor, Guid id, CancellationToken ct = default);
    Task<CasePage> ListAsync(ActorContext actor, int limit, string? cursor, CancellationToken ct = default);
}

// Internal processing capability exposed to the host worker, not to HTTP callers.
public interface ICaseAuditDelivery
{
    Task<int> DeliverBatchAsync(CancellationToken ct = default);
}

public sealed class CaseValidationException(string message) : Exception(message);
