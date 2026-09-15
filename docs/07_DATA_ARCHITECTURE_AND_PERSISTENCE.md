# ResolveAI .NET — Data Architecture & Persistence Ownership

**Status:** Accepted planning baseline v1.1  
**Version:** 1.1  
**Depends on:** ADR-001, ADR-002  
**Primary database direction:** PostgreSQL

---

# 1. Purpose

This document translates the previously accepted bounded contexts into a concrete persistence model.

It defines:

- database topology;
- module data ownership;
- EF Core `DbContext` boundaries;
- PostgreSQL schema ownership;
- transaction boundaries;
- cross-module references;
- migrations;
- tenant scoping;
- idempotency storage;
- audit persistence;
- knowledge/vector storage;
- integration-event durability.

It deliberately does **not** define the final `.sln` or `.csproj` structure.

The main persistence principle is:

> **A shared physical database does not imply shared data ownership.**

---

# 2. Database topology decision

## 2.1 Initial topology

ResolveAI V0/V1 will use:

```text
one PostgreSQL instance
        ↓
one primary ResolveAI database
        ↓
multiple module-owned schemas
```

Conceptually:

```text
resolveai
├── case_management
├── orchestration
├── decisioning
├── ai
├── execution
├── knowledge
└── audit
```

Optional platform/support schemas may be added later if justified.

This is a **single-database modular monolith**, not a database-per-service architecture.

---

# 3. Why one database initially

The project currently needs:

- strong module ownership;
- local ACID transactions inside a module;
- simple local development;
- low operational overhead;
- easy integration testing;
- PostgreSQL + pgvector support;
- credible future extraction boundaries.

It does **not** currently need:

- independent database scaling per domain;
- independent database deployment;
- strict physical isolation between multiple engineering teams;
- multi-region data placement.

Therefore one PostgreSQL database is the simplest adequate choice.

---

# 4. Why schema-per-module

Each core module receives a dedicated PostgreSQL schema.

Example:

```text
case_management.support_cases
orchestration.workflow_runs
decisioning.policy_decisions
decisioning.approval_requests
ai.agent_runs
ai.model_invocations
execution.tool_executions
execution.idempotency_records
knowledge.documents
knowledge.chunks
audit.audit_events
```

Benefits:

- makes ownership visible in the database;
- reduces accidental table access;
- supports module-specific migrations;
- helps architecture reviews;
- provides a clean path toward later extraction;
- avoids a single undifferentiated `public` schema.

The schema boundary supports the module boundary; it does not replace application-level architecture rules.

---

# 5. EF Core persistence boundary

## 5.1 Direction

Use **one logical EF Core `DbContext` per module that owns persistent state**.

Conceptually:

```text
CaseManagementDbContext
OrchestrationDbContext
DecisioningDbContext
AiDbContext
ExecutionDbContext
KnowledgeDbContext
AuditDbContext
```

This decision does not yet dictate whether each context lives in a separate `.csproj`.

---

# 6. Why not one giant DbContext

A single application-wide `ResolveAiDbContext` would make it easy to write:

```csharp
context.SupportCases
context.ApprovalRequests
context.AgentRuns
context.IdempotencyRecords
context.KnowledgeChunks
```

from anywhere in the application.

That would undermine the bounded contexts even if the source code folders look modular.

Separate contexts make illegal access more difficult and make ownership explicit.

---

# 7. DbContext rule

A module may access only its own `DbContext`.

Forbidden conceptually:

```text
AI module
    ↓
ExecutionDbContext
```

or:

```text
Orchestration module
    ↓
DecisioningDbContext
```

Allowed:

```text
Orchestration
    ↓
IDecisioningModule
    ↓
Decisioning
    ↓
DecisioningDbContext
```

---

# 8. Cross-module database relationships

## 8.1 Default rule

Do **not** create relational foreign keys across module-owned schemas.

Example:

`orchestration.workflow_runs.case_id` may contain a `CaseId`, but there should not automatically be:

```text
FOREIGN KEY orchestration.workflow_runs.case_id
→ case_management.support_cases.id
```

