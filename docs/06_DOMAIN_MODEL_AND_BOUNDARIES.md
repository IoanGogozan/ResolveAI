# ResolveAI .NET — Domain Model & Boundaries

**Status:** Accepted planning baseline v1.1  
**Version:** 1.1  
**Architecture style:** Modular Monolith  
**Decomposition strategy:** Bounded Contexts by Business Capability

---

# 1. Purpose

This document defines the logical module boundaries for ResolveAI before the `.NET` solution and `.csproj` structure are designed.

The goal is to make explicit:

- what each module is responsible for;
- what state each module owns;
- what concepts are intentionally external to ResolveAI;
- what public contracts modules expose;
- which dependencies are allowed;
- which dependencies are forbidden;
- which business invariants belong where.

The key rule is:

> **Entity ≠ Module ≠ Bounded Context ≠ Microservice**

A module exists because it owns a coherent business capability, not because an entity exists.

---

# 2. System boundary

ResolveAI is not the system of record for the entire e-commerce domain.

ResolveAI is the AI-enabled customer-operations layer that coordinates work across existing or simulated enterprise systems.

## 2.1 Inside the ResolveAI system boundary

Core modules:

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

Engineering tooling:

```text
Evals
```

## 2.2 Outside the ResolveAI core domain

External bounded contexts / enterprise systems:

```text
CRM
Orders
Payments
Shipping
```

These may be simulated inside the portfolio repository, but they remain external from the perspective of the ResolveAI core domain.

That distinction is architectural, not necessarily physical.

---

# 3. High-level context map

```text
                           ┌──────────────────┐
                           │   Customer / UI  │
                           └────────┬─────────┘
                                    │
                                    ▼
                           ┌──────────────────┐
                           │  Case Management │
                           └────────┬─────────┘
                                    │
                                    ▼
                           ┌──────────────────┐
                           │  Orchestration   │
                           └──────┬─┬─┬───────┘
                                  │ │ │
                    ┌─────────────┘ │ └─────────────┐
                    ▼               ▼               ▼
             ┌────────────┐  ┌──────────────┐ ┌────────────┐
             │     AI     │  │ Decisioning  │ │ Execution  │
             └─────┬──────┘  └──────────────┘ └─────┬──────┘
                   │                                  │
                   ▼                                  ▼
            ┌──────────────┐                 ┌────────────────┐
            │  Knowledge   │                 │  Integrations  │
            └──────────────┘                 └───────┬────────┘
                                                    │
                     ┌──────────────────────────────┼────────────────────────────┐
                     ▼                              ▼                            ▼
                ┌────────┐                    ┌────────┐                   ┌──────────┐
                │  CRM   │                    │ Orders │                   │ Payments │
                └────────┘                    └────────┘                   └────┬─────┘
                                                                              │
                                                                              ▼
                                                                         ┌──────────┐
                                                                         │ Shipping │
                                                                         └──────────┘

Cross-cutting:
Identity / Tenancy / Observability / Audit
```

This diagram shows responsibility flow, not deployment topology.

---

# 4. Boundary design rules

## BR-01 — Business capability before entity

Modules are defined around cohesive business responsibilities.

Do not create modules named only after individual persistence entities unless the business capability justifies it.

---

## BR-02 — One owner for mutable business state

Every important mutable concept has one owning module.

Other modules may receive references, snapshots or read models, but they must not directly mutate another module's owned state.

---

## BR-03 — No direct database access across modules

A module must not query or update another module's tables through its DbContext/repository.

Even if all modules share the same PostgreSQL instance, storage ownership remains explicit.

---

## BR-04 — Public contracts, private implementation

Each module exposes a small public contract.

Internal entities, EF Core mappings, repositories and provider details stay private.

---

## BR-05 — External systems remain external

CRM, Orders, Payments and Shipping are treated as independent domains.

ResolveAI integrates through ports/adapters rather than copying their domain model into the core.

---

## BR-06 — References and snapshots are allowed

ResolveAI may persist:

```text
CustomerId
OrderId
PaymentId
ShipmentId
```

and may keep immutable operational snapshots where audit/recovery requires them.

A snapshot does not transfer ownership of the external business entity to ResolveAI.

