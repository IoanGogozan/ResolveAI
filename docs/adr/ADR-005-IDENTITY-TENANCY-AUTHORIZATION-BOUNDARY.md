# ADR-005 — Normalize Identity and Tenant Context Behind a Platform Security Boundary

**Status:** Accepted  
**Date:** 2026-09-15  
**Related:** ADR-001 through ADR-004

---

# 1. Context

ResolveAI needs to support:

- human operators;
- administrators;
- AI agents;
- background workers;
- multi-tenant resource isolation;
- high-risk approval;
- tool-level permissions;
- long-running workflows.

ASP.NET Core authentication, business policy, approval authority and tool execution are distinct concerns.

Without a normalized security boundary, provider-specific claims or `HttpContext` can easily leak into domain modules and couple the system to one identity provider or delivery mechanism.

---

# 2. Decision

ResolveAI will normalize authenticated identity into a trusted internal execution context.

Conceptually:

```text
OIDC / Development Auth
        ↓
ASP.NET Core security adapter
        ↓
IExecutionContext
        ↓
application/module contracts
```

The exact interface name is implementation detail.

The context includes stable trusted concepts such as:

```text
ActorId
ActorType
TenantId
Permissions
Roles (if needed)
CorrelationId
```

It does not contain raw tokens or provider-specific claim structures.

---

# 3. Tenant decision

Tenant identity is backend-controlled trusted context.

It must never be accepted as authoritative from:

- LLM output;
- tool arguments;
- arbitrary request payloads;
- retrieved knowledge.

If tenant selection is user-driven, membership/permission is validated before creating the trusted context.

---

# 4. HttpContext decision

Core modules and orchestration/business logic will not depend on `HttpContext` or `IHttpContextAccessor`.

The delivery/platform layer translates HTTP identity into normalized execution context.

Background workers and Temporal activities use equivalent trusted internal context.

---

# 5. Authorization decision

ResolveAI uses capability/permission-oriented authorization.

Examples:

```text
cases.read
approvals.approve
approvals.approve_high_value
payments.refund.request
```

Roles may group permissions.

Core checks should not rely exclusively on role names.

---

# 6. Business policy decision

ASP.NET authorization does not replace Decisioning.

Example:

```text
approvals.approve
```

allows an actor to attempt approval.

Decisioning still determines whether that actor is sufficient for the specific policy requirement.

---

# 7. Approval authority decision

Decisioning owns approval authority.

Approval validation includes:

- tenant;
- actor authority;
- status;
- expiration;
- policy requirement;
- action fingerprint.

Execution relies on a valid approval result where required.

---

# 8. AI identity decision

The AI agent is a constrained machine actor.

It receives only explicit request/tool capabilities.

A permission such as:

```text
payments.refund.request
```

does not imply:

```text
payments.refund.execute
```

The AI cannot elevate its own permissions.

---

# 9. Worker/system identity decision

Background workers use trusted system identity but do not gain unrestricted business authority.

Audit preserves separate identities for:

```text
Requester
Approver
Executor
```

where relevant.

---

# 10. Identity-provider decision

The domain architecture remains provider-agnostic.

V0 may use a development/test authentication handler.

V1 should use an OIDC-compatible identity provider.

Keycloak is the preferred V1 portfolio implementation in document 11; exact production configuration remains deferred.

---

# 11. Options considered

## Option A — Use ClaimsPrincipal/HttpContext everywhere

Rejected because:

- couples domain logic to HTTP;
- complicates workers/Temporal;
- leaks provider-specific claims;
- weakens testability.

## Option B — Pass TenantId/UserId manually as arbitrary method arguments everywhere

Rejected as the primary pattern because:

- easy to spoof or mix trusted/untrusted identifiers;
- no normalized actor model;
- inconsistent authorization context.

Stable identifiers may still appear explicitly in operation contracts where semantically required.

## Option C — Normalized trusted execution context

Accepted because:

- separates provider/delivery concerns;
- supports HTTP and background workflows;
- centralizes trusted tenant origin;
- improves testing;
- preserves domain boundaries.

---

# 12. Consequences

## Positive

- identity provider can change;
- domain modules are independent of ASP.NET transport;
- tenant origin is explicit;
- background workers fit the same model;
- authorization tests become easier;
- AI cannot choose identity/tenant.

## Negative

- requires mapping layer;
- permission model must be maintained;
- care is needed to avoid turning execution context into a large service locator.

---

# 13. Guardrails

The execution context must not become:

```text
HttpContext wrapper
global mutable state
service locator
raw claims bag
raw token container
```

It should remain small, immutable and security-focused.

---

# 14. Verification

The decision is successful when:

- no core module references `HttpContext`;
- provider claim names are absent from domain/application logic;
- cross-tenant tests reliably fail;
- background workflows execute with trusted tenant context;
- standard vs senior approval authority tests pass;
- AI tool requests cannot elevate permissions.

---

# 15. Revisit triggers

Revisit this ADR if:

- service-to-service identity becomes significantly more complex;
- delegated authorization requires standards such as token exchange;
- tenants require hierarchical/sub-tenant authorization;
- the project becomes explicitly tied to a specific enterprise identity platform.

The trusted normalized context principle should remain even if the provider changes.
