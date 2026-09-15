# ResolveAI .NET — Planning Pack Index

**Status:** Accepted planning baseline v1.1  
**Date:** 2026-09-15

This package is the clean, synchronized architecture and implementation baseline for ResolveAI.

The planning phase is considered complete enough to start **Delivery 1 — Foundation & Case Intake**. Further documents should be added only when implementation creates a concrete decision need.

---

# 1. Core documents

| Document | Source of truth for |
|---|---|
| `01_PROJECT_CHARTER.md` | Project purpose, portfolio objective and product framing |
| `02_SCOPE_AND_REQUIREMENTS.md` | V0/V1 scope, functional requirements and Definition of Done |
| `03_ARCHITECTURAL_DRIVERS.md` | Quality attributes and architecture drivers |
| `04_ARCHITECTURE_PRINCIPLES.md` | System-wide architectural principles and invariants |
| `05_DECISION_BACKLOG.md` | Current ADR status and intentionally deferred decisions |
| `06_DOMAIN_MODEL_AND_BOUNDARIES.md` | Bounded contexts, responsibilities and ownership |
| `07_DATA_ARCHITECTURE_AND_PERSISTENCE.md` | PostgreSQL layout, persistence ownership and consistency |
| `08_WORKFLOW_ARCHITECTURE.md` | Workflow model and V0 → V1 durable orchestration strategy |
| `09_IDENTITY_TENANCY_AUTHORIZATION.md` | Authentication, tenant context and authorization boundaries |
| `10_AI_AND_TOOL_BOUNDARIES.md` | LLM Gateway and Tool/Execution Gateway |
| `11_TECHNOLOGY_STACK.md` | V0/V1 technology choices |
| `12_DOTNET_SOLUTION_STRUCTURE.md` | `.sln`, `.csproj`, dependency graph and repository layout |
| `13_IMPLEMENTATION_ROADMAP.md` | Delivery order, canonical V0 release gate and V1 milestones |

---

# 2. Architecture Decision Records

| ADR | Status | Decision |
|---|---|---|
| `adr/ADR-001-MODULAR-MONOLITH-FIRST.md` | Accepted | Start as a modular monolith |
| `adr/ADR-002-BOUNDED-CONTEXTS-BY-BUSINESS-CAPABILITY.md` | Accepted | Decompose by business capability |
| `adr/ADR-003-MODULE-OWNED-POSTGRES-SCHEMAS-AND-DBCONTEXTS.md` | Accepted | One PostgreSQL database with module-owned schemas/DbContexts |
| `adr/ADR-004-WORKFLOW-EVOLUTION-TO-TEMPORAL.md` | Accepted | V0 persisted state machine → V1 Temporal |
| `adr/ADR-005-IDENTITY-TENANCY-AUTHORIZATION-BOUNDARY.md` | Accepted | Normalize trusted identity/tenant context behind a platform boundary |

---

# 3. Canonical dependency clarification

The AI module uses the Tool/Execution Gateway rather than calling external business systems directly.

```text
ResolveAI.AI
    → ResolveAI.Platform
    → ResolveAI.Execution
    → ResolveAI.Knowledge (V1, when RAG is introduced)
```

`ResolveAI.Execution` remains the authority for tool execution, safety checks and idempotency.

---

# 4. Delivery baseline

## V0

Build the complete walking skeleton:

```text
case
→ business data
→ structured AI proposal
→ deterministic decision
→ approval
→ idempotent execution
→ durable workflow
→ audit/observability
```

## V1

Harden the system with:

```text
Knowledge/RAG
OIDC identity
Temporal
100+ evals
provider replacement proof
fault injection
production-like observability
minimal portfolio UI
```

Optional technologies remain optional until justified.

---

# 5. Planning status

No further large architecture-planning document is required before implementation.

Next action:

```text
Delivery 1 — Foundation & Case Intake
```

## Baseline v1.1 alignment

- `13_IMPLEMENTATION_ROADMAP.md`, section 10, is the canonical V0 release gate; the requirements document maps to that gate.
- `12_DOTNET_SOLUTION_STRUCTURE.md`, section 12, owns the compile-time dependency graph. Runtime flow diagrams do not authorize project references.
- Durability, audit and tracing are delivered with the first relevant behavior, then verified end-to-end.
- Critical cross-module handoffs require a persisted recovery mechanism before their delivery is complete.
- Refund safety covers retries and concurrent independent workflows for the same payment.
- Temporal is the accepted V1 target; MCP remains optional V1+.
- Business-policy examples are illustrative until the scenario rules are specified in the requirements document.
- Accepted planning status does not mean implementation checklists have passed.
