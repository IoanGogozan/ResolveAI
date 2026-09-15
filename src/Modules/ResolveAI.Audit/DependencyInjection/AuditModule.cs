using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResolveAI.Audit.Application;
using ResolveAI.Audit.Contracts;
using ResolveAI.Audit.Persistence;

namespace ResolveAI.Audit.DependencyInjection;

public static class AuditModule
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AuditDbContext>(options => options.UseNpgsql(connectionString,
            pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "audit")));
        services.AddScoped<AuditLog>();
        services.AddScoped<IAuditWriter>(sp => sp.GetRequiredService<AuditLog>());
        services.AddScoped<IAuditReader>(sp => sp.GetRequiredService<AuditLog>());
        return services;
    }

    public static async Task MigrateAuditAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.MigrateAsync(ct);
    }
}