---

# 5. Module: Case Management

## 5.1 Purpose

Case Management represents the support problem ResolveAI is trying to solve.

It is the business-facing entry point for a customer/operator case.

## 5.2 Responsibilities

- create a support case;
- maintain case lifecycle;
- associate trusted tenant/customer references;
- store customer-provided request content;
- expose case state to UI/operator;
- record case-level outcome;
- maintain case timeline references.

## 5.3 Owned concepts

Likely owned concepts:

```text
SupportCase
CaseId
CaseStatus
CasePriority
CaseChannel
CaseOutcome
CaseReference
CaseTimelineEntry
```

Possible references:

```text
CustomerId
OrderId
PaymentId
WorkflowId
```

## 5.4 Does NOT own

```text
Customer
Order
Payment
Shipment
Policy
ApprovalRequest
AgentRun
ToolExecution
```

## 5.5 Public commands

Examples:

```text
CreateCase
AttachOrderReference
CloseCase
ReopenCase
RecordCaseOutcome
```

## 5.6 Public queries

Examples:

```text
GetCase
GetCaseSummary
GetCaseTimeline
```

## 5.7 Emits

Possible events:

```text
CaseCreated
CaseReadyForInvestigation
CaseClosed
```

## 5.8 Allowed dependencies

Case Management may depend on:

- trusted tenant/user context;
- its own persistence;
- audit publishing contract.

Host connects HTTP intake to application contracts. Orchestration consumes CaseManagement contracts; CaseManagement must not reference Orchestration.

## 5.9 Forbidden dependencies

Case Management must not directly depend on:

- OpenAI/provider SDKs;
- payment provider SDK;
- Knowledge persistence;
- Policy implementation;
- another module's DbContext.

---

# 6. Module: Orchestration

## 6.1 Purpose

Orchestration owns long-running business process state.

It coordinates **when** capabilities are invoked and **what state the overall process is in**.

It does not own the detailed business rules of those capabilities.

## 6.2 Responsibilities

- start a workflow for a case;
- persist workflow state;
- coordinate investigation steps;
- request AI reasoning;
- request policy evaluation;
- pause for approval;
- resume after approval;
- request side-effect execution;
- track retries and recoverable failures;
- determine process completion.

## 6.3 Owned concepts

```text
WorkflowRun
WorkflowId
WorkflowType
WorkflowState
WorkflowStep
WaitingReason
WorkflowFailure
RetryMetadata
```

Optional:

```text
WorkflowCheckpoint
```

## 6.4 Does NOT own

```text
Policy
ApprovalRequest
AgentRun
ModelInvocation
ToolExecution
IdempotencyRecord
KnowledgeDocument
AuditEvent
```

It may reference their identifiers/results.

## 6.5 Public commands

```text
StartWorkflow
ResumeWorkflow
CancelWorkflow
RetryWorkflowStep
```

## 6.6 Public queries

```text
GetWorkflow
GetWorkflowHistory
GetWorkflowStatus
```

## 6.7 Emits

```text
WorkflowStarted
WorkflowWaitingForApproval
WorkflowResumed
WorkflowFailed
WorkflowCompleted
```

## 6.8 Allowed dependencies

Orchestration may call public contracts from:

- AI;
- Decisioning;
- Execution;
- Case Management where case context is required.

## 6.9 Forbidden dependencies

Orchestration must not:

- implement policy thresholds;
- call LLM providers directly;
- call payment providers directly;
- query Knowledge storage directly;
- update Approval tables directly.

## 6.10 Key invariant

> Orchestration coordinates business capabilities; it does not absorb them.

---

# 7. Module: Decisioning

## 7.1 Purpose

Decisioning owns deterministic decisions about whether a proposed business action is allowed, blocked or requires approval.

Policy and approval belong together in this first architecture because approval exists primarily as a consequence of policy.

## 7.2 Responsibilities

- evaluate proposed actions against deterministic policies;
- version policy logic/rules;
- determine risk/approval requirement;
- create approval requests;
- validate approval authority;
- record approve/reject decisions;
- ensure approval corresponds to the intended action.

## 7.3 Owned concepts

