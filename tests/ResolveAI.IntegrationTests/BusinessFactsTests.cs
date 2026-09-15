using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ResolveAI.CaseManagement.Contracts;
using ResolveAI.Execution.Contracts;
using ResolveAI.Integrations.DependencyInjection;
using ResolveAI.Platform;

namespace ResolveAI.IntegrationTests;

public sealed class BusinessFactsTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static readonly CaseBusinessReferences Monitor = new("customer-monitor", "order-monitor", "payment-monitor");
    private static CreateCaseRequest Request(CaseBusinessReferences? references) => new("Damaged monitor", "The monitor arrived broken.", references);
    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task References_and_updated_business_data_survive_restart_and_reseeding()
    {
        var connection = await database.CreateDatabaseAsync();
        Guid caseId;
        await using (var first = new AppFactory(connection))
        {
            await first.MigrateAsync();
            await first.Services.SeedSimulationAsync();
            using var client = first.Client();
            var created = await client.PostAsJsonAsync("/api/cases", Request(Monitor));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var item = (await created.Content.ReadFromJsonAsync<CaseSummary>())!;
            Assert.Equal(Monitor, item.References);
            caseId = item.Id;
        }
        // Simulate an external system change: restarting/reseeding must not reset live state.
        await ExecuteAsync(connection, """
            UPDATE simulation.payments SET "RefundedAmount" = 10
            WHERE "TenantId" = '11111111-1111-1111-1111-111111111111' AND "Id" = 'payment-monitor'
            """);
        await using var restarted = new AppFactory(connection);
        await restarted.MigrateAsync();
        await restarted.Services.SeedSimulationAsync();
        await restarted.Services.SeedSimulationAsync();
        using var restored = restarted.Client();
        var facts = (await restored.GetFromJsonAsync<CaseBusinessFacts>($"/api/cases/{caseId}/business-facts"))!;
        Assert.Equal("Synthetic Alpha monitor", facts.Customer.DisplayName);
        Assert.Equal("Monitor", facts.Order.Product);
        Assert.Equal(649m, facts.Payment.CapturedAmount);
        Assert.Equal(10m, facts.Payment.RefundedAmount);
        Assert.Equal("EUR", facts.Payment.Currency);
        Assert.Equal(Monitor, (await restored.GetFromJsonAsync<CaseSummary>($"/api/cases/{caseId}"))!.References);
    }

    [Fact]
    public async Task Same_external_ids_resolve_inside_the_actor_tenant_and_private_resources_are_hidden()
    {
        await using var app = new AppFactory(await database.CreateDatabaseAsync());
        await app.MigrateAsync();
        await app.Services.SeedSimulationAsync();
        using var alpha = app.Client();
        using var beta = app.Client("beta-operator");
        const string route = "/api/business/customers/customer-monitor/orders/order-monitor/payments/payment-monitor";
        Assert.Equal(649m, (await alpha.GetFromJsonAsync<PaymentFacts>(route))!.CapturedAmount);
        Assert.Equal(749m, (await beta.GetFromJsonAsync<PaymentFacts>(route))!.CapturedAmount);
        Assert.Equal("Synthetic Beta monitor", (await beta.GetFromJsonAsync<CustomerFacts>("/api/business/customers/customer-monitor"))!.DisplayName);
        foreach (var path in new[]
        {
            "/api/business/customers/customer-alpha-only",
            "/api/business/customers/customer-monitor/orders/order-alpha-only",
            "/api/business/customers/customer-monitor/orders/order-monitor/payments/payment-alpha-only"
        })
            Assert.Equal(HttpStatusCode.NotFound, (await beta.GetAsync(path)).StatusCode);
        var created = await alpha.PostAsJsonAsync("/api/cases", Request(Monitor));
        var item = (await created.Content.ReadFromJsonAsync<CaseSummary>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await beta.GetAsync($"/api/cases/{item.Id}/business-facts")).StatusCode);
    }

    [Theory]
    [InlineData("customer-other", "order-monitor", "payment-monitor", 422, "AssociationMismatch")]
    [InlineData("customer-monitor", "order-monitor", "payment-other", 422, "AssociationMismatch")]
    [InlineData("customer-missing", "order-monitor", "payment-monitor", 404, "NotFound")]
    [InlineData("customer-monitor", "order-missing", "payment-monitor", 404, "NotFound")]
    [InlineData("customer-monitor", "order-monitor", "payment-missing", 404, "NotFound")]
    [InlineData("", "order-monitor", "payment-monitor", 400, "InvalidReference")]
    [InlineData("customer-monitor", null, "payment-monitor", 400, "InvalidReference")]
    public async Task Invalid_associations_fail_before_any_case_is_saved(string customer, string? order, string payment, int status, string code)
    {
        var connection = await database.CreateDatabaseAsync();
        await using var app = new AppFactory(connection);
        await app.MigrateAsync();
        await app.Services.SeedSimulationAsync();
        using var client = app.Client();
        var response = await client.PostAsJsonAsync("/api/cases", Request(new(customer, order!, payment)));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync());
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var count = new NpgsqlCommand("SELECT COUNT(*) FROM case_management.cases", db);
        Assert.Equal(0L, await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Permissions_are_checked_in_HTTP_and_the_module_even_without_HTTP()
    {
        await using var app = new AppFactory(await database.CreateDatabaseAsync());
        await app.MigrateAsync();
        await app.Services.SeedSimulationAsync();
        using var reader = app.Client("alpha-reader");
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/business/customers/customer-monitor")).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var reads = scope.ServiceProvider.GetRequiredService<IBusinessReads>();
        var actor = new ActorContext(Guid.Parse("11111111-1111-1111-1111-111111111111"), "test-agent", "Agent", ImmutableHashSet<string>.Empty);
        await Assert.ThrowsAsync<AccessDeniedException>(() => reads.GetCustomerAsync(actor, Monitor.CustomerId));
        actor = actor with { Permissions = ImmutableHashSet.Create("customers.read") };
        Assert.Equal(Monitor.CustomerId, (await reads.GetCustomerAsync(actor, Monitor.CustomerId)).Id);
        await Assert.ThrowsAsync<AccessDeniedException>(() => reads.GetOrderAsync(actor, Monitor.CustomerId, Monitor.OrderId));
        actor = actor with { Permissions = actor.Permissions.Add("orders.read") };
        await Assert.ThrowsAsync<AccessDeniedException>(() => reads.GetPaymentAsync(actor, Monitor.CustomerId, Monitor.OrderId, Monitor.PaymentId));
    }

    [Fact]
    public async Task Unlinked_cases_remain_supported_and_changed_external_associations_are_revalidated()
    {
        var connection = await database.CreateDatabaseAsync();
        await using var app = new AppFactory(connection);
        await app.MigrateAsync();
        await app.Services.SeedSimulationAsync();
        using var client = app.Client();
        var unlinked = await client.PostAsJsonAsync("/api/cases", Request(null));
        var unlinkedCase = (await unlinked.Content.ReadFromJsonAsync<CaseSummary>())!;
        var missing = await client.GetAsync($"/api/cases/{unlinkedCase.Id}/business-facts");
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        Assert.Contains("MissingReferences", await missing.Content.ReadAsStringAsync());
        var linked = await client.PostAsJsonAsync("/api/cases", Request(Monitor));
        var item = (await linked.Content.ReadFromJsonAsync<CaseSummary>())!;
        await ExecuteAsync(connection, """
            UPDATE simulation.orders SET "CustomerId" = 'customer-other'
            WHERE "TenantId" = '11111111-1111-1111-1111-111111111111' AND "Id" = 'order-monitor'
            """);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"/api/cases/{item.Id}/business-facts")).StatusCode);
    }

    [Fact]
    public async Task Currency_mismatch_and_unavailable_store_have_explicit_safe_outcomes()
    {
        var connection = await database.CreateDatabaseAsync();
        await using var app = new AppFactory(connection);
        await app.MigrateAsync();
        await app.Services.SeedSimulationAsync();
        using var client = app.Client();
        await ExecuteAsync(connection, "UPDATE simulation.payments SET \"Currency\" = 'USD'");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/api/cases", Request(Monitor))).StatusCode);
        await ExecuteAsync(connection, "ALTER TABLE simulation.customers RENAME TO unavailable_customers");
        var failed = await client.GetAsync("/api/business/customers/customer-monitor");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Contains("Unavailable", await failed.Content.ReadAsStringAsync());
        Assert.DoesNotContain("simulation", await failed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Capability_spans_are_correlated_to_the_case_request()
    {
        var spans = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Telemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Add
        };
        ActivitySource.AddActivityListener(listener);
        await using var app = new AppFactory(await database.CreateDatabaseAsync());
        await app.MigrateAsync();
        await app.Services.SeedSimulationAsync();
        using var client = app.Client();
        var created = await client.PostAsJsonAsync("/api/cases", Request(Monitor));
        var item = (await created.Content.ReadFromJsonAsync<CaseSummary>())!;
        const string trace = "abcdef1234567890abcdef1234567890";
        client.DefaultRequestHeaders.Add("traceparent", $"00-{trace}-1234567890123456-01");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/cases/{item.Id}/business-facts")).StatusCode);
        var requestSpans = spans.Where(x => x.TraceId.ToString() == trace).ToArray();
        Assert.Contains(requestSpans, x => x.OperationName == "case.business_facts" && x.GetTagItem("case.id")?.ToString() == item.Id.ToString());
        foreach (var name in new[] { "get_customer", "get_order", "get_payment" })
            Assert.Contains(requestSpans, x => x.OperationName == name && x.GetTagItem("tenant.id") is not null);
    }
}
