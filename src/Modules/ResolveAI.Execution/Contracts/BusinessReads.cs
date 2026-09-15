using ResolveAI.Platform;

namespace ResolveAI.Execution.Contracts;

public sealed record CustomerFacts(string Id, string DisplayName);
public sealed record OrderFacts(string Id, string CustomerId, string Product, decimal Total, string Currency, string DeliveryStatus);
public sealed record PaymentFacts(string Id, string OrderId, decimal CapturedAmount, decimal RefundedAmount, string Currency, string Status);
public sealed record CaseBusinessFacts(CustomerFacts Customer, OrderFacts Order, PaymentFacts Payment);

// Ports are owned by Execution; adapters receive trusted tenant scope separately from resource IDs.
public interface ICustomerReadPort
{
    Task<CustomerFacts?> GetAsync(Guid tenantId, string id, CancellationToken ct);
}
public interface IOrderReadPort
{
    Task<OrderFacts?> GetAsync(Guid tenantId, string id, CancellationToken ct);
}
public interface IPaymentReadPort
{
    Task<PaymentFacts?> GetAsync(Guid tenantId, string id, CancellationToken ct);
}

public sealed record ReadCapability(string Name, string Permission, TimeSpan Timeout)
{
    public string Risk => "Low";
    public bool HasSideEffects => false;
}

public static class BusinessReadCapabilities
{
    public static readonly ReadCapability Customer = new("get_customer", "customers.read", TimeSpan.FromSeconds(5));
    public static readonly ReadCapability Order = new("get_order", "orders.read", TimeSpan.FromSeconds(5));
    public static readonly ReadCapability Payment = new("get_payment", "payments.read", TimeSpan.FromSeconds(5));
}

public interface IBusinessReads
{
    Task<CustomerFacts> GetCustomerAsync(ActorContext actor, string customerId, CancellationToken ct = default);
    Task<OrderFacts> GetOrderAsync(ActorContext actor, string customerId, string orderId, CancellationToken ct = default);
    Task<PaymentFacts> GetPaymentAsync(ActorContext actor, string customerId, string orderId, string paymentId, CancellationToken ct = default);
    Task<CaseBusinessFacts> ResolveAsync(ActorContext actor, string customerId, string orderId, string paymentId, CancellationToken ct = default);
}

public enum BusinessReadFailure { InvalidReference, NotFound, AssociationMismatch, Unavailable }
public sealed class BusinessReadException(BusinessReadFailure failure) : Exception(failure.ToString())
{
    public BusinessReadFailure Failure { get; } = failure;
}
