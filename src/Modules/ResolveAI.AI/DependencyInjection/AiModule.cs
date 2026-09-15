using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ResolveAI.AI.Application;
using ResolveAI.AI.Contracts;
using ResolveAI.AI.Infrastructure;
using ResolveAI.AI.Persistence;

namespace ResolveAI.AI.DependencyInjection;

public static class AiModule
{
    public static ILlmGateway CreateOpenAiGateway(AiSettings settings)
    {
        settings.Validate();
        return new OpenAiGateway(settings);
    }

    public static IServiceCollection AddAiModule(this IServiceCollection services, string connectionString, AiSettings settings)
    {
        settings.Validate();
        services.AddSingleton(settings);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ILlmGateway>(CreateOpenAiGateway(settings));
        services.AddDbContext<AiDbContext>(o => o.UseNpgsql(connectionString,
            pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "ai")));
        services.AddScoped<IProposals, Proposals>();
        services.AddScoped<IProposalAuditDelivery, ProposalAuditDelivery>();
        return services;
    }

    public static async Task MigrateAiAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AiDbContext>().Database.MigrateAsync(ct);
    }
}