Why:

- a foreign key creates a physical coupling between module stores;
- it encourages cross-schema joins;
- future extraction becomes harder;
- lifecycle ownership becomes ambiguous.

---

# 9. References across modules

Use stable identifiers.

Examples:

```text
WorkflowRun
- WorkflowId
- CaseId

ApprovalRequest
- ApprovalId
- WorkflowId
- ProposedActionId

ToolExecution
- ExecutionId
- WorkflowId
- ApprovalId?
```

The owning module validates external/module references through public contracts when necessary.

---

# 10. Cross-module joins

Direct cross-module SQL joins are forbidden for business behavior.

Forbidden:

```sql
SELECT *
FROM orchestration.workflow_runs w
JOIN decisioning.approval_requests a
  ON a.workflow_id = w.id;
```

inside operational module code.

Instead:

```text
Orchestration query
    +
Decisioning query
    ↓
application/read composition
```

For operator dashboards, a dedicated read model may later be introduced if composition becomes inefficient.

That read model would be explicitly derived, not treated as a new source of truth.

---

# 11. Transaction boundaries

## 11.1 Primary rule

An ACID transaction belongs to one owning module.

Examples:

```text
Decisioning:
create PolicyDecision + ApprovalRequest
→ one local transaction
```

```text
Execution:
create execution attempt + idempotency state
→ one local transaction
```

---

# 12. Cross-module transaction rule

Do not design business workflows around one database transaction spanning several module contexts.

Even though the modules currently share PostgreSQL, the architecture should assume:

```text
module operation A
        succeeds
module operation B
        can fail
```

Orchestration, retries, idempotency and recovery handle this reality.

This prepares the system for real external dependencies, which cannot participate in the same local database transaction anyway.

---

# 13. Example: refund workflow persistence

A refund process may persist data across several ownership boundaries:

```text
Case Management
    SupportCase

Orchestration
    WorkflowRun

AI
    AgentRun
    StructuredProposal metadata

Decisioning
    PolicyDecision
    ApprovalRequest

Execution
    ToolExecution
    IdempotencyRecord

Audit
    AuditEvents
```

These records are related through stable identifiers and correlation metadata, not through one giant aggregate.

---

# 14. Aggregate boundary guidance

Within a module, normal aggregate rules apply.

Example:

```text
Decisioning aggregate:
ApprovalRequest
    └── ApprovalDecision / state transition
```

Possible invariant:

```text
only PENDING approval can transition to APPROVED/REJECTED
```

Another example:

```text
Execution aggregate:
IdempotencyRecord
    └── execution status/result fingerprint
```

Possible invariant:

```text
same idempotency key + same operation fingerprint
→ same business result
```

---

# 15. Tenant scoping

## 15.1 Core rule

All tenant-owned operational records must include trusted tenant identity where relevant.

Examples:

```text
tenant_id
case_id
workflow_id
...
```

Tenant identity originates from backend execution context.

It must never be derived from model output.

---

# 16. Tenant-aware primary/unique constraints

Where appropriate, business uniqueness should include tenant scope.

Conceptual examples:

```text
UNIQUE(tenant_id, external_case_reference)

UNIQUE(tenant_id, idempotency_key)

UNIQUE(tenant_id, policy_name, policy_version)
```

The exact indexes are decided per aggregate.

---

# 17. Application-level tenant enforcement

Initial required layer:

- tenant-aware query filters/repositories;
- explicit tenant in module contracts where necessary;
- authorization before resource access;
- tests for cross-tenant access.

The architecture must not rely only on developer convention.

---

# 18. PostgreSQL Row-Level Security

RLS is considered **defense in depth**, not the primary authorization model.

Direction:

- not required for V0;
- evaluate for V1 after the module persistence model is stable;
- if enabled, tenant context must be safely propagated to the DB session/transaction;
- automated tests must prove both application filtering and RLS behavior.

RLS must not be used to compensate for unclear data ownership.

---

# 19. Identifier strategy

Use globally unique, opaque identifiers for core records.

Preferred conceptual choices:

