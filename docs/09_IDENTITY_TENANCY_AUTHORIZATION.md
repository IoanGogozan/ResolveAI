# ResolveAI .NET — Identity, Tenancy & Authorization Architecture

**Status:** Accepted planning baseline v1.1  
**Version:** 1.1  
**Related ADR:** ADR-005  
**Scope:** platform security boundary

---

# 1. Purpose

This document defines the security context that all ResolveAI modules rely on.

It separates six concerns that must not be collapsed into one mechanism:

```text
Authentication
Authorization
Tenant Context
Business Policy
Approval Authority
Tool Permission
```

The central rule is:

> **Identity establishes who the caller is. Authorization establishes what the caller may do. Decisioning establishes whether the business action is allowed.**

These are related but distinct concerns.

---

# 2. Security responsibility map

```text
Authentication
    ↓
Who is the caller?

Tenant Context
    ↓
Which tenant is the caller operating within?

Authorization
    ↓
Which application capabilities may the caller invoke?

Decisioning / Business Policy
    ↓
Is this specific business action allowed?

Approval Authority
    ↓
Is this actor allowed to approve this specific action?

Execution / Tool Permission
    ↓
Can this capability be executed now with this trusted context?
```

No single layer replaces the others.

---

# 3. Authentication

Authentication proves the identity of a human or service principal.

Typical actors:

```text
Customer
Operator
Senior Operator
Administrator
Internal Worker / Service Principal
```

Authentication is a platform concern.

It is not owned by:

- Case Management;
- Decisioning;
- AI;
- Execution.

---

# 4. Authentication direction

ResolveAI should use standards-based authentication.

Target architecture:

```text
OIDC / OAuth 2.0 compatible Identity Provider
        ↓
ASP.NET Core authentication
        ↓
trusted principal
```

Keycloak is the preferred reproducible V1 portfolio implementation; exact production configuration remains deferred.

Possible implementations may include:

- local development/test identity;
- Keycloak;
- Auth0;
- Microsoft Entra ID;
- another OIDC-compatible provider.

The domain architecture must not depend on a specific provider.

---

# 5. No provider-specific claims in domain modules

Provider claim names must not leak through the business codebase.

Avoid:

```text
"tid"
"oid"
"http://schemas.microsoft.com/..."
custom provider-specific claim keys
```

inside Decisioning, Execution or Case Management.

Instead, the web/security adapter translates the authenticated principal into a trusted internal execution context.

---

# 6. Trusted execution context

Define an internal platform abstraction conceptually similar to:

```text
IExecutionContext
- ActorId
- ActorType
- TenantId
- Roles
- Permissions
- AuthenticationMethod?
- CorrelationId
```

The exact C# shape is decided later.

Important:

> Domain/application code consumes trusted normalized context, not `HttpContext`.

---

# 7. HttpContext boundary

`HttpContext` belongs to the ASP.NET Core delivery layer.

Forbidden:

```text
DecisioningService(HttpContext)
ExecutionService(IHttpContextAccessor)
Workflow domain logic reading ClaimsPrincipal directly
```

Preferred:

```text
ASP.NET Core endpoint
    ↓
security adapter
    ↓
IExecutionContext
    ↓
application/module contract
```

Background workers and Temporal activities can create equivalent trusted contexts without requiring HTTP.

---

# 8. Tenant context

Tenant context determines the tenant boundary for the operation.

It must come from trusted authentication/session/application context.

It must never come authoritatively from:

- LLM output;
- tool arguments;
- user-provided JSON body;
- RAG content;
- external system payload.

---

# 9. Tenant resolution

For a human request:

```text
authenticated principal
    ↓
tenant membership / allowed tenant
    ↓
resolved TenantId
    ↓
IExecutionContext
```

For an internal workflow:

```text
persisted trusted workflow TenantId
    ↓
internal execution context
```

For an AI tool request:

```text
LLM proposes tool args
    ↓
Execution receives trusted TenantId separately
```

The model does not choose tenant scope.

---

# 10. Tenant switching

If one user can operate in multiple tenants, tenant selection must be explicit and validated.

Conceptually:

```text
Authenticated user
    ↓
requested tenant
    ↓
membership/permission validation
    ↓
trusted active TenantId
```

Do not trust a route/header/body tenant identifier without validating membership.

---

# 11. Authorization

Authorization determines whether the actor may invoke an application capability.