```text
Policy
PolicyVersion
PolicyRule
PolicyDecision
DecisionOutcome

ApprovalRequest
ApprovalId
ApprovalStatus
ApprovalDecision
ApprovalAuthority
```

Possible decision outcomes:

```text
Allow
RequireApproval
Block
```

## 7.4 Does NOT own

```text
WorkflowRun
Refund
Payment
AgentRun
KnowledgeDocument
ToolExecution
```

## 7.5 Public commands

```text
EvaluateAction
CreateApprovalRequest
ApproveAction
RejectAction
ExpireApproval
```

## 7.6 Public queries

```text
GetPolicyDecision
GetApprovalRequest
GetApprovalStatus
GetApplicablePolicyVersion
```

## 7.7 Emits

```text
ActionAllowed
ActionBlocked
ApprovalRequired
ActionApproved
ActionRejected
ApprovalExpired
```

## 7.8 Allowed dependencies

Decisioning may depend on:

- trusted actor/tenant context;
- its own persistence;
- stable action facts supplied in the request.

If evidence references are needed, Decisioning receives them as input rather than querying AI internals.

## 7.9 Forbidden dependencies

Decisioning must not depend directly on:

- LLM provider;
- payment provider;
- RAG implementation;
- workflow persistence;
- HTTP request objects.

## 7.10 Key invariant

> A prompt may describe policy; only Decisioning enforces policy.

---

# 8. Module: AI

## 8.1 Purpose

The AI module owns probabilistic reasoning and model interaction.

It transforms task context into validated structured proposals.

## 8.2 Responsibilities

- build minimal task context;
- invoke the internal LLM gateway;
- validate structured output;
- record model invocation metadata;
- track prompt/model version;
- create AgentRun records;
- return proposals/evidence references to callers.

## 8.3 Owned concepts

```text
AgentRun
AgentRunId
AgentTask
StructuredProposal
ModelInvocation
PromptVersion
ContextManifest
ModelUsage
```

## 8.4 Does NOT own

```text
PolicyDecision
ApprovalDecision
WorkflowState
RefundExecution
TenantIdentity
AuthorizationDecision
KnowledgeDocument
```

## 8.5 Public commands

Conceptually:

```text
InvestigateCase
GenerateProposal
ClassifyIntent
SummarizeEvidence
```

## 8.6 Public result shape

Conceptual example:

```text
StructuredProposal
- actionType
- targetReference
- arguments
- confidence
- reasonCode
- evidenceReferences
```

The proposal is not authorization.

## 8.7 Allowed dependencies

AI may use:

- Knowledge public retrieval contract;
- Execution read-only capabilities where explicitly exposed to the agent;
- internal LLM gateway abstraction;
- trusted task/tenant context supplied by caller.

## 8.8 Forbidden dependencies

AI must never:

- execute a high-risk side effect;
- decide tenant identity;
- override policy;
- validate approval authority;
- mutate payment state directly.

## 8.9 Key invariant

> AI output is untrusted structured input to the rest of the backend.

---

# 9. Module: Execution

## 9.1 Purpose

Execution is the safety boundary between requested capabilities and real-world/business side effects.

It owns tool/capability execution semantics.

## 9.2 Responsibilities

- register/expose tool definitions;
- validate tool input;
- enforce tool permission requirements;
- enforce risk classification;
- validate policy/approval preconditions;
- derive/validate idempotency key;
- execute adapters;
- persist execution result;
- safely handle retries;
- emit execution audit information.

## 9.3 Owned concepts

```text
ToolDefinition
ToolVersion
ToolRiskLevel
ToolRequest
ToolResult

ExecutionRequest
ExecutionId
ExecutionStatus
ExecutionResult

IdempotencyKey
IdempotencyRecord
ExecutionAttempt
```

## 9.4 Does NOT own

```text
Payment
Order
Customer
Shipment
Policy
ApprovalRequest
WorkflowRun
```

## 9.5 Public commands

Examples:

```text
ExecuteCapability
ExecuteApprovedAction
```

## 9.6 Public queries

Examples:

```text
GetExecutionResult
GetToolDefinition
GetIdempotencyResult
```

## 9.7 Emits

```text
ExecutionStarted
ExecutionSucceeded
ExecutionFailed
DuplicateExecutionResolved
```

