using ResolveAI.Platform;

namespace ResolveAI.CaseManagement.Contracts;

public sealed record CreateCaseRequest(string Subject, string Description);
public sealed record CaseSummary(Guid Id, string Subject, string Description, string Status, DateTimeOffset CreatedAt);

public interface ICaseService
{
    Task<CaseSummary> CreateAsync(ActorContext actor, CreateCaseRequest request, CancellationToken ct = default);
    Task<CaseSummary?> GetAsync(ActorContext actor, Guid id, CancellationToken ct = default);
}

// Internal processing capability exposed to the host worker, not to HTTP callers.
public interface ICaseAuditDelivery
{
    Task<int> DeliverBatchAsync(CancellationToken ct = default);
}

public sealed class CaseValidationException(string message) : Exception(message);