Examples:

```text
cases.read
cases.create
cases.manage

approvals.read
approvals.approve
approvals.approve_high_value

tools.read
tools.execute_low_risk
tools.execute_high_risk_request

admin.manage_tenant
```

Permissions should represent capabilities, not UI screens.

---

# 12. Roles vs permissions

Roles are convenient groupings.

Example:

```text
SupportOperator
    ├── cases.read
    ├── cases.manage
    ├── approvals.read
    └── approvals.approve

SeniorSupportOperator
    └── approvals.approve_high_value
```

Business code should prefer permission/capability checks where practical.

Reason:

- roles evolve;
- permissions express intent more precisely;
- external identity-provider role names should not control business code structure.

---

# 13. Authorization boundary

ASP.NET Core endpoint authorization is useful but not sufficient.

Example:

```text
POST /approvals/{id}/approve
```

may require:

```text
approvals.approve
```

at the endpoint.

But Decisioning must still verify:

- same tenant;
- approval is pending;
- actor has required approval authority;
- action fingerprint matches;
- approval has not expired.

Endpoint authorization cannot replace business invariants.

---

# 14. Business policy is not authorization

Example:

```text
Operator has permission:
approvals.approve
```

does not mean:

```text
Operator may approve every refund.
```

Decisioning may determine:

```text
refund < 500
→ normal approval

refund >= 500
→ senior approval
```

Therefore:

```text
Authorization:
Can this actor attempt approval?

Decisioning:
Is this actor sufficient for this specific approval?
```

---

# 15. Approval authority

Decisioning owns approval authority semantics.

Conceptual approval requirement:

```text
ApprovalRequirement
- Level = Standard | Senior
- RequiredPermission
- PolicyDecisionId
- ActionFingerprint
```

When approval is submitted, Decisioning validates:

```text
actor
tenant
approval status
required authority
action binding
expiration
```

---

# 16. AI actor model

The AI agent is not treated as a human super-user.

It has a constrained machine actor identity.

Conceptually:

```text
ActorType = Agent
ActorId = support-agent-v1
TenantId = trusted workflow tenant
Permissions = explicitly granted tool-request capabilities
```

The AI never receives blanket administrator authority.

---

# 17. Agent permissions

Agent permissions should be narrower than backend permissions.

Examples:

```text
customer.read
order.read
payment.read
knowledge.search

payments.refund.request
```

Important distinction:

```text
payments.refund.request
≠
payments.refund.execute
```

The model may request/propose a refund.

Execution authority remains backend-controlled.

---

# 18. Tool permission model

Every tool/capability declares required permission metadata.

Example:

```text
get_order
risk: LOW
permission: order.read

refund_payment
risk: HIGH
permission: payments.refund.execute
```

Execution evaluates:

```text
trusted actor permissions
+
tenant scope
+
tool risk
+
policy decision
+
approval result
```

before side effect execution.

---

# 19. AI cannot elevate its own permission

Tool arguments cannot contain authoritative permission claims.

Forbidden:

```json
{
  "tool": "refund_payment",
  "arguments": {
    "payment_id": "p1",
    "permission": "admin"
  }
}
```

Such fields are ignored/rejected.

Permissions come only from trusted backend configuration/context.

---

# 20. Service-to-service / worker identity

Internal workers may execute workflow activities without a human HTTP request.

They require a trusted internal actor context.

Example:

```text
ActorType = System
ActorId = orchestration-worker
TenantId = workflow.TenantId
```

Internal service identity does not automatically grant business authority.

It executes only the operation previously authorized by the process/policy/approval chain.

---

# 21. Delegated execution

When an approved action is executed later, preserve both:

```text
Requester
Approver
Executor
```

Example:

```text
Requester = AI agent
Approver = senior operator
Executor = orchestration worker
```

Audit should be able to distinguish them.

This is more accurate than attributing the refund only to the worker process.

---

# 22. Actor types

Initial conceptual set:

```text
Customer
Operator
Administrator
Agent
System
```

These types describe provenance.

They are not a complete authorization model.

---

# 23. Permission checks and module boundaries

## Case Management

May require:

```text
cases.read
cases.create
cases.manage
```

## Decisioning

May require:

```text
approvals.read
approvals.approve
approvals.approve_high_value
policies.manage
```

## Execution

May require:

```text
tool capability permissions
```