## 9.8 Allowed dependencies

Execution may depend on:

- Decisioning public validation/result contract;
- trusted user/tenant/permission context;
- integration ports;
- its own persistence;
- audit publishing contract.

## 9.9 Forbidden dependencies

Execution must not:

- let AI bypass policy;
- trust tenant from tool arguments;
- access external systems without tenant/resource validation;
- re-run a completed idempotent action as a new business effect.

## 9.10 Key invariant

> Every critical mutation must be safe under retry, duplication and timeout.

---

# 10. Module: Knowledge

## 10.1 Purpose

Knowledge owns policy/support document ingestion, retrieval and evidence provenance.

## 10.2 Responsibilities

- ingest knowledge documents;
- version documents;
- chunk/index documents;
- attach tenant/region/version metadata;
- retrieve relevant evidence;
- return provenance;
- support hybrid retrieval if justified.

## 10.3 Owned concepts

```text
KnowledgeDocument
KnowledgeDocumentId
KnowledgeDocumentVersion
KnowledgeChunk
KnowledgeMetadata
RetrievalQuery
RetrievalResult
EvidenceReference
```

## 10.4 Does NOT own

```text
PolicyDecision
ApprovalDecision
AgentRun
SupportCase
```

A knowledge document may describe a policy, but Decisioning owns enforcement.

## 10.5 Public commands

```text
IngestDocument
PublishDocumentVersion
RetireDocumentVersion
```

## 10.6 Public queries

```text
RetrieveEvidence
GetDocumentVersion
GetEvidence
```

## 10.7 Emits

Possible events:

```text
KnowledgeDocumentPublished
KnowledgeDocumentRetired
```

## 10.8 Allowed dependencies

Knowledge may depend on:

- tenant context;
- embedding/search infrastructure through internal adapters;
- its own persistence.

## 10.9 Forbidden dependencies

Knowledge must not:

- authorize actions;
- execute mutations;
- determine approval requirements.

## 10.10 Key invariant

> Retrieval produces evidence; it does not produce authority.

---

# 11. Module: Audit

## 11.1 Purpose

Audit stores the authoritative operational evidence trail for important business/security actions.

## 11.2 Responsibilities

- append audit events;
- retain actor/action/resource/result metadata;
- record policy and approval references;
- support forensic/business reconstruction;
- protect audit integrity.

## 11.3 Owned concepts

```text
AuditEvent
AuditEventId
AuditActor
AuditAction
AuditResource
AuditOutcome
```

## 11.4 Persistence semantics

Conceptually:

```text
INSERT → allowed
UPDATE → forbidden
DELETE → forbidden
```

Physical enforcement strategy will be decided later.

## 11.5 Public commands

```text
AppendAuditEvent
```

## 11.6 Public queries

```text
GetAuditTrail
GetResourceAuditTrail
```

## 11.7 Dependencies

Audit should depend on as little business implementation detail as possible.

Other modules publish audit-safe facts through an explicit contract.

## 11.8 Key invariant

> Audit records what happened; it does not become the workflow engine or operational database.

---

# 12. Platform capability: Identity & Tenancy

Identity/Tenancy is not a primary business bounded context.

It is trusted platform context.

## Responsibilities

- authenticated principal;
- user/operator identity;
- roles/permissions;
- trusted tenant context;
- authorization helpers/policies.

Conceptual contract:

```text
IExecutionContext
- UserId
- TenantId
- Roles
- Permissions
```

## Critical invariant

> TenantId must never be accepted from LLM output as authoritative execution context.

---

# 13. Platform capability: Observability

Observability is infrastructure/platform capability, not a business module.

It provides:

- traces;
- metrics;
- structured logs;
- correlation;
- redaction policy.

Business modules may instrument operations but must not depend on specific telemetry backends.

---

# 14. Engineering tooling: Evals

Evals is intentionally outside the production domain dependency graph.

## Owns

```text
EvalCase
EvalDataset
EvalRun
EvalResult
Baseline
RegressionReport
```

## Dependency direction

Allowed:

```text
ResolveAI.Evals
    ↓
ResolveAI public contracts / API / test harness
```

Forbidden:

