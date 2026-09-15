using System.Collections.Immutable;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ResolveAI.Platform;

namespace ResolveAI.Host.Security;

internal sealed class DevelopmentAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var name = Request.Headers["X-Development-User"].ToString();
        if (name.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        // The caller chooses a predefined local fixture, never arbitrary tenant/permission claims.
        var tenant = name switch
        {
            "alpha-operator" or "alpha-reader" => "11111111-1111-1111-1111-111111111111",
            "beta-operator" => "22222222-2222-2222-2222-222222222222",
            _ => null
        };
        if (tenant is null) return Task.FromResult(AuthenticateResult.Fail("Unknown development identity."));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, name), new("tenant_id", tenant), new("actor_type", "Operator"),
            new("permission", "cases.read")
        };
        if (name != "alpha-reader")
        {
            claims.Add(new("permission", "cases.create"));
            claims.Add(new("permission", "audit.read"));
            claims.Add(new("permission", "customers.read"));
            claims.Add(new("permission", "orders.read"));
            claims.Add(new("permission", "payments.read"));
            claims.Add(new("permission", "proposals.create"));
            claims.Add(new("permission", "proposals.read"));
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

internal static class ActorMapping
{
    public static ActorContext ToActor(this ClaimsPrincipal principal) => new(
        Guid.Parse(principal.FindFirstValue("tenant_id")!), principal.FindFirstValue(ClaimTypes.NameIdentifier)!,
        principal.FindFirstValue("actor_type")!,
        principal.FindAll("permission").Select(x => x.Value).ToImmutableHashSet(StringComparer.Ordinal));
}
