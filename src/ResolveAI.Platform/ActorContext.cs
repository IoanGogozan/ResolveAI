using System.Collections.Immutable;
using System.Diagnostics;

namespace ResolveAI.Platform;

public sealed record ActorContext(Guid TenantId, string ActorId, string ActorType,
    ImmutableHashSet<string> Permissions)
{
    public void Require(string permission)
    {
        if (TenantId == Guid.Empty || string.IsNullOrWhiteSpace(ActorId) || !Permissions.Contains(permission))
            throw new AccessDeniedException();
    }
}

public sealed class AccessDeniedException : Exception;

public static class Telemetry
{
    public const string SourceName = "ResolveAI";
    public static readonly ActivitySource Source = new(SourceName);
}