```text
Guid / UUID
```

Potentially UUIDv7 when supported consistently by the chosen implementation/database strategy.

Do not encode tenant, type, or business meaning inside identifiers unless there is a specific need.

Strongly typed IDs may be introduced at module boundaries if they improve correctness without excessive ceremony.

---

# 20. External-system identifiers

External references should be represented explicitly.

Examples:

```text
CustomerExternalId
OrderExternalId
PaymentExternalId
ShipmentExternalId
```

Do not pretend external identifiers are ResolveAI-owned entities.

If external systems use strings, preserve their semantics behind integration contracts.

---

# 21. Operational snapshots

Some facts must be retained as point-in-time evidence.

Example:

```text
RefundDecisionSnapshot
- tenant_id
- customer_reference
- order_reference
- payment_reference
- amount
- currency
- delivery_state
- policy_version
- evidence_references
- captured_at
```

A snapshot is immutable evidence.

It does not become a synchronized copy of CRM/Orders/Payments.

---

# 22. Idempotency storage

Idempotency is owned by **Execution**.

Suggested persisted shape:

```text
IdempotencyRecord
- Id
- TenantId
- IdempotencyKey
- OperationType
- TargetResourceId
- RequestFingerprint
- Status
- ResultReference / ResultPayload
- CreatedAt
- CompletedAt
- LastAttemptAt
```

---

# 23. Idempotency key

Initial conceptual key:

```text
workflow_id + action_type + target_resource_id
```

In storage, tenant scope must also be enforced.

Example:

```text
tenant_acme:wf_1001:refund:payment_9384
```

The exact serialized format is implementation detail. This key deduplicates a workflow action; it does not stop two different workflows refunding the same payment.

The Payments adapter contract must support stable idempotency keys and status lookup/reconciliation. The simulated Payments ledger persists across application restart and atomically enforces total refunds <= captured refundable amount, including independent workflow requests. Execution must preserve the same operation identity across retries. An ambiguous result stays unresolved/manual-review until reconciliation establishes the outcome; it must not trigger a new blind refund.

---

# 24. Request fingerprint

An idempotency key must not be silently reused for a materially different request.

Therefore store a stable fingerprint of normalized arguments.

Example failure:

```text
same key
refund amount 649
```

followed by:

```text
same key
refund amount 899
```

Expected behavior:

```text
reject as idempotency conflict
```

not "return old result" without validation.

---

# 25. Idempotency concurrency

The database must provide a unique constraint that prevents two concurrent requests from creating independent execution records for the same key.

The application must handle the resulting race deterministically.

The exact locking/upsert implementation is deferred to detailed implementation design.

---

# 26. Execution state model

Conceptual states:

```text
Pending
InProgress
Succeeded
FailedRetryable
FailedPermanent
OutcomeUnknown
```

`OutcomeUnknown` is important for network failures where the external provider may already have executed the operation.

The adapter must support reconciliation when possible.

---

# 27. Audit persistence

Audit owns its own schema:

```text
audit.audit_events
```

Properties:

- append-oriented;
- immutable by application behavior;
- tenant-aware;
- correlated with case/workflow/execution identifiers.

---

# 28. Audit immutability

V0:

- application exposes append only;
- no update/delete repository methods;
- first business mutation has minimal audit;
- source facts needed for audit delivery commit with the owning module change (outbox or reconstructable persisted evidence);
- Audit append deduplicates by stable source event identity, so recovery cannot lose or duplicate facts.

V1 hardening candidates:

- restricted DB role permissions;
- database trigger/rule protection;
- optional hash chaining;
- separate archival/retention design.

The first implementation should not over-engineer cryptographic tamper evidence before basic append-only semantics work.

---

# 29. Knowledge persistence

Knowledge owns:

```text
knowledge.documents
knowledge.document_versions
knowledge.chunks
```

PostgreSQL remains the primary store.

`pgvector` may be enabled at database level while vector columns/tables remain owned by the Knowledge module.

---

# 30. Knowledge metadata

Each chunk/document version should preserve metadata required for filtering and provenance, such as:

