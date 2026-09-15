using Microsoft.EntityFrameworkCore;
using Npgsql;
using ResolveAI.Execution.Contracts;
using ResolveAI.Integrations.Persistence;

namespace ResolveAI.Integrations.Application;

internal sealed class SimulatedBusinessReads(SimulationDbContext db) : ICustomerReadPort, IOrderReadPort, IPaymentReadPort
{
    Task<CustomerFacts?> ICustomerReadPort.GetAsync(Guid tenantId, string id, CancellationToken ct) => TranslateAsync(() =>
        db.Customers.AsNoTracking().Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => new CustomerFacts(x.Id, x.DisplayName)).SingleOrDefaultAsync(ct));

    Task<OrderFacts?> IOrderReadPort.GetAsync(Guid tenantId, string id, CancellationToken ct) => TranslateAsync(() =>
        db.Orders.AsNoTracking().Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => new OrderFacts(x.Id, x.CustomerId, x.Product, x.Total, x.Currency, x.DeliveryStatus)).SingleOrDefaultAsync(ct));

    Task<PaymentFacts?> IPaymentReadPort.GetAsync(Guid tenantId, string id, CancellationToken ct) => TranslateAsync(() =>
        db.Payments.AsNoTracking().Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => new PaymentFacts(x.Id, x.OrderId, x.CapturedAmount, x.RefundedAmount, x.Currency, x.Status)).SingleOrDefaultAsync(ct));

    private static async Task<T> TranslateAsync<T>(Func<Task<T>> read)
    {
        try { return await read(); }
        catch (NpgsqlException) { throw new BusinessReadException(BusinessReadFailure.Unavailable); }
    }
}
