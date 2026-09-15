# ResolveAI .NET — Architecture Decision Backlog

**Status:** Accepted planning baseline v1.1  
**Date:** 2026-09-15

This file tracks decisions that materially affect the architecture. It is not a list of every implementation choice.

---

# Accepted ADRs

## ADR-001 — Modular monolith first

**State:** Accepted

ResolveAI starts as a modular monolith. Service extraction is allowed later only when evidence justifies it.

See `adr/ADR-001-MODULAR-MONOLITH-FIRST.md`.

---

## ADR-002 — Bounded contexts by business capability

**State:** Accepted

Core bounded contexts:

```text
Case Management
Orchestration
Decisioning
AI
Execution
Knowledge
Audit
```

External business systems remain integrations/bounded contexts outside the ResolveAI core.

See `06_DOMAIN_MODEL_AND_BOUNDARIES.md` and `adr/ADR-002-BOUNDED-CONTEXTS-BY-BUSINESS-CAPABILITY.md`.

---

## ADR-003 — Persistence ownership and PostgreSQL layout

**State:** Accepted

V0/V1 baseline:

```text
one PostgreSQL database
+
module-owned schemas
+
module-owned DbContexts
```

No direct cross-module DbContext access.

See `07_DATA_ARCHITECTURE_AND_PERSISTENCE.md` and `adr/ADR-003-MODULE-OWNED-POSTGRES-SCHEMAS-AND-DBCONTEXTS.md`.

---

## ADR-004 — Workflow evolution

**State:** Accepted

```text
V0
.NET + PostgreSQL persisted state machine

V1
Temporal durable workflows
```

Host/application entry points use orchestration contracts. Orchestration calls module contracts; CaseManagement does not reference Orchestration. Runtime details remain private.

See `08_WORKFLOW_ARCHITECTURE.md` and `adr/ADR-004-WORKFLOW-EVOLUTION-TO-TEMPORAL.md`.

---

## ADR-005 — Identity, tenancy and authorization boundary

**State:** Accepted

- normalize identity into trusted internal execution context;
- no `HttpContext` or provider-specific claims in core modules;
- tenant context is backend-trusted;
- Decisioning owns approval authority;
- Execution owns tool execution enforcement;
- AI is a constrained machine actor.

See `09_IDENTITY_TENANCY_AUTHORIZATION.md` and `adr/ADR-005-IDENTITY-TENANCY-AUTHORIZATION-BOUNDARY.md`.

---

# Accepted non-ADR implementation directions

These are documented but do not currently need separate ADRs.

## AI / Tool boundary

```text
ResolveAI.AI
    → Platform
    → Execution
    → Audit (durable proposal outcome delivery)
    → Knowledge (when RAG appears)
```

AI uses the Tool/Execution Gateway for approved capabilities. It does not call business integrations directly.

See `10_AI_AND_TOOL_BOUNDARIES.md` and `12_DOTNET_SOLUTION_STRUCTURE.md`.

## Technology baseline

V0 core:

```text
.NET 10
ASP.NET Core
EF Core
PostgreSQL
OpenAI provider adapter
xUnit
Testcontainers
OpenTelemetry
Docker Compose
```

See `11_TECHNOLOGY_STACK.md`.

---

# Explicit V1 verification commitments

V1 includes:

```text
provider replacement proof
fault injection
```

These are part of **Milestone 4 — Evals & Observability Hardening**.

Provider replacement proves adapter isolation; it does not require live multi-provider routing/fallback.

See `13_IMPLEMENTATION_ROADMAP.md`.

---

# Deferred decisions

Do not decide these until implementation creates a concrete need:

- exact production OIDC provider configuration;
- exact Temporal deployment topology;
- RabbitMQ/MassTransit introduction;
- Redis introduction;
- MCP exposure;
- Kubernetes;
- PostgreSQL RLS;
- second-provider live routing/fallback;
- module split into additional `.csproj` assemblies;
- exact architecture-test library;
- exact retry/timeout numeric values.

A deferred item should become an ADR only if the choice is architecturally significant.


# Baseline v1.1 implementation commitments

- Delivery 1 combines foundation and persistent case intake, two-tenant isolation, audit/tracing and CI.
- Canonical V0 release gate: roadmap section 10.
- Compile-time references: solution structure section 12; no CaseManagement/Orchestration cycle.
- Critical handoff recovery is required in V0. Choose minimal outbox or persisted-state reconciliation at the first affected delivery and document its crash tests.
- Payments simulation persists state and atomically protects remaining refundable amount across independent workflows.
- Audit begins with the first mutation; deterministic AI substitutes run in CI and bounded real-provider evals run separately.
- Concrete scenario rules remain to be specified in requirements section 9 before Delivery 4.
