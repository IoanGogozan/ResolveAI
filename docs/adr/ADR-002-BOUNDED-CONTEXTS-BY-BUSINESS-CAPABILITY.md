# ADR-002 — Decompose ResolveAI by Business Capability

**Status:** Accepted  
**Date:** 2026-09-15  
**Supersedes:** none  
**Related:** ADR-001 — Start ResolveAI as a Modular Monolith

---

# 1. Context

ResolveAI includes many concepts:

```text
SupportTicket
Customer
Order
Payment
Shipment
WorkflowRun
Policy
ApprovalRequest
AgentRun
ToolCall
KnowledgeDocument
AuditEvent
EvalCase
```

A naive architecture could create one module per entity or organize all code into generic technical layers such as `Services`, `Repositories` and `Data`.

Both approaches produce weak business boundaries.

The architecture needs module boundaries that:

- reflect ResolveAI's actual responsibilities;
- keep AI reasoning separate from enforcement;
- preserve clear state ownership;
- avoid turning the project into a full e-commerce implementation;
- support a future `.NET` solution structure;
- remain understandable and demonstrable in a portfolio.

---

# 2. Decision drivers

The main drivers are:

- one developer / portfolio-scale implementation;
- strong domain ownership;
- safe AI integration;
- deterministic policy enforcement;
- long-running workflows;
- idempotent side effects;
- external enterprise systems;
- multi-tenancy;
- testability;
- future extraction possibility without requiring microservices now.

---

# 3. Options considered

## Option A — Module per entity

Example:

```text
Tickets
Customers
Orders
Payments
Shipping
Policies
Approvals
Workflows
```

### Advantages

- visually simple;
- easy initial mapping from database entities.

### Disadvantages

- confuses entity boundaries with business capabilities;
- creates many small, highly coupled modules;
- encourages chatty cross-module calls;
- tends toward "microservices by noun";
- makes process ownership unclear.

### Decision

Rejected.

---

## Option B — Technical-layer modules

Example:

```text
Api
Services
Repositories
Data
AI
Common
```

### Advantages

- familiar;
- easy to start;
- fewer initial projects.

### Disadvantages

- business boundaries disappear;
- `Services` and `Common` grow indefinitely;
- rules for one capability become scattered across technical layers;
- difficult to reason about ownership;
- weak preparation for future extraction.

### Decision

Rejected.

---

## Option C — Bounded contexts by business capability

Initial business capabilities:

```text
Case Management
Orchestration
Decisioning
AI
Execution
Knowledge
Audit
```

Platform capabilities:

```text
Identity
Tenancy
Observability
```

External bounded contexts:

```text
CRM
Orders
Payments
Shipping
```

Engineering tooling:

```text
Evals
```

### Advantages

- responsibilities follow business purpose;
- ownership is clearer;
- reduces cross-module data access;
- keeps external enterprise domains outside the ResolveAI core;
- supports modular-monolith implementation;
- creates a credible path to future service extraction;
- aligns naturally with the safety model.

### Disadvantages

- requires more upfront domain thinking;
- boundaries may need adjustment as implementation reveals new information;
- some concepts could plausibly belong in more than one module;
- module contracts must be designed carefully.

### Decision

Accepted.

---

# 4. Decision

ResolveAI will be decomposed primarily by **business capability / bounded context**, not by entity and not by technical layer.

The initial core module set is:

1. **Case Management**
2. **Orchestration**
3. **Decisioning**
4. **AI**
5. **Execution**
6. **Knowledge**
7. **Audit**

The module set is intentionally logical first. It does not yet imply one `.csproj` per module.

---

# 5. Specific ownership decisions

## Case Management

Owns the support case and its lifecycle.

## Orchestration

Owns long-running process/workflow state.

## Decisioning

Owns deterministic policy decisions and human approval.

Policy and approval remain together initially because approval is primarily produced by policy outcomes.

