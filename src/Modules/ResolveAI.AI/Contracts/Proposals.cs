using ResolveAI.Execution.Contracts;
using ResolveAI.Platform;

namespace ResolveAI.AI.Contracts;

public sealed record ProposalReferences(string CustomerId, string OrderId, string PaymentId);
public sealed record ProposalTask(Guid CaseId, string Subject, string Description, ProposalReferences? References);
public sealed record ProposalModelInput(string Subject, string Description, string? CustomerId, OrderFacts? Order, PaymentFacts? Payment);
public sealed record StructuredProposal(string Action, string? PaymentId, decimal? Amount, string? Currency,
    string ReasonCode, decimal Confidence, string[] Evidence);
public sealed record ModelReply(string? Json, string Model, string Outcome, int? InputTokens, int? OutputTokens);

public interface ILlmGateway
{
    string Provider { get; }
    string Model { get; }
    Task<ModelReply> GenerateAsync(ProposalModelInput input, CancellationToken ct);
}

public sealed class ModelGatewayException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed record ProposalRun(Guid RunId, Guid OperationId, Guid CaseId, string Status,
    StructuredProposal? Proposal, string? ErrorCode, string Provider, string Model, string PromptVersion,
    string SchemaVersion, int? InputTokens, int? OutputTokens, decimal? EstimatedCostUsd, long? LatencyMs,
    DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);

public interface IProposals
{
    Task<ProposalRun> GenerateAsync(ActorContext actor, Guid operationId, ProposalTask task, CancellationToken ct = default);
    Task<ProposalRun?> GetAsync(ActorContext actor, Guid caseId, Guid operationId, CancellationToken ct = default);
}

public interface IProposalAuditDelivery
{
    Task<int> DeliverBatchAsync(CancellationToken ct = default);
}

public sealed class ProposalRequestException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed record AiSettings
{
    public string? ApiKey { get; init; }
    public string Model { get; init; } = "gpt-4.1-mini-2025-04-14";
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxOutputTokens { get; init; } = 512;
    public int MaxInputBytes { get; init; } = 16384;
    public decimal InputUsdPerMillion { get; init; } = 0.40m;
    public decimal OutputUsdPerMillion { get; init; } = 1.60m;
    public decimal MaxInvocationCostUsd { get; init; } = 0.02m;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Model) || TimeoutSeconds is < 1 or > 120 ||
            MaxOutputTokens is < 128 or > 2048 || MaxInputBytes is < 1024 or > 32768 ||
            InputUsdPerMillion <= 0 || OutputUsdPerMillion <= 0 || MaxInvocationCostUsd <= 0)
            throw new InvalidOperationException("Invalid AI limits or pricing configuration.");
    }
}