```text
ResolveAI production modules
    ↓
ResolveAI.Evals
```

The production application must not require the eval runner to function.

---

# 15. External bounded contexts and anti-corruption layer

## 15.1 CRM

ResolveAI contract may expose:

```text
GetCustomer
GetCustomerRiskFacts
AddCustomerNote
```

ResolveAI does not own the customer master record.

---

## 15.2 Orders

ResolveAI contract may expose:

```text
GetOrder
GetOrderItems
GetOrderStatus
```

ResolveAI does not own order lifecycle.

---

## 15.3 Payments

ResolveAI contract may expose:

```text
GetPayment
RequestRefund / Refund
GetRefundStatus
```

Payment ownership remains external.

Execution owns the safety/idempotency behavior around calling this system.

---

## 15.4 Shipping

ResolveAI contract may expose:

```text
GetShipment
GetDeliveryStatus
CancelShipment
```

ResolveAI does not own shipment lifecycle.

---

# 16. Anti-corruption layer principle

External schemas must not leak deeply into ResolveAI.

Example:

```text
External Payment API DTO
        ↓ adapter maps
PaymentFacts / PaymentReference
        ↓
ResolveAI
```

Avoid:

```text
StripeRefundResponse
```

or another provider-specific object appearing in:

- Decisioning;
- Orchestration;
- AI;
- Case Management.

---

# 17. Ownership matrix

| Concept | Owner |
|---|---|
| SupportCase | Case Management |
| CaseStatus | Case Management |
| CaseOutcome | Case Management |
| WorkflowRun | Orchestration |
| WorkflowState | Orchestration |
| RetryMetadata | Orchestration |
| Policy | Decisioning |
| PolicyVersion | Decisioning |
| PolicyDecision | Decisioning |
| ApprovalRequest | Decisioning |
| ApprovalDecision | Decisioning |
| AgentRun | AI |
| ModelInvocation | AI |
| PromptVersion | AI |
| StructuredProposal | AI |
| ToolDefinition | Execution |
| ToolExecution | Execution |
| IdempotencyRecord | Execution |
| KnowledgeDocument | Knowledge |
| KnowledgeChunk | Knowledge |
| EvidenceReference | Knowledge |
| AuditEvent | Audit |
| Customer source of truth | CRM external context |
| Order source of truth | Orders external context |
| Payment source of truth | Payments external context |
| Shipment source of truth | Shipping external context |
| EvalCase / EvalRun | Evals tooling |

---

# 18. Dependency policy

## 18.1 Runtime flow and compile-time dependencies

Runtime flow:

```text
HTTP intake → case persistence → workflow start
Orchestration → AI / Decisioning / Execution
AI → Execution (approved reads) / Knowledge (V1 retrieval)
Execution → integration port → adapter → simulated/external system
```

This describes calls, not project references. The canonical compile-time graph is in `12_DOTNET_SOLUTION_STRUCTURE.md`, section 12.

CaseManagement does not reference Orchestration. Integration ports belong to the consuming module; Integrations implements them and Host wires them through DI. Thus Execution does not reference the Integrations assembly.

Audit receives durable business facts; Identity/Tenancy supplies trusted context. Critical handoffs need persisted recovery as defined in the persistence and workflow documents.

---

# 19. Dependency restrictions

The following direct dependencies are forbidden:

```text
AI → Payments SDK
AI → Decisioning persistence
AI → Approval persistence

Orchestration → LLM SDK
Orchestration → Payment provider
Orchestration → Knowledge database

Decisioning → LLM provider
Decisioning → Payment provider

Case Management → Payment provider

Knowledge → Policy enforcement

Integration adapter → another module's database
```

---

# 20. Module communication styles

Use the simplest style that preserves boundaries.

## 20.1 Synchronous application contract

Use when the caller needs an immediate result.

Examples:

```text
EvaluateAction
GetWorkflow
RetrieveEvidence
ExecuteApprovedAction
```

## 20.2 Domain/integration event

Use when a fact has happened and one or more listeners may react.

Examples:

```text
CaseCreated
ApprovalRequired
ActionApproved
ExecutionSucceeded
WorkflowCompleted
```

Do not introduce a message broker merely because events exist.