## AI

Owns model interaction, AgentRun, structured proposals, prompt/model metadata and context construction.

## Execution

Owns tool/capability execution, risk enforcement at execution boundary, execution records and idempotency semantics.

## Knowledge

Owns knowledge documents, document versions, chunks, retrieval and evidence references.

## Audit

Owns append-oriented audit records.

---

# 6. External-domain decision

CRM, Orders, Payments and Shipping are **not** core ResolveAI modules.

They are modeled as external bounded contexts accessed through integration ports/adapters.

The portfolio repository may contain simulated implementations, but the ResolveAI core treats them as external systems.

This avoids accidental scope expansion into building a full commerce platform.

---

# 7. Platform capability decision

Identity, Tenancy and Observability are platform capabilities rather than primary business bounded contexts.

They provide trusted context/infrastructure to business modules.

They must not become generic locations for business logic.

---

# 8. Evals decision

Evals is engineering/quality tooling.

Production modules must not depend on the eval runner.

Allowed direction:

```text
Evals → ResolveAI
```

Forbidden direction:

```text
ResolveAI → Evals
```

---

# 9. Cross-module data access decision

Direct database access across module boundaries is forbidden.

Even if modules share one PostgreSQL database:

```text
Module A
```

must not use:

```text
Module B DbContext / Repository / tables
```

for business operations.

Communication must occur through explicit module contracts or events.

---

# 10. AI safety consequence

This decomposition encodes the principle:

```text
AI
   proposes
Decisioning
   authorizes / requires approval / blocks
Execution
   safely executes
Orchestration
   coordinates
```

No single AI component owns authorization and execution authority.

---

# 11. Consequences

## Positive

- clear ownership;
- easier architecture testing;
- stronger interview narrative;
- prevents domain logic from collapsing into generic services;
- reduces scope;
- allows external systems to be simulated independently;
- future deployment extraction remains possible.

## Negative

- more contracts/interfaces must be designed;
- discipline is needed to prevent direct database shortcuts;
- some synchronous calls may initially feel more verbose;
- developers must understand domain boundaries rather than only persistence entities.

---

# 12. Boundary uncertainty

The following boundaries are accepted provisionally but should be observed during implementation:

## AI ↔ Execution

Current position:

- AI may use safe/read capabilities exposed through the execution/tool boundary;
- AI may propose mutations;
- AI cannot execute high-risk mutations.

If implementation reveals excessive coupling, the contract will be refined without merging authority.

## AI ↔ Knowledge

Current position:

- Knowledge remains separate because it owns ingestion/retrieval/provenance;
- AI consumes retrieval as a capability.

## Decisioning ↔ Approval

Current position:

- kept together;
- separation is reconsidered only if approval becomes a broader enterprise workflow capability beyond policy-driven actions.

---

# 13. Verification

This decision is considered successful when:

- each mutable concept has one owner;
- architecture tests can prevent selected forbidden dependencies;
- external provider DTOs do not leak into core modules;
- AI cannot bypass Decisioning/Execution;
- no core module reads another module's tables directly;
- V0/V1 can be implemented without circular module dependencies;
- the eventual `.NET` solution structure follows the domain boundaries rather than inventing new ones.

---

# 14. Revisit triggers

Revisit the module decomposition if:

- implementation repeatedly requires unnatural cross-module calls;
- ownership of an important concept cannot be stated clearly;
- two modules always change together and expose no meaningful independent capability;
- one module accumulates unrelated responsibilities;
- a module requires independent scaling/security/deployment;
- the business scenario materially expands.

Changes require a new ADR or an update to this ADR with explicit rationale.


---

## Baseline v1.1 clarification — 2026-09-15

CaseManagement does not reference Orchestration. Orchestration consumes module contracts; Host wires entry points. Integration ports belong to consuming modules, with adapters in Integrations. Document 12 section 12 is the canonical compile-time graph.
