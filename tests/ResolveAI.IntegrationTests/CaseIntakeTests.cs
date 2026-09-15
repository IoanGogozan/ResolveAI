using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ResolveAI.Audit.Contracts;
using ResolveAI.CaseManagement.Contracts;
using ResolveAI.Platform;

namespace ResolveAI.IntegrationTests;

public sealed class CaseIntakeTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private static async Task<CaseSummary> CreateAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/cases", new { subject = "Damaged monitor", description = "The monitor arrived broken." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<CaseSummary>())!;
        Assert.Equal($"/api/cases/{item.Id}", response.Headers.Location?.OriginalString);
        return item;
    }

    private static async Task DeliverAsync(AppFactory app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICaseAuditDelivery>().DeliverBatchAsync();
    }

    [Fact]
    public async Task Case_and_pending_audit_survive_a_fresh_host_and_migrations_are_repeatable()
    {
        var connection = await database.CreateDatabaseAsync();
        Guid id;
        await using (var first = new AppFactory(connection))
        {
            await first.MigrateAsync();
            using var client = first.Client();
            id = (await CreateAsync(client)).Id;
            var before = await client.GetFromJsonAsync<AuditFact[]>($"/api/cases/{id}/audit");
            Assert.Empty(before!); // Dispatch is stopped, simulating interruption before publication.
        }
        await using var restarted = new AppFactory(connection);
        await restarted.MigrateAsync();
        using var restored = restarted.Client();
        var item = await restored.GetFromJsonAsync<CaseSummary>($"/api/cases/{id}");
        Assert.Equal("Damaged monitor", item!.Subject);
        await DeliverAsync(restarted);
        var facts = await restored.GetFromJsonAsync<AuditFact[]>($"/api/cases/{id}/audit");
        var fact = Assert.Single(facts!);
        Assert.Equal("case.created", fact.Action);
        Assert.Equal("alpha-operator", fact.ActorId);
        Assert.Equal(id, fact.CaseId);
        Assert.False(string.IsNullOrEmpty(fact.TraceParent));
    }

    [Fact]
    public async Task Cross_tenant_reads_audit_and_body_tenant_spoofing_are_blocked()
    {
        await using var app = new AppFactory(await database.CreateDatabaseAsync());
        await app.MigrateAsync();
        using var alpha = app.Client();
        using var beta = app.Client("beta-operator");
        var item = await CreateAsync(alpha);
        await DeliverAsync(app);
        Assert.Equal(HttpStatusCode.NotFound, (await beta.GetAsync($"/api/cases/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await beta.GetAsync($"/api/cases/{item.Id}/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alpha.GetAsync($"/api/cases/{item.Id}")).StatusCode);
        var spoof = await alpha.PostAsJsonAsync("/api/cases", new
        {
            subject = "x", description = "x", tenantId = "22222222-2222-2222-2222-222222222222"
        });
        Assert.Equal(HttpStatusCode.BadRequest, spoof.StatusCode);
        var other = await CreateAsync(beta);
        Assert.Equal(HttpStatusCode.NotFound, (await alpha.GetAsync($"/api/cases/{other.Id}")).StatusCode);
    }

    [Fact]
    public async Task Authentication_permissions_and_validation_are_enforced()
    {
        await using var app = new AppFactory(await database.CreateDatabaseAsync());
        await app.MigrateAsync();
        using var anonymous = app.Client(null);
        using var unknown = app.Client("invented-admin");
        using var reader = app.Client("alpha-reader");
        using var writer = app.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/cases/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await unknown.GetAsync($"/api/cases/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync("/api/cases", new { subject = "x", description = "y" })).StatusCode);
        var item = await CreateAsync(writer);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/cases/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"/api/cases/{item.Id}/audit")).StatusCode);
        foreach (var subject in new[] { "", "  ", new string('x', 201) })
            Assert.Equal(HttpStatusCode.BadRequest, (await writer.PostAsJsonAsync("/api/cases", new { subject, description = "text" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await writer.PostAsJsonAsync("/api/cases", new { subject = "x", description = (string?)null })).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var unprivileged = new ActorContext(Guid.NewGuid(), "worker", "System", ImmutableHashSet<string>.Empty);
        await Assert.ThrowsAsync<AccessDeniedException>(() => scope.ServiceProvider.GetRequiredService<ICaseService>()
            .CreateAsync(unprivileged, new CreateCaseRequest("x", "y")));
        await Assert.ThrowsAsync<AccessDeniedException>(() => scope.ServiceProvider.GetRequiredService<IAuditReader>()
            .GetForCaseAsync(unprivileged, item.Id));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Development_authentication_cannot_start_in_other_environments(string environment)
    {
        await using var app = new AppFactory(await database.CreateDatabaseAsync(), environment);
        var exception = Assert.Throws<InvalidOperationException>(() => app.CreateClient());
        Assert.Contains("forbidden outside Development/Testing", exception.Message);
    }

    [Fact]
    public async Task Audit_commit_before_ack_recovers_without_duplicate_even_with_concurrent_delivery()
    {
        var connection = await database.CreateDatabaseAsync();
        Guid id;
        await using (var interrupted = new AppFactory(connection, failAfterAuditCommit: true))
        {
            await interrupted.MigrateAsync();
            using var client = interrupted.Client();
            id = (await CreateAsync(client)).Id;
            await Assert.ThrowsAsync<IOException>(() => DeliverAsync(interrupted));
            Assert.Single((await client.GetFromJsonAsync<AuditFact[]>($"/api/cases/{id}/audit"))!);
        }
        await using var restarted = new AppFactory(connection);
        await restarted.MigrateAsync();
        await Task.WhenAll(DeliverAsync(restarted), DeliverAsync(restarted));
        await DeliverAsync(restarted);
        using var restored = restarted.Client();
        Assert.Single((await restored.GetFromJsonAsync<AuditFact[]>($"/api/cases/{id}/audit"))!);
        await using var scope = restarted.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ICaseAuditDelivery>().DeliverBatchAsync());
    }

    [Fact]
    public async Task Failed_outbox_insert_rolls_back_the_case_in_the_same_transaction()
    {
        var connectionString = await database.CreateDatabaseAsync();
        await using var app = new AppFactory(connectionString);
        await app.MigrateAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var inject = new NpgsqlCommand("""
            CREATE FUNCTION case_management.reject_outbox() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'injected persistence failure'; END; $$;
            CREATE TRIGGER reject_outbox BEFORE INSERT ON case_management.case_created_outbox
            FOR EACH ROW EXECUTE FUNCTION case_management.reject_outbox();
            """, connection))
            await inject.ExecuteNonQueryAsync();
        using var client = app.Client();
        var response = await client.PostAsJsonAsync("/api/cases", new { subject = "x", description = "y" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("injected", await response.Content.ReadAsStringAsync());
        await using var count = new NpgsqlCommand("SELECT COUNT(*) FROM case_management.cases", connection);
        Assert.Equal(0L, await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Background_worker_delivers_audit_and_preserves_trace_correlation()
    {
        var connection = await database.CreateDatabaseAsync();
        await using (var setup = new AppFactory(connection)) await setup.MigrateAsync();
        await using var app = new AppFactory(connection, dispatch: true);
        using var client = app.Client();
        const string traceId = "12345678901234567890123456789012";
        client.DefaultRequestHeaders.Add("traceparent", $"00-{traceId}-1234567890123456-01");
        var item = await CreateAsync(client);
        AuditFact[] facts = [];
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(15))
        {
            facts = (await client.GetFromJsonAsync<AuditFact[]>($"/api/cases/{item.Id}/audit"))!;
            if (facts.Length > 0) break;
            await Task.Delay(100);
        }
        Assert.Contains(traceId, Assert.Single(facts).TraceParent!);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/openapi/v1.json")).StatusCode);
    }
}
