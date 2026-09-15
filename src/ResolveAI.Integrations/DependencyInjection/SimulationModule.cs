using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveAI.Execution.Contracts;
using ResolveAI.Integrations.Application;
using ResolveAI.Integrations.Persistence;

namespace ResolveAI.Integrations.DependencyInjection;

public static class SimulationModule
{
    public static IServiceCollection AddSimulatedIntegrations(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SimulationDbContext>(o => o.UseNpgsql(connectionString,
            pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "simulation")));
        services.AddScoped<SimulatedBusinessReads>();
        services.AddScoped<ICustomerReadPort>(sp => sp.GetRequiredService<SimulatedBusinessReads>());
        services.AddScoped<IOrderReadPort>(sp => sp.GetRequiredService<SimulatedBusinessReads>());
        services.AddScoped<IPaymentReadPort>(sp => sp.GetRequiredService<SimulatedBusinessReads>());
        return services;
    }

    public static async Task MigrateSimulationAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SimulationDbContext>().Database.MigrateAsync(ct);
    }

    // Host restricts this explicit setup operation to Development/Testing; never called at normal startup.
    public static async Task SeedSimulationAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SimulationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var (tenant, label, amount) in new[]
        {
            (Guid.Parse("11111111-1111-1111-1111-111111111111"), "Alpha", 649m),
            (Guid.Parse("22222222-2222-2222-2222-222222222222"), "Beta", 749m)
        })
        {
            foreach (var suffix in label == "Alpha" ? new[] { "monitor", "other", "alpha-only" } : new[] { "monitor", "other" })
            {
                var customer = $"customer-{suffix}";
                var order = $"order-{suffix}";
                var payment = $"payment-{suffix}";
                var name = $"Synthetic {label} {suffix}";
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO simulation.customers ("TenantId", "Id", "DisplayName") VALUES ({tenant}, {customer}, {name})
                    ON CONFLICT ("TenantId", "Id") DO NOTHING;
                    INSERT INTO simulation.orders ("TenantId", "Id", "CustomerId", "Product", "Total", "Currency", "DeliveryStatus")
                    VALUES ({tenant}, {order}, {customer}, 'Monitor', {amount}, 'EUR', 'Delivered')
                    ON CONFLICT ("TenantId", "Id") DO NOTHING;
                    INSERT INTO simulation.payments ("TenantId", "Id", "OrderId", "CapturedAmount", "RefundedAmount", "Currency", "Status")
                    VALUES ({tenant}, {payment}, {order}, {amount}, 0, 'EUR', 'Captured')
                    ON CONFLICT ("TenantId", "Id") DO NOTHING;
                    """, ct);
            }
        }
        await transaction.CommitAsync(ct);
    }
}
