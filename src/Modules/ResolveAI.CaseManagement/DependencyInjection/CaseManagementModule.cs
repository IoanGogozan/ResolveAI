using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ResolveAI.CaseManagement.Application;
using ResolveAI.CaseManagement.Contracts;
using ResolveAI.CaseManagement.Persistence;

namespace ResolveAI.CaseManagement.DependencyInjection;

public static class CaseManagementModule
{
    public static IServiceCollection AddCaseManagementModule(this IServiceCollection services, string connectionString)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<CaseDbContext>(options => options.UseNpgsql(connectionString,
            pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "case_management")));
        services.AddScoped<ICaseService, CaseService>();
        services.AddScoped<ICaseAuditDelivery, CaseAuditDelivery>();
        return services;
    }

    public static async Task MigrateCaseManagementAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CaseDbContext>().Database.MigrateAsync(ct);
    }
}
