using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ResolveAI.AI.Contracts;
using ResolveAI.Audit.Contracts;
using ResolveAI.CaseManagement.Contracts;
using ResolveAI.Integrations.DependencyInjection;

namespace ResolveAI.IntegrationTests;

public sealed class ProposalTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string Valid = """
        {"action":"RefundPayment","paymentId":"payment-monitor","amount":649,"currency":"EUR",
        "reasonCode":"DamagedItem","confidence":0.9,"evidence":["case","order","payment"]}
        """;
    private sealed class FakeGateway(Func<ProposalModelInput, CancellationToken, Task<ModelReply>> generate) : ILlmGateway
    {
        private int calls;
        public int Calls => calls;
        public string Provider => "Fixture";
        public string Model => "fixture-v1";
        public Task<ModelReply> GenerateAsync(ProposalModelInput input, CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            return generate(input, ct);
        }
    }
    private static ModelReply Reply(string? json = Valid, string outcome = "Completed") => new(json, "fixture-v1", outcome, 100, 50);
    private static async Task<CaseSummary> CreateAsync(HttpClient client, bool references = true)
    {
        var response = await client.PostAsJsonAsync("/api/cases", new CreateCaseRequest("Damaged monitor", "The monitor arrived broken.",
            references ? new("customer-monitor", "order-monitor", "payment-monitor") : null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CaseSummary>())!;
    }
    private static async Task<ProposalRun> GenerateAsync(HttpClient client, Guid caseId, Guid operationId)
    {
        var response = await client.PostAsJsonAsync($"/api/cases/{caseId}/proposals", new { operationId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProposalRun>())!;
    }

    [Fact]
    public async Task Concurrent_duplicate_and_restarted_requests_reuse_one_invocation_and_durable_audit()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new FakeGateway(async (_, ct) => { started.TrySetResult(); await release.Task.WaitAsync(ct); return Reply(); });
        var connection = await database.CreateDatabaseAsync();
        var operationId = Guid.NewGuid();
        Guid caseId;
        await using (var app = new AppFactory(connection, gateway: gateway))
        {
            await app.MigrateAsync(); await app.Services.SeedSimulationAsync();
            using var client = app.Client(); caseId = (await CreateAsync(client)).Id;
            var first = GenerateAsync(client, caseId, operationId);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                var second = await client.PostAsJsonAsync($"/api/cases/{caseId}/proposals", new { operationId });
                Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
                Assert.Equal("Running", (await second.Content.ReadFromJsonAsync<ProposalRun>())!.Status);
            }
            finally { release.TrySetResult(); }
            var result = await first;
            Assert.Equal("Completed", result.Status);
            Assert.Equal(649m, result.Proposal!.Amount);
            Assert.Equal(100, result.InputTokens);
            Assert.Equal(0.00012m, result.EstimatedCostUsd);
            Assert.Equal(ProposalProtocol.PromptVersion, result.PromptVersion);
            Assert.Equal(1, gateway.Calls);
        }
        var unexpected = new FakeGateway((_, _) => throw new InvalidOperationException("Should not call after restart"));
        await using var restarted = new AppFactory(connection, gateway: unexpected);
        await restarted.MigrateAsync();
        using var restored = restarted.Client();
        Assert.Equal("Completed", (await GenerateAsync(restored, caseId, operationId)).Status);
        Assert.Equal(0, unexpected.Calls);
        await using var scope = restarted.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICaseAuditDelivery>().DeliverBatchAsync();
        var delivery = scope.ServiceProvider.GetRequiredService<IProposalAuditDelivery>();
        await delivery.DeliverBatchAsync(); await delivery.DeliverBatchAsync();
        var audit = await restored.GetFromJsonAsync<AuditFact[]>($"/api/cases/{caseId}/audit");
        Assert.Single(audit!, x => x.Action == "ai.proposal.completed");
    }

    [Theory]
    [InlineData("bad json")]
    [InlineData("{\"action\":\"RefundPayment\"}")]
    [InlineData("{\"action\":\"RefundPayment\",\"paymentId\":\"wrong-payment\",\"amount\":649,\"currency\":\"EUR\",\"reasonCode\":\"DamagedItem\",\"confidence\":0.9,\"evidence\":[\"order\",\"payment\"]}")]
    [InlineData("{\"action\":\"RefundPayment\",\"paymentId\":\"payment-monitor\",\"amount\":99999,\"currency\":\"EUR\",\"reasonCode\":\"DamagedItem\",\"confidence\":0.9,\"evidence\":[\"order\",\"payment\"]}")]
    public async Task Invalid_output_is_never_published_as_a_proposal_or_automatically_retried(string raw)
    {
        var gateway = new FakeGateway((_, _) => Task.FromResult(Reply(raw)));
        await using var app = new AppFactory(await database.CreateDatabaseAsync(), gateway: gateway);
        await app.MigrateAsync(); await app.Services.SeedSimulationAsync();
        using var client = app.Client(); var item = await CreateAsync(client); var operation = Guid.NewGuid();
        var result = await GenerateAsync(client, item.Id, operation);
        Assert.Equal("Failed", result.Status); Assert.Equal("InvalidModelOutput", result.ErrorCode); Assert.Null(result.Proposal);
        Assert.Equal(100, result.InputTokens);
        await GenerateAsync(client, item.Id, operation);
        Assert.Equal(1, gateway.Calls);
    }

    [Theory]
    [InlineData("Refused")]
    [InlineData("Incomplete")]
    public async Task Refused_or_incomplete_replies_preserve_usage_but_no_proposal(string outcome)
    {
        var gateway = new FakeGateway((_, _) => Task.FromResult(Reply(null, outcome)));
        await using var app = new AppFactory(await database.CreateDatabaseAsync(), gateway: gateway);
        await app.MigrateAsync();
        using var client = app.Client(); var item = await CreateAsync(client, false);
        var run = await GenerateAsync(client, item.Id, Guid.NewGuid());
        Assert.Equal(outcome, run.ErrorCode); Assert.Equal("Failed", run.Status); Assert.Null(run.Proposal);
        Assert.Equal(50, run.OutputTokens);
    }

    [Fact]
    public async Task Permissions_tenant_and_operation_fingerprint_cannot_be_bypassed()
    {
        var gateway = new FakeGateway((_, _) => Task.FromResult(Reply()));
        await using var app = new AppFactory(await database.CreateDatabaseAsync(), gateway: gateway);
        await app.MigrateAsync(); await app.Services.SeedSimulationAsync();
        using var client = app.Client(); using var beta = app.Client("beta-operator"); using var reader = app.Client("alpha-reader");
        var item = await CreateAsync(client); var operation = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"/api/cases/{item.Id}/proposals", new { operationId = operation })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await beta.PostAsJsonAsync($"/api/cases/{item.Id}/proposals", new { operationId = operation })).StatusCode);
        await GenerateAsync(client, item.Id, operation);
        Assert.Equal(HttpStatusCode.NotFound, (await beta.GetAsync($"/api/cases/{item.Id}/proposals/{operation}")).StatusCode);
        var otherCase = await CreateAsync(client);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/cases/{otherCase.Id}/proposals", new { operationId = operation })).StatusCode);
        Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public async Task Budget_failure_happens_before_provider_invocation()
    {
        var gateway = new FakeGateway((_, _) => Task.FromResult(Reply()));
        await using var app = new AppFactory(await database.CreateDatabaseAsync(), gateway: gateway,
            aiSettings: new AiSettings { MaxInvocationCostUsd = 0.00000001m });
        await app.MigrateAsync();
        using var client = app.Client(); var item = await CreateAsync(client, false);
        var result = await GenerateAsync(client, item.Id, Guid.NewGuid());
        Assert.Equal("InvocationBudgetExceeded", result.ErrorCode); Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Timeout_and_provider_failure_do_not_retry_or_claim_zero_usage()
    {
        var timeout = new FakeGateway(async (_, ct) => { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return Reply(); });
        var connection = await database.CreateDatabaseAsync();
        await using var app = new AppFactory(connection, gateway: timeout, aiSettings: new AiSettings { TimeoutSeconds = 1 });
        await app.MigrateAsync();
        using var client = app.Client(); var item = await CreateAsync(client, false); var operation = Guid.NewGuid();
        var run = await GenerateAsync(client, item.Id, operation);
        Assert.Equal("Interrupted", run.Status); Assert.Equal("ModelTimeout", run.ErrorCode);
        Assert.Null(run.EstimatedCostUsd); Assert.Null(run.InputTokens);
        await GenerateAsync(client, item.Id, operation); Assert.Equal(1, timeout.Calls);
        var failure = new FakeGateway((_, _) => throw new ModelGatewayException("ProviderRateLimited"));
        await using var another = new AppFactory(connection, gateway: failure);
        using var anotherClient = another.Client();
        run = await GenerateAsync(anotherClient, item.Id, Guid.NewGuid());
        Assert.Equal("Failed", run.Status); Assert.Equal("ProviderRateLimited", run.ErrorCode); Assert.Equal(1, failure.Calls);
    }

    [Fact]
    public async Task Expired_orphan_is_marked_interrupted_without_reissuing_the_call()
    {
        var connection = await database.CreateDatabaseAsync();
        var gateway = new FakeGateway((_, _) => Task.FromResult(Reply()));
        await using var app = new AppFactory(connection, gateway: gateway);
        await app.MigrateAsync(); await app.Services.SeedSimulationAsync();
        using var client = app.Client(); var item = await CreateAsync(client); var operation = Guid.NewGuid();
        await GenerateAsync(client, item.Id, operation);
        // Reconstruct the durable state left by a terminated process before result commit.
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        await using var command = new NpgsqlCommand("""
            UPDATE ai.agent_runs SET "Status"='Running', "ProposalJson"=NULL, "CompletedAt"=NULL,
                "InputTokens"=NULL,"OutputTokens"=NULL,"EstimatedCostUsd"=NULL,"LeaseUntil"=NOW()-INTERVAL '1 minute'
            WHERE "OperationId"=@operation
            """, db);
        command.Parameters.AddWithValue("operation", operation); await command.ExecuteNonQueryAsync();
        var result = await GenerateAsync(client, item.Id, operation);
        Assert.Equal("Interrupted", result.Status); Assert.Equal("OutcomeUnknown", result.ErrorCode);
        Assert.Null(result.Proposal); Assert.Equal(1, gateway.Calls);
    }

    [Fact]
    public async Task Run_list_is_scoped_to_one_case_and_never_reports_an_abandoned_claim_as_running()
    {
        var connection = await database.CreateDatabaseAsync();
        var gateway = new FakeGateway((_, _) => Task.FromResult(Reply()));
        await using var app = new AppFactory(connection, gateway: gateway);
        await app.MigrateAsync(); await app.Services.SeedSimulationAsync();
        using var client = app.Client(); using var beta = app.Client("beta-operator"); using var reader = app.Client("alpha-reader");
        var item = await CreateAsync(client); var other = await CreateAsync(client);
        var first = await GenerateAsync(client, item.Id, Guid.NewGuid());
        await Task.Delay(2); // UUIDv7 only orders across distinct milliseconds.
        var second = await GenerateAsync(client, item.Id, Guid.NewGuid());
        var unrelated = await GenerateAsync(client, other.Id, Guid.NewGuid());

        var runs = (await client.GetFromJsonAsync<ProposalRun[]>($"/api/cases/{item.Id}/proposals"))!;
        Assert.Equal(new[] { second.RunId, first.RunId }, runs.Select(x => x.RunId).ToArray());
        Assert.All(runs, x => Assert.Equal(item.Id, x.CaseId));
        Assert.Equal(649m, runs[0].Proposal!.Amount);
        Assert.Equal(unrelated.RunId, Assert.Single((await client.GetFromJsonAsync<ProposalRun[]>($"/api/cases/{other.Id}/proposals"))!).RunId);
        Assert.Equal(HttpStatusCode.NotFound, (await beta.GetAsync($"/api/cases/{item.Id}/proposals")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/cases/{item.Id}/proposals")).StatusCode);

        // Reconstruct a claim left behind by a terminated process; the list must resolve it, not display it as active.
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        await using var command = new NpgsqlCommand("""
            UPDATE ai.agent_runs SET "Status"='Running', "CompletedAt"=NULL, "LeaseUntil"=NOW()-INTERVAL '1 minute'
            WHERE "RunId"=@run
            """, db);
        command.Parameters.AddWithValue("run", second.RunId); await command.ExecuteNonQueryAsync();
        var resolved = (await client.GetFromJsonAsync<ProposalRun[]>($"/api/cases/{item.Id}/proposals"))!;
        Assert.Equal("Interrupted", resolved[0].Status);
        Assert.Equal("OutcomeUnknown", resolved[0].ErrorCode);
        Assert.Equal(3, gateway.Calls);
    }

    [Fact]
    public void Schema_rejects_extra_duplicate_and_wrong_typed_fields()
    {
        var input = new ProposalModelInput("x", "y", "customer-monitor",
            new("order-monitor", "customer-monitor", "Monitor", 649, "EUR", "Delivered"),
            new("payment-monitor", "order-monitor", 649, 0, "EUR", "Captured"));
        foreach (var raw in new[]
        {
            Valid.Replace("0.9", "\"0.9\""),
            Valid.Replace("0.9", "2"),
            Valid.Replace("\"action\":", "\"tenantId\":\"other\",\"action\":"),
            Valid.Replace("\"action\":", "\"action\":\"NoAction\",\"action\":")
        }) Assert.Throws<ModelGatewayException>(() => ProposalProtocol.Validate(raw, input));
    }
}
