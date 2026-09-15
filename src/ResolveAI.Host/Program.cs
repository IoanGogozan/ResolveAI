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

var builder = WebApplication.CreateBuilder(args);
var localEnvironment = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
var developmentAuth = builder.Configuration.GetValue<bool>("DevelopmentAuth:Enabled");
if (developmentAuth && !localEnvironment)
    throw new InvalidOperationException("Development authentication is forbidden outside Development/Testing.");
if (!developmentAuth)
    throw new InvalidOperationException("No authentication provider configured. Delivery 1 supports explicit Development/Testing authentication only.");

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
    foreach (var permission in new[] { "cases.create", "cases.read", "audit.read" })
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireClaim("permission", permission));
});
builder.Services.AddAuditModule(connectionString);
builder.Services.AddCaseManagementModule(connectionString);
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
app.MapPost("/api/cases", async (CreateCaseRequest request, HttpContext http, ICaseService cases, CancellationToken ct) =>
{
    var result = await cases.CreateAsync(http.User.ToActor(), request, ct);
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
await app.RunAsync();

public partial class Program;