## Knowledge

Administrative mutation may require:

```text
knowledge.manage
```

Search/retrieval is tenant-scoped and may be invoked through AI under constrained capability.

## Audit

Read access should be more restrictive than normal case access.

---

# 24. Administrative operations

Administrative permissions should remain distinct.

Examples:

```text
tenants.manage
users.manage
policies.manage
knowledge.manage
audit.read
system.failure_injection
```

Failure injection must never be available to ordinary production actors.

For portfolio/demo environments, it should be strongly gated.

---

# 25. Tenant-aware resource validation

Authorization is incomplete without resource ownership validation.

Example:

Actor has:

```text
orders.read
```

but asks for:

```text
Tenant B Order
```

while operating in:

```text
Tenant A
```

Expected:

```text
deny
```

Permission does not override tenant boundary.

---

# 26. Trusted resource access pattern

Conceptually:

```text
ExecutionContext.TenantId
        ↓
module query
        ↓
WHERE tenant_id = trusted tenant
AND resource_id = requested resource
```

Do not first load by resource id and then casually check tenant later if this can expose side-channel/data mistakes.

Tenant scoping should be built into data-access patterns.

---

# 27. Authorization failures

Authorization failure is a non-retryable workflow failure unless trusted authorization state changes through a separate explicit operation.

Do not retry:

```text
403-style permission failure
```

with exponential backoff.

---

# 28. Security event audit

Security-relevant actions should emit audit facts.

Examples:

```text
authorization denied
cross-tenant access attempt
approval granted
approval rejected
high-risk tool request
high-risk tool execution
admin policy change
tenant membership change
```

Not every normal read requires a heavy immutable audit event; this will be balanced later.

---

# 29. Authentication vs audit identity

Audit should persist normalized internal actor identity plus relevant external subject reference if useful.

Do not make future audit reconstruction depend entirely on an external IdP retaining old display names/roles forever.

---

# 30. Token handling

Access tokens are transport credentials.

Rules:

- do not pass raw access tokens into LLM prompts;
- do not store tokens in domain entities;
- do not log tokens;
- do not persist them in audit events;
- adapters/services receive credentials through secure infrastructure mechanisms.

---

# 31. Secrets

API keys, provider credentials and database secrets are infrastructure configuration.

They must not appear in:

- model context;
- tool arguments visible to the model;
- audit payloads;
- source control;
- normal application logs.

Secret-management product choice is deferred.

---

# 32. Development identity

V0 needs low-friction local development.

Recommended direction:

```text
Development/Test Authentication Handler
```

capable of creating known principals/tenants/permissions.

Requirements:

- only available in Development/Test environments;
- impossible to enable accidentally in production-like configuration;
- supports automated integration tests.

This avoids blocking V0 on external IdP infrastructure.

---

# 33. V1 identity provider

For a production-like V1 demo, use an OIDC-compatible provider.

Selection criteria:

- .NET integration;
- local reproducibility;
- tenant/role claims;
- portfolio relevance;
- operational overhead.

Keycloak is the preferred portfolio implementation in the technology stack; provider-specific configuration is selected during V1 identity delivery.

No domain code may need to change when the provider changes.

---

# 34. ASP.NET Core authorization direction

Likely implementation direction:

```text
authentication schemes
+
policy-based authorization
+
custom authorization requirements/handlers where needed
```

However, ASP.NET authorization policies enforce application permissions, not Decisioning business policy.

Naming must keep this distinction clear.

---

# 35. Naming rule

Avoid using the generic word `Policy` for both:

```text
ASP.NET AuthorizationPolicy
```

and:

```text
ResolveAI business Policy
```

within the same layer without qualification.

Preferred terminology:

```text
Authorization Policy / Requirement
Business Policy / PolicyDecision
```

This prevents conceptual confusion.

---

# 36. Background/Temporal security context

When Temporal is introduced, do not serialize full `ClaimsPrincipal` into workflow state.

Persist/transport only stable trusted identifiers and required context:

```text
TenantId
RequesterActorId
RequesterActorType
correlation identifiers
approved action references
```

Activities reconstruct required platform context through trusted application mechanisms.

---

# 37. Long-running workflow identity changes

A workflow may run for hours/days.

The user's current permissions may change after the workflow starts.

Rules differ by operation:

## Read/investigation continuation

May execute under trusted system actor according to workflow rules.