```text
TenantId
DocumentId
Version
Region
ValidFrom
ValidTo?
SourceType
ContentHash
PublishedAt
```

The exact model will be defined in the RAG architecture document.

---

# 31. Vector data boundary

Other modules must not perform raw vector queries against Knowledge tables.

Allowed:

```text
AI
    ↓
IKnowledgeRetrieval
    ↓
Knowledge
    ↓
pgvector
```

Forbidden:

```text
AI
    ↓
KnowledgeDbContext.Chunks
```

---

# 32. AI persistence

AI persistence stores operational metadata, not unrestricted hidden reasoning.

Expected records include:

```text
AgentRun
ModelInvocation
PromptVersion
Model/Provider metadata
token usage
latency
structured proposal
context manifest / evidence references
status/error metadata
```

Sensitive prompts/context should not automatically be persisted in full.

Data-retention/redaction policy is defined later under security.

---

# 33. Workflow persistence

Orchestration owns workflow state independently of the eventual workflow-engine choice.

Minimum persistent data:

```text
WorkflowRun
- WorkflowId
- TenantId
- CaseId
- WorkflowType
- CurrentState
- WaitingReason
- Version / concurrency token
- StartedAt
- UpdatedAt
- CompletedAt
- FailureCode?
```

This model must survive application restart.

ADR-004 accepts the application-owned V0 state machine and Temporal for V1. Runtime migration preserves business ownership.

---

# 34. Optimistic concurrency

Mutable state machines such as:

```text
WorkflowRun
ApprovalRequest
SupportCase
```

should use an explicit concurrency strategy.

Initial preference:

- optimistic concurrency token/version;
- reject or retry conflicting transitions;
- domain invariant re-check after reload.

Do not silently use "last write wins" for workflow/approval state.

---

# 35. Migrations

Each module owns its migrations.

Conceptually:

```text
CaseManagement migrations
Orchestration migrations
Decisioning migrations
AI migrations
Execution migrations
Knowledge migrations
Audit migrations
```

Migration ordering must be deterministic at deployment/startup tooling level.

---

# 36. EF migrations history

Each module/context should maintain clearly isolated migration history.

Possible implementation:

```text
module schema + module-specific __EFMigrationsHistory
```

Configure schema-specific migration history when implementing each module; apply migrations deterministically and verify a clean-database install in integration tests.

The important rule is that migration ownership must remain module-specific.

---

# 37. Seed data

Seed data categories must be separated.

## Development/demo seed

Examples:

- tenants;
- fake customers/orders/payments in simulated external services;
- sample policies;
- knowledge documents.

## Production-like reference data

Only stable required reference values.

Do not hide essential business initialization inside ad-hoc development seed scripts.

---

# 38. Integration events and durability

Module boundaries may use events.

Initially, in-process delivery is acceptable for non-critical secondary behavior.

For events that must survive a crash between:

```text
database commit
and
event publication
```

use a **transactional outbox** pattern, or explicit reconciliation when the committed source state preserves every fact needed to reconstruct the handoff. Both require stable identities and duplicate handling.

---

# 39. Outbox direction

Durable recovery is mandatory for critical V0 handoffs. A minimal transactional outbox is one option; explicit reconciliation of authoritative persisted states is another, if it can reconstruct every pending action safely.

Choose and document the mechanism when implementing case-to-workflow start, approval-to-resume and audit publication. Prove recovery after the producing commit but before delivery, plus duplicate delivery. Do not postpone these guarantees to V1. No message broker or generic messaging framework is required.

Rule:

> The producing module owns its outbox records because they must commit atomically with the module state change.

Example:

```text
Decisioning transaction:
- ApprovalRequest = APPROVED
- Outbox: ActionApproved
COMMIT
```

A publisher can dispatch the event later.

---

# 40. Inbox / duplicate event handling

If an external broker is introduced later, consumers that require deduplication may persist inbox/message-consumption records.

This is not needed merely for in-process events.

Do not add an inbox framework until messages can actually be delivered more than once.

---

# 41. Read models

Operational modules own normalized transactional state.

