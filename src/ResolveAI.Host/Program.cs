using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ResolveAI.Audit.Contracts;
using ResolveAI.Audit.DependencyInjection;
using ResolveAI.CaseManagement.Contracts;
using ResolveAI.CaseManagement.DependencyInjection;
using ResolveAI.Host.Infrastructure;
using ResolveAI.Host.Security;
using ResolveAI.Platform;
using ResolveAI.Execution.Contracts;
using ResolveAI.Execution.DependencyInjection;
using ResolveAI.Integrations.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
var localEnvironment = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
var developmentAuth = builder.Configuration.GetValue<bool>("DevelopmentAuth:Enabled");
if (developmentAuth && !localEnvironment)
    throw new InvalidOperationException("Development authentication is forbidden outside Development/Testing.");
if (!developmentAuth)
    throw new InvalidOperationException("No authentication provider configured. This version supports explicit Development/Testing authentication only.");

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 65536);
builder.Services.AddOpenApi();
builder.Services.AddAuthentication(DevelopmentAuthentication.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthentication>(DevelopmentAuthentication.SchemeName, _ => { });
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in new[] { "cases.create", "cases.read", "audit.read", "customers.read", "orders.read", "payments.read" })
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireClaim("permission", permission));
});
builder.Services.AddAuditModule(connectionString);
builder.Services.AddCaseManagementModule(connectionString);
builder.Services.AddExecutionModule();
builder.Services.AddSimulatedIntegrations(connectionString);
if (builder.Configuration.GetValue("Audit:DispatchEnabled", true))
    builder.Services.AddHostedService<AuditDeliveryWorker>();
builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService("ResolveAI.Host"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation().AddSource(Telemetry.SourceName);
        if (!string.IsNullOrEmpty(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            tracing.AddOtlpExporter();
    });

var app = builder.Build();
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await app.Services.MigrateAuditAsync();
    await app.Services.MigrateCaseManagementAsync();
    await app.Services.MigrateSimulationAsync();
    await app.DisposeAsync();
    return;
}
if (args.Contains("--seed-demo", StringComparer.Ordinal))
{
    await app.Services.SeedSimulationAsync();
    await app.DisposeAsync();
    return;
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapOpenApi();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (CancellationToken ct) =>
{
    try
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
    catch (NpgsqlException) { return Results.Problem(statusCode: 503, title: "Database unavailable."); }
});
app.MapPost("/api/cases", async (CreateCaseRequest request, HttpContext http, ICaseService cases, IBusinessReads reads, CancellationToken ct) =>
{
    var actor = http.User.ToActor();
    using var activity = Telemetry.Source.StartActivity("case.intake");
    activity?.SetTag("tenant.id", actor.TenantId);
    if (request.References is { } references)
        await reads.ResolveAsync(actor, references.CustomerId, references.OrderId, references.PaymentId, ct);
    var result = await cases.CreateAsync(actor, request, ct);
    activity?.SetTag("case.id", result.Id);
    return Results.Created($"/api/cases/{result.Id}", result);
}).RequireAuthorization("cases.create");
app.MapGet("/api/cases/{id:guid}", async (Guid id, HttpContext http, ICaseService cases, CancellationToken ct) =>
{
    var result = await cases.GetAsync(http.User.ToActor(), id, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
}).RequireAuthorization("cases.read");
app.MapGet("/api/cases/{id:guid}/audit", async (Guid id, HttpContext http, ICaseService cases, IAuditReader audit, CancellationToken ct) =>
{
    var actor = http.User.ToActor();
    if (await cases.GetAsync(actor, id, ct) is null) return Results.NotFound();
    return Results.Ok(await audit.GetForCaseAsync(actor, id, ct));
}).RequireAuthorization("cases.read", "audit.read");
app.MapGet("/api/cases/{id:guid}/business-facts", async (Guid id, HttpContext http, ICaseService cases, IBusinessReads reads, CancellationToken ct) =>
{
    var actor = http.User.ToActor();
    using var activity = Telemetry.Source.StartActivity("case.business_facts");
    activity?.SetTag("tenant.id", actor.TenantId);
    activity?.SetTag("case.id", id);
    var item = await cases.GetAsync(actor, id, ct);
    if (item is null) return Results.NotFound();
    if (item.References is not { } references)
        return Results.Problem(statusCode: 409, title: "Case has no business references.",
            extensions: new Dictionary<string, object?> { ["code"] = "MissingReferences" });
    return Results.Ok(await reads.ResolveAsync(actor, references.CustomerId, references.OrderId, references.PaymentId, ct));
}).RequireAuthorization("cases.read", "customers.read", "orders.read", "payments.read");
app.MapGet("/api/business/customers/{customerId}", async (string customerId, HttpContext http, IBusinessReads reads, CancellationToken ct) =>
    Results.Ok(await reads.GetCustomerAsync(http.User.ToActor(), customerId, ct))).RequireAuthorization("customers.read");
app.MapGet("/api/business/customers/{customerId}/orders/{orderId}", async (string customerId, string orderId, HttpContext http, IBusinessReads reads, CancellationToken ct) =>
    Results.Ok(await reads.GetOrderAsync(http.User.ToActor(), customerId, orderId, ct))).RequireAuthorization("customers.read", "orders.read");
app.MapGet("/api/business/customers/{customerId}/orders/{orderId}/payments/{paymentId}", async (string customerId, string orderId, string paymentId, HttpContext http, IBusinessReads reads, CancellationToken ct) =>
    Results.Ok(await reads.GetPaymentAsync(http.User.ToActor(), customerId, orderId, paymentId, ct))).RequireAuthorization("customers.read", "orders.read", "payments.read");
await app.RunAsync();

public partial class Program;