In-process-only events are acceptable for non-critical secondary behavior. Case-to-workflow, approval-to-resume and audit publication require persisted recovery in V0 through a minimal outbox or explicit reconciliation.

---

# 21. Command / Query / Event vocabulary

This project will use command/query/event terminology as a modeling discipline.

It does not imply adoption of a CQRS framework or event sourcing.

## Commands

Intent to change state:

```text
CreateCase
StartWorkflow
ApproveAction
ExecuteApprovedAction
```

## Queries

Read-only intent:

```text
GetCase
GetOrderFacts
RetrieveEvidence
GetWorkflowStatus
```

## Events

Facts that already occurred:

```text
CaseCreated
ApprovalRequired
ActionApproved
RefundExecuted
```

---

# 22. Transaction boundaries

Initial direction:

- local transaction boundaries stay inside one owning module;
- cross-module workflows do not pretend to be one distributed ACID transaction;
- orchestration coordinates multiple module/external operations;
- idempotency and recovery handle retries/partial failure.

Example:

```text
Decisioning creates ApprovalRequest
```

is one transaction.

Later:

```text
Execution performs refund
```

is a separate transaction/external operation.

Orchestration connects the process.

---

# 23. Important snapshots vs references

## Reference

Use when only identity is needed:

```text
CustomerId
OrderId
PaymentId
```

## Snapshot

Use when a point-in-time fact must be preserved for audit/reproducibility:

```text
RefundAssessmentSnapshot
- order amount at decision time
- delivery state at decision time
- policy version
- selected evidence references
```

Snapshots should be explicit and immutable.

They must not become accidental replicas of entire external databases.

---

# 24. Shared Kernel policy

Do not create a generic dumping-ground project.

A possible Shared Kernel must be minimal.

Candidates only after real duplication appears:

```text
TenantId
Money
Result<T>
strongly typed identifiers
clock abstraction
```

Business concepts such as `Customer`, `Order`, `Policy`, `Workflow`, `Approval` must not be moved into Shared merely to make references convenient.

---

# 25. Boundary validation questions

Before adding a dependency between modules, ask:

1. Which business capability requires it?
2. Who owns the state being accessed?
3. Could the interaction use a smaller contract?
4. Is the caller reading facts or changing state?
5. Does the caller need synchronous confirmation?
6. What happens if the dependency is unavailable?
7. Is tenant context preserved?
8. Is audit required?
9. Would the dependency still make sense if modules were separate processes later?

If the answers are unclear, the boundary is not ready.

---

# 26. Decisions confirmed by this document

The following are now the preferred architecture direction:

- bounded contexts by business capability;
- modular monolith;
- seven initial core ResolveAI modules;
- Decisioning owns both policy and approval for the initial architecture;
- CRM, Orders, Payments and Shipping remain external bounded contexts;
- Evals remains engineering tooling;
- Identity/Tenancy/Observability are platform capabilities;
- no direct cross-module database access;
- AI output is a proposal, never authorization;
- external-system adapters form an anti-corruption layer.

---

# 27. Related decisions and remaining deferrals

Already decided in the synchronized baseline:

- One project per implemented bounded context: solution structure document.
- Module-owned PostgreSQL schemas and DbContexts: ADR-003.
- V0 persisted state machine and V1 Temporal: ADR-004.
- Trusted identity boundary: ADR-005; development auth in V0 and OIDC in V1.
- Initial stack and provider adapter: technology stack document.

Still deferred: broker/cache adoption, exact production identity and Temporal deployment configuration, detailed RAG implementation, physical store separation and service extraction. These require implementation evidence.

---

# 28. Acceptance criteria for the boundary design

This design is ready to drive the next architecture stage when:

- [ ] every important mutable concept has one owner;
- [ ] every module has a coherent purpose;
- [ ] external systems are clearly separated;
- [ ] forbidden dependencies are understood;
- [ ] workflow coordination is separated from business rules;
- [ ] AI reasoning is separated from authorization/execution;
- [ ] policy enforcement is deterministic;
- [ ] tool execution owns idempotency semantics;
- [ ] the dependency graph has no obvious circular business dependencies;
- [ ] the structure can be mapped into .NET projects without inventing new domain boundaries.