For operator dashboards that need data from many modules, we may later create dedicated read models.

Example:

```text
CaseOperationsView
- Case status
- Workflow state
- Approval state
- Latest AI proposal
- Execution result
```

This is a projection.

It is not the source of truth.

---

# 42. Reporting and evaluation data

Evals should not become part of the production transactional model.

Preferred direction:

```text
eval datasets/results
→ separate tooling storage / files / dedicated eval database if needed
```

A later tooling decision will choose the exact storage.

Production runtime must not depend on Eval storage.

---

# 43. PII and sensitive data

The persistence model must classify fields rather than treating every string equally.

Later security design will define:

- PII categories;
- encryption requirements;
- retention;
- log redaction;
- prompt/context persistence;
- deletion/anonymization requirements.

For now, the rule is:

> Persist only data that has a defined operational purpose.

---

# 44. Backup and disaster recovery

Not a V0 driver.

For V1 portfolio documentation, define:

- what data is critical;
- backup assumptions;
- restore verification concept;
- which data can be regenerated.

Do not build multi-region DR infrastructure unless it becomes an explicit learning goal.

---

# 45. Local development

Local development direction:

```text
PostgreSQL container
+ pgvector extension when Knowledge requires it
```

No separate database container per module.

This preserves module ownership without unnecessary operational complexity.

---

# 46. Testing implications

## Module integration tests

Each module tests its own persistence against real PostgreSQL where important.

Examples:

- unique constraints;
- transaction behavior;
- optimistic concurrency;
- migrations;
- tenant scoping;
- idempotency races.

## Cross-module tests

Must use public contracts, not cross-schema SQL shortcuts.

## Security tests

Must include:

- cross-tenant reads;
- cross-tenant mutations;
- idempotency conflict;
- stale approval transition;
- unauthorized execution.

---

# 47. Persistence ownership matrix

| Module | PostgreSQL schema | Primary state |
|---|---|---|
| Case Management | `case_management` | cases, case lifecycle |
| Orchestration | `orchestration` | workflow state/checkpoints |
| Decisioning | `decisioning` | policies, decisions, approvals |
| AI | `ai` | agent/model invocation metadata |
| Execution | `execution` | tool executions, idempotency |
| Knowledge | `knowledge` | documents, chunks, retrieval metadata |
| Audit | `audit` | append-only audit events |

External systems retain ownership of Customer, Order, Payment and Shipment master data.

---

# 48. Decisions accepted by this document

Preferred direction:

- one PostgreSQL database for V0/V1;
- schema-per-core-module;
- one logical DbContext per persistent module;
- no direct cross-module DbContext usage;
- no cross-module operational SQL joins;
- no default cross-module foreign keys;
- local transaction boundary per owning module;
- cross-module consistency through orchestration/retry/events;
- tenant-aware data model;
- Execution owns idempotency;
- Knowledge owns pgvector-backed knowledge state;
- Audit owns append-oriented audit data;
- outbox introduced only where durable publication is required;
- RLS remains optional defense-in-depth for later evaluation.

---

# 49. Decisions intentionally deferred

Not decided here:

- exact EF Core package versions;
- exact ID type implementation;
- UUIDv7 implementation details;
- RLS adoption;
- exact outbox library vs custom implementation;
- broker product;
- Redis;
- DB connection topology;
- final deployment migration mechanism;
- physical database separation for any module.

---

# 50. Acceptance criteria

This data architecture is ready for .NET solution design when:

- [ ] every persistent concept has one module owner;
- [ ] every module's schema is known;
- [ ] DbContext ownership is clear;
- [ ] cross-module FK/join rules are understood;
- [ ] transaction rules are clear;
- [ ] tenant scoping strategy is explicit;
- [ ] idempotency storage semantics are explicit;
- [ ] workflow state durability requirements are explicit;
- [ ] audit persistence semantics are explicit;
- [ ] migrations have module ownership;
- [ ] each critical V0 handoff has outbox or persisted-state reconciliation with crash/duplicate evidence;
- [ ] optional future RLS can be added without breaking the model.
