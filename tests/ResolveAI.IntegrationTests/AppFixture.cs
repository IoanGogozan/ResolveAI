using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ResolveAI.Audit.Contracts;
using ResolveAI.Audit.DependencyInjection;
using ResolveAI.CaseManagement.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ResolveAI.IntegrationTests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18.3").Build();
    public Task InitializeAsync() => container.StartAsync();
    public async Task DisposeAsync() => await container.DisposeAsync();

    public async Task<string> CreateDatabaseAsync()
    {
        var database = "test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {database}", connection);
        await command.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = database }.ConnectionString;
    }
}

public sealed class AppFactory(string connectionString, string environment = "Testing",
    bool dispatch = false, bool failAfterAuditCommit = false) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Database", connectionString);
        builder.UseSetting("DevelopmentAuth:Enabled", "true");
        builder.UseSetting("Audit:DispatchEnabled", dispatch.ToString());
        if (failAfterAuditCommit)
            builder.ConfigureTestServices(services =>
            {
                var original = services.Single(x => x.ServiceType == typeof(IAuditWriter));
                services.RemoveAll<IAuditWriter>();
                services.AddScoped<IAuditWriter>(sp => new FailAfterCommit((IAuditWriter)original.ImplementationFactory!(sp)));
            });
    }

    public async Task MigrateAsync()
    {
        await Services.MigrateAuditAsync();
        await Services.MigrateCaseManagementAsync();
    }

    public HttpClient Client(string? user = "alpha-operator")
    {
        var client = CreateClient();
        if (user is not null) client.DefaultRequestHeaders.Add("X-Development-User", user);
        return client;
    }

    private sealed class FailAfterCommit(IAuditWriter inner) : IAuditWriter
    {
        public async Task AppendAsync(AuditFact fact, CancellationToken cancellationToken = default)
        {
            await inner.AppendAsync(fact, cancellationToken);
            throw new IOException("Injected interruption after audit commit, before outbox acknowledgement.");
        }
    }
}
