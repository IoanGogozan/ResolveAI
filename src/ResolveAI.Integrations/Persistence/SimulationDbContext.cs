using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ResolveAI.Integrations.Persistence;

internal sealed class SimulatedCustomer
{
    public Guid TenantId { get; set; }
    public required string Id { get; set; }
    public required string DisplayName { get; set; }
}
internal sealed class SimulatedOrder
{
    public Guid TenantId { get; set; }
    public required string Id { get; set; }
    public required string CustomerId { get; set; }
    public required string Product { get; set; }
    public decimal Total { get; set; }
    public required string Currency { get; set; }
    public required string DeliveryStatus { get; set; }
}
internal sealed class SimulatedPayment
{
    public Guid TenantId { get; set; }
    public required string Id { get; set; }
    public required string OrderId { get; set; }
    public decimal CapturedAmount { get; set; }
    public decimal RefundedAmount { get; set; }
    public required string Currency { get; set; }
    public required string Status { get; set; }
}

internal sealed class SimulationDbContext(DbContextOptions<SimulationDbContext> options) : DbContext(options)
{
    public DbSet<SimulatedCustomer> Customers => Set<SimulatedCustomer>();
    public DbSet<SimulatedOrder> Orders => Set<SimulatedOrder>();
    public DbSet<SimulatedPayment> Payments => Set<SimulatedPayment>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("simulation");
        var customer = model.Entity<SimulatedCustomer>();
        customer.ToTable("customers");
        customer.HasKey(x => new { x.TenantId, x.Id });
        customer.Property(x => x.Id).HasMaxLength(100);
        customer.Property(x => x.DisplayName).HasMaxLength(200);
        var order = model.Entity<SimulatedOrder>();
        order.ToTable("orders");
        order.HasKey(x => new { x.TenantId, x.Id });
        order.Property(x => x.Id).HasMaxLength(100);
        order.Property(x => x.CustomerId).HasMaxLength(100);
        order.Property(x => x.Product).HasMaxLength(200);
        order.Property(x => x.Total).HasPrecision(18, 2);
        order.Property(x => x.Currency).HasMaxLength(3);
        order.Property(x => x.DeliveryStatus).HasMaxLength(30);
        order.HasOne<SimulatedCustomer>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.CustomerId }).OnDelete(DeleteBehavior.Restrict);
        var payment = model.Entity<SimulatedPayment>();
        payment.ToTable("payments", t => t.HasCheckConstraint("CK_payment_amounts", "\"CapturedAmount\" >= 0 AND \"RefundedAmount\" >= 0 AND \"RefundedAmount\" <= \"CapturedAmount\""));
        payment.HasKey(x => new { x.TenantId, x.Id });
        payment.Property(x => x.Id).HasMaxLength(100);
        payment.Property(x => x.OrderId).HasMaxLength(100);
        payment.Property(x => x.CapturedAmount).HasPrecision(18, 2);
        payment.Property(x => x.RefundedAmount).HasPrecision(18, 2);
        payment.Property(x => x.Currency).HasMaxLength(3);
        payment.Property(x => x.Status).HasMaxLength(30);
        payment.HasOne<SimulatedOrder>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.OrderId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SimulationDesignFactory : IDesignTimeDbContextFactory<SimulationDbContext>
{
    public SimulationDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SimulationDbContext>()
        .UseNpgsql("Host=localhost;Database=design_only", pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "simulation")).Options);
}
