using Microsoft.Extensions.DependencyInjection;
using ResolveAI.Execution.Contracts;

namespace ResolveAI.Execution.DependencyInjection;

public static class ExecutionModule
{
    public static IServiceCollection AddExecutionModule(this IServiceCollection services) =>
        services.AddScoped<IBusinessReads, Application.BusinessReads>();
}
