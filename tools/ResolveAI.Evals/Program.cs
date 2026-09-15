using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ResolveAI.AI.Contracts;
using ResolveAI.AI.DependencyInjection;

var options = new Dictionary<string, string>(StringComparer.Ordinal);
string[] allowed = ["--mode", "--output", "--baseline", "--max-calls", "--budget-usd"];
for (var i = 0; i < args.Length; i += 2)
{
    if (i + 1 >= args.Length || !allowed.Contains(args[i]) || !options.TryAdd(args[i], args[i + 1]))
    {
        Console.Error.WriteLine("Usage: --mode fixture|live --output FILE [--baseline FILE] [--max-calls 10] [--budget-usd 0.20]");
        return 2;
    }
}
var mode = options.GetValueOrDefault("--mode", "fixture");
if (mode is not ("fixture" or "live") ||
    !int.TryParse(options.GetValueOrDefault("--max-calls", "10"), out var maxCalls) || maxCalls is < 1 or > 10 ||
    !decimal.TryParse(options.GetValueOrDefault("--budget-usd", "0.20"), NumberStyles.Number, CultureInfo.InvariantCulture, out var budget) || budget <= 0)
{
    Console.Error.WriteLine("Invalid mode, call limit or budget."); return 2;
}
var settings = new AiSettings { ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") };
if (mode == "live" && string.IsNullOrWhiteSpace(settings.ApiKey))
{
    Console.Error.WriteLine("Live evaluation requires OPENAI_API_KEY. Configure it locally; do not commit or print the key."); return 2;
}
var data = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", "initial-cases.json"));
var dataset = JsonSerializer.Deserialize<EvalCase[]>(data, ProposalProtocol.JsonOptions)!;
var hash = Convert.ToHexString(SHA256.HashData(data));
EvalReport? baseline = null;
if (options.TryGetValue("--baseline", out var baselinePath))
{
    baseline = JsonSerializer.Deserialize<EvalReport>(await File.ReadAllTextAsync(baselinePath), ProposalProtocol.JsonOptions);
    if (baseline is null || baseline.Mode != mode || baseline.DatasetHash != hash || baseline.SchemaVersion != ProposalProtocol.SchemaVersion)
    {
        Console.Error.WriteLine("Baseline must use the same mode, dataset and output schema. Fixture reports cannot measure live model regressions."); return 2;
    }
}
var rows = new List<EvalResult>();
var reserved = 0m;
var calls = 0;
var stopReason = "Completed";
ILlmGateway? live = mode == "live" ? AiModule.CreateOpenAiGateway(settings) : null;
foreach (var test in dataset)
{
    var hasFacts = test.Facts != "none";
    var input = new ProposalModelInput(test.Subject, test.Description, hasFacts ? "customer-monitor" : null,
        hasFacts ? new("order-monitor", "customer-monitor", "Monitor", 649m, "EUR", "Delivered") : null,
        hasFacts ? new("payment-monitor", "order-monitor", 649m, test.Facts == "refunded" ? 649m : 0m, "EUR", "Captured") : null);
    var reserve = ProposalProtocol.ReserveCost(input, settings);
    if (mode == "live" && (calls >= maxCalls || reserved + reserve > budget || reserve > settings.MaxInvocationCostUsd))
    {
        stopReason = "BudgetOrCallLimit"; break;
    }
    ModelReply? reply = null;
    StructuredProposal? proposal = null;
    string? error = null;
    var stopwatch = Stopwatch.StartNew();
    try
    {
        if (mode == "live")
        {
            // Reserve pessimistically BEFORE every attempt, including failures with unknown billing.
            reserved += reserve; calls++;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            reply = await live!.GenerateAsync(input, timeout.Token);
        }
        else reply = new(test.Fixture.GetRawText(), "fixture-v1", "Completed", null, null);
        if (reply.Outcome != "Completed") throw new ModelGatewayException(reply.Outcome);
        proposal = ProposalProtocol.Validate(reply.Json ?? "", input);
    }
    catch (ModelGatewayException ex) { error = ex.Code; }
    catch (OperationCanceledException) { error = "ModelTimeout"; }
    catch (Exception) { error = "EvaluationFailure"; }
    stopwatch.Stop();
    var passed = error is null && proposal is not null && test.ExpectedActions.Contains(proposal.Action) &&
        test.ExpectedReasons.Contains(proposal.ReasonCode);
    decimal? estimated = reply?.InputTokens is { } inputCount && reply.OutputTokens is { } outputCount
        ? (inputCount * settings.InputUsdPerMillion + outputCount * settings.OutputUsdPerMillion) / 1_000_000m : null;
    var previous = baseline?.Results.SingleOrDefault(x => x.Id == test.Id);
    rows.Add(new(test.Id, test.Category, passed, proposal?.Action, proposal?.ReasonCode, error,
        reply?.InputTokens, reply?.OutputTokens, estimated, stopwatch.ElapsedMilliseconds, previous is { Passed: true } && !passed));
    Console.WriteLine($"{test.Id}: {(passed ? "PASS" : "FAIL")} {error ?? proposal?.Action}");
}
var report = new EvalReport(mode, mode == "fixture" ? "Contract harness only; no model quality measured" : "Live model evaluation",
    hash, ProposalProtocol.PromptVersion, ProposalProtocol.SchemaVersion, live?.Provider ?? "Fixture", live?.Model ?? "fixture-v1",
    settings.InputUsdPerMillion, settings.OutputUsdPerMillion, DateTimeOffset.UtcNow, stopReason, calls, reserved, budget,
    dataset.Length, rows.ToArray());
var output = Path.GetFullPath(options.GetValueOrDefault("--output", $"artifacts/evals-{mode}.json"));
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions(ProposalProtocol.JsonOptions) { WriteIndented = true }));
Console.WriteLine($"{rows.Count(x => x.Passed)}/{dataset.Length} passed; {calls} provider attempts; report: {output}");
return stopReason == "Completed" && rows.Count == dataset.Length && rows.All(x => x.Passed) ? 0 : 1;

internal sealed record EvalCase(string Id, string Category, string Subject, string Description, string Facts,
    string[] ExpectedActions, string[] ExpectedReasons, JsonElement Fixture);
internal sealed record EvalResult(string Id, string Category, bool Passed, string? Action, string? ReasonCode, string? ErrorCode,
    int? InputTokens, int? OutputTokens, decimal? EstimatedCostUsd, long LatencyMs, bool Regression);
internal sealed record EvalReport(string Mode, string Meaning, string DatasetHash, string PromptVersion, string SchemaVersion,
    string Provider, string Model, decimal InputUsdPerMillion, decimal OutputUsdPerMillion, DateTimeOffset CreatedAt,
    string Status, int ProviderAttempts, decimal ReservedCostUsd, decimal BudgetUsd, int ExpectedCases, EvalResult[] Results);