## High-risk side effect

Must depend on persisted valid policy/approval state and execution authorization rules.

Do not assume the original requester's old access token remains valid or available.

---

# 38. Approval revocation / expiration

Approval is business authorization evidence, not a forever capability.

Decisioning may invalidate it when:

- expired;
- action changed;
- policy changed materially;
- approval revoked;
- target resource state changed.

Execution must verify approval validity as required immediately before the protected action.

---

# 39. TOCTOU consideration

There may be time between:

```text
policy evaluation
approval
execution
```

Resource state may change.

Example:

```text
payment already refunded by another process
```

Therefore Execution must revalidate execution-critical invariants at execution time.

Approval does not freeze the external world.

---

# 40. Authorization and idempotency

Repeated execution of the same idempotent action should not require re-performing a business side effect.

However, a caller must still be authorized to request/read the existing execution result as appropriate.

Idempotency is not an authorization bypass.

---

# 41. Cross-tenant tests

Required tests include:

```text
Tenant A user → Tenant B case → denied
Tenant A agent → Tenant B order tool → denied
Tenant A workflow → Tenant B payment → denied
Tenant A knowledge retrieval → Tenant B document → no result/denied
```

Expected unauthorized cross-tenant side effects:

```text
0
```

---

# 42. Approval authority tests

Examples:

```text
standard operator approves low/medium threshold action → allowed
standard operator approves senior-required action → denied
senior operator approves senior-required action → allowed
approval from another tenant → denied
expired approval → denied
approval fingerprint mismatch → denied
```

---

# 43. Tool authorization tests

Examples:

```text
agent with order.read → get_order succeeds
agent without payment.read → get_payment denied
agent with payments.refund.request → may propose/request refund
agent cannot directly gain payments.refund.execute
high-risk execution without valid approval → denied
```

---

# 44. AI security principle

The model must be considered potentially incorrect or manipulated.

Even if the model says:

```text
"I am an administrator"
```

or retrieves a document saying:

```text
"ignore approval checks"
```

trusted backend identity and authorization remain unchanged.

---

# 45. Module dependency consequence

Business modules may depend on a small normalized security-context abstraction.

They must not depend directly on:

```text
ASP.NET Core HttpContext
OIDC SDK
Keycloak SDK
Auth0 SDK
Entra-specific SDK
JWT parsing code
```

Those belong to infrastructure/host/platform adapters.

---

# 46. Security context data minimization

Pass only required context to module operations.

Avoid a giant mutable `UserContext` containing:

- raw token;
- every claim;
- headers;
- request body;
- session object.

Preferred:

```text
ActorId
ActorType
TenantId
Permissions
CorrelationId
```

plus operation-specific data.

---

# 47. Decisions confirmed

- authentication uses standards-based identity boundaries;
- provider-specific claims do not leak into domain modules;
- modules consume normalized trusted execution context;
- `HttpContext` stays at delivery/platform boundary;
- tenant context is trusted and model-independent;
- permissions express application capabilities;
- roles group permissions but do not replace business rules;
- Decisioning owns approval authority;
- Execution owns tool execution enforcement;
- AI permissions are explicitly constrained;
- `payments.refund.request` and `payments.refund.execute` are distinct concepts;
- system/worker actor is distinguished from requester/approver;
- development auth may use a test handler;
- V1 targets an OIDC-compatible identity provider;
- cross-tenant tests are mandatory.

---

# 48. Decisions deferred

Not decided yet:

- exact production OIDC provider/configuration;
- exact token/claim mapping;
- exact permission storage model;
- whether permissions are sourced entirely from IdP or enriched locally;
- exact admin/user management model;
- PostgreSQL RLS adoption;
- secret-management product;
- session/token lifetime strategy;
- customer authentication details if public customer UI is implemented.

---

# 49. Acceptance criteria

This security-context architecture is ready to drive .NET solution design when:

- [ ] trusted tenant origin is explicit;
- [ ] HttpContext is excluded from domain/application boundaries;
- [ ] authentication and authorization are conceptually separated;
- [ ] business policy is distinct from ASP.NET authorization;
- [ ] approval authority has an owner;
- [ ] agent permissions are constrained;
- [ ] tool permission semantics are defined;
- [ ] background-worker identity is defined;
- [ ] delegated execution can identify requester/approver/executor;
- [ ] cross-tenant security tests are specified.
