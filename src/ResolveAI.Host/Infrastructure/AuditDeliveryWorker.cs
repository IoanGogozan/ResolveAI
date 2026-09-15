using ResolveAI.CaseManagement.Contracts;

namespace ResolveAI.Host.Infrastructure;

internal sealed class AuditDeliveryWorker(IServiceScopeFactory scopes, ILogger<AuditDeliveryWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ICaseAuditDelivery>().DeliverBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                // Do not log exception messages that could contain database values or connection strings.
                logger.LogError("Audit delivery failed ({ErrorType}); committed outbox entries remain pending",
                    exception.GetType().Name);
            }
            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
