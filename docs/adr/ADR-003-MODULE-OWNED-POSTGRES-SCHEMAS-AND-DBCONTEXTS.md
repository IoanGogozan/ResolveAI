# ADR-003 — Single PostgreSQL Database with Module-Owned Schemas and DbContexts

**Status:** Accepted  
**Date:** 2026-09-15  
**Related:** ADR-001, ADR-002

---

# 1. Context

ResolveAI is a modular monolith with seven initial core bounded contexts:

```text
Case Management
Orchestration
Decisioning
AI
Execution
Knowledge
Audit
```

The architecture needs strong persistence ownership without introducing database-per-service operational complexity.

A persistence strategy must support:

- explicit module ownership;
- PostgreSQL;
- EF Core;
- multi-tenancy;
- local transactions;
- idempotency;
- workflow durability;
- pgvector for Knowledge;
- future module extraction if justified.

---

# 2. Options considered

## Option A — One database, one public schema, one global DbContext

### Advantages

- simplest setup;
- easiest joins;
- few migrations.

### Disadvantages

- weak module boundaries;
- easy cross-module repository/table access;
- one global object model;
- difficult future extraction;
- high risk of a big-ball-of-mud persistence layer.

### Decision

Rejected.

---

## Option B — One PostgreSQL database, schema-per-module, DbContext-per-module

### Advantages

- clear ownership;
- low operational complexity;
- strong EF Core boundaries;
- module-specific migrations;
- easy local development;
- supports pgvector;
- future extraction remains possible.

### Disadvantages

- more contexts/migrations;
- cross-module queries require composition;
- developers must resist direct schema joins;
- deployment migration tooling needs ordering.

### Decision

Accepted.

---

## Option C — Database-per-module from day one

### Advantages

- strongest physical isolation;
- independent DB lifecycle;
- natural preparation for microservices.

### Disadvantages

- unnecessary operational complexity;
- more containers/connections/backups;
- harder local development;
- harder integration testing;
- no current scaling/team/isolation driver.

### Decision

Rejected for current scope.

---

# 3. Decision

ResolveAI V0/V1 will use:

```text
one PostgreSQL database
+
one schema per persistent core module
+
one logical EF Core DbContext per persistent core module
```

Initial schemas:

```text
case_management
orchestration
decisioning
ai
execution
knowledge
audit
```

---

# 4. Cross-module data rule

Modules must not directly use another module's DbContext/repository/tables for operational behavior.

No default cross-module relational foreign keys will be created.

Cross-module relationships use stable identifiers and module contracts.

---

# 5. Transaction rule

ACID transactions are local to one owning module.

Cross-module workflows are coordinated explicitly.

The architecture will not rely on the fact that all modules happen to share one database to create hidden multi-module transactional coupling.

---

# 6. Tenant rule

Tenant-owned records are tenant-scoped using trusted backend context.

Application-level tenant enforcement is required.

PostgreSQL RLS remains an optional defense-in-depth feature to evaluate later.

---

# 7. Idempotency rule

Execution owns persisted idempotency state.

Critical mutation keys must be tenant-scoped and protected by database uniqueness/concurrency guarantees.

A request fingerprint must detect reuse of a key with materially different arguments.

---

# 8. Knowledge/vector rule

PostgreSQL + pgvector remains the initial knowledge-store direction.

The vector extension is database infrastructure.

Knowledge tables and vector queries remain owned by the Knowledge module.

---

# 9. Audit rule

Audit data lives in a dedicated module schema and is append-oriented.

The application will not expose normal update/delete behavior for audit events.

Additional database hardening may be added later.

---

# 10. Migration rule

Each module owns its EF Core migrations and migration history.

Deployment tooling is responsible for deterministic application of migrations.

---

# 11. Consequences

## Positive

- bounded contexts are reflected in persistence;
- local development remains simple;
- PostgreSQL remains the only required operational database;
- EF Core ownership is explicit;
- future extraction is easier;
- cross-module coupling becomes visible.

## Negative

- more migration sets;
- read composition is sometimes more verbose;
- no convenient cross-module navigation properties;
- architecture discipline is required to prevent raw SQL shortcuts.

---

# 12. Verification

The decision is successful if:

- architecture tests/code review can prevent cross-module persistence access;
- migrations can be applied independently per module;
- cross-tenant integration tests pass;
- idempotency concurrency tests pass;
- workflow state survives restart;
- no core module requires direct SQL access to another module's schema.

---

# 13. Revisit triggers

Revisit physical database topology if:

- a module requires independent scaling;
- strict security/compliance isolation appears;
- an extracted service needs independent persistence;
- database contention becomes measurable;
- independent deployment becomes a real requirement.

Do not move to database-per-module merely for diagram purity.


---

## Baseline v1.1 clarification — 2026-09-15

Critical V0 handoffs require minimal outbox or explicit persisted-state reconciliation from first use, including audit publication. Workflow-scoped idempotency must be complemented by atomic payment-level remaining-amount enforcement across independent workflows. Simulated Payments state survives restart.
