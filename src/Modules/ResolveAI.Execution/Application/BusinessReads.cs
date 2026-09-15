using ResolveAI.Execution.Contracts;
using ResolveAI.Platform;

namespace ResolveAI.Execution.Application;

internal sealed class BusinessReads(ICustomerReadPort customers, IOrderReadPort orders, IPaymentReadPort payments) : IBusinessReads
{
    public Task<CustomerFacts> GetCustomerAsync(ActorContext actor, string customerId, CancellationToken ct = default) =>
        ReadAsync(actor, BusinessReadCapabilities.Customer, customerId, token => customers.GetAsync(actor.TenantId, customerId, token), ct);

    public async Task<OrderFacts> GetOrderAsync(ActorContext actor, string customerId, string orderId, CancellationToken ct = default)
    {
        actor.Require(BusinessReadCapabilities.Order.Permission);
        var customer = await GetCustomerAsync(actor, customerId, ct);
        return await ReadOrderAsync(actor, customer.Id, orderId, ct);
    }

    public async Task<PaymentFacts> GetPaymentAsync(ActorContext actor, string customerId, string orderId, string paymentId, CancellationToken ct = default) =>
        (await ResolveAsync(actor, customerId, orderId, paymentId, ct)).Payment;

    public async Task<CaseBusinessFacts> ResolveAsync(ActorContext actor, string customerId, string orderId, string paymentId, CancellationToken ct = default)
    {
        // Check all required permissions before performing any read.
        actor.Require(BusinessReadCapabilities.Customer.Permission);
        actor.Require(BusinessReadCapabilities.Order.Permission);
        actor.Require(BusinessReadCapabilities.Payment.Permission);
        var customer = await GetCustomerAsync(actor, customerId, ct);
        var order = await ReadOrderAsync(actor, customer.Id, orderId, ct);
        var payment = await ReadAsync(actor, BusinessReadCapabilities.Payment, paymentId,
            token => payments.GetAsync(actor.TenantId, paymentId, token), ct);
        if (payment.OrderId != order.Id || payment.Currency != order.Currency)
            throw new BusinessReadException(BusinessReadFailure.AssociationMismatch);
        return new(customer, order, payment);
    }

    private async Task<OrderFacts> ReadOrderAsync(ActorContext actor, string customerId, string orderId, CancellationToken ct)
    {
        var order = await ReadAsync(actor, BusinessReadCapabilities.Order, orderId,
            token => orders.GetAsync(actor.TenantId, orderId, token), ct);
        if (order.CustomerId != customerId) throw new BusinessReadException(BusinessReadFailure.AssociationMismatch);
        return order;
    }

    private static async Task<T> ReadAsync<T>(ActorContext actor, ReadCapability capability, string id,
        Func<CancellationToken, Task<T?>> read, CancellationToken ct) where T : class
    {
        actor.Require(capability.Permission);
        if (string.IsNullOrWhiteSpace(id) || id.Length > 100 || id != id.Trim())
            throw new BusinessReadException(BusinessReadFailure.InvalidReference);
        using var activity = Telemetry.Source.StartActivity(capability.Name);
        activity?.SetTag("tenant.id", actor.TenantId);
        activity?.SetTag("capability.name", capability.Name);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(capability.Timeout);
        try
        {
            var result = await read(deadline.Token);
            if (result is null) throw new BusinessReadException(BusinessReadFailure.NotFound);
            activity?.SetTag("capability.outcome", "found");
            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            activity?.SetTag("capability.outcome", "timeout");
            throw new BusinessReadException(BusinessReadFailure.Unavailable);
        }
        catch (BusinessReadException exception)
        {
            activity?.SetTag("capability.outcome", exception.Failure.ToString());
            throw;
        }
    }
}
