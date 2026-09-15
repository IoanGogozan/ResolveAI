# ResolveAI .NET — Workflow Architecture

**Status:** Accepted planning baseline v1.1  
**Version:** 1.1  
**Related ADR:** ADR-004  
**Scope:** Orchestration module

---

# 1. Purpose

This document defines how ResolveAI models long-running workflows before the final .NET solution structure is created.

The workflow architecture must support:

- persistent state;
- restart recovery;
- human approval waits;
- retries and backoff;
- timeout handling;
- failure classification;
- idempotent continuation;
- external side effects;
- future migration from an application-owned persisted state machine to Temporal.

The central architectural rule is:

> **Workflow orchestration coordinates capabilities; it does not own domain rules.**

---

# 2. Architectural evolution

ResolveAI deliberately uses two stages.

## V0 — Application-owned persistent workflow state

```text
.NET
+
PostgreSQL persisted state machine
```

Goals:

- understand state transitions;
- prove restart recovery;
- prove approval wait;
- prove concurrency handling;
- prove idempotent continuation.

## V1 — Dedicated durable workflow engine

```text
Temporal
```

Goals:

- long-running durable execution;
- retries/backoff;
- signals;
- timers;
- workflow history;
- worker recovery;
- production-grade orchestration.

The domain architecture remains the same in both stages.

---

# 3. Stable orchestration boundary

The rest of the system should depend on orchestration contracts, not on PostgreSQL state-machine implementation details or Temporal SDK types.

Conceptually:

```text
Host / application entry points
        ↓
Orchestration contracts
        ↓
Orchestration implementation
        ↓
PostgreSQL V0 / Temporal V1
```

The exact C# interfaces will be defined later.

---

# 4. Workflow responsibility

Orchestration owns:

- workflow identity;
- current workflow state;
- lifecycle coordination;
- waiting state;
- retry scheduling intent;
- failure status;
- references to results from other modules;
- transition history sufficient for operational visibility.

Orchestration does NOT own:

- policy thresholds;
- authorization;
- tenant authority;
- LLM prompts;
- knowledge retrieval logic;
- payment/refund semantics;
- idempotency semantics;
- approval business rules;
- audit authority.

---

# 5. Canonical workflow — damaged item refund

Conceptual flow:

```text
START
  ↓
Load Case
  ↓
Investigate
  ↓
Generate AI Proposal
  ↓
Evaluate Action
  ↓
Needs Approval?
  ├── No ─────────────────────────┐
  │                               │
  └── Yes → WAITING_FOR_APPROVAL  │
                  ↓               │
             Human Decision       │
                  ↓               │
             Approved?            │
             ├── No → REJECTED    │
             └── Yes ─────────────┘
                                  ↓
                           Execute Refund
                                  ↓
                           Update Case
                                  ↓
                           Optional CRM/notification integration
                                  ↓
                              COMPLETED
```

V0 requires the persisted case outcome. CRM mutation and customer notification are optional extensions; when introduced, their failures must be visible and their retries must not repeat the refund.

---

# 6. V0 workflow state model

Initial workflow states:

```text
Created
Investigating
AwaitingDecision
WaitingForApproval
ReadyForExecution
Executing
RetryPending
ManualReview
Rejected
Completed
Failed
Cancelled
```

These states are intentionally business-process-oriented rather than infrastructure-oriented.

---

# 7. Transition rules

Example legal transitions:

```text
Created
→ Investigating

Investigating
→ AwaitingDecision
→ RetryPending
→ Failed

AwaitingDecision
→ WaitingForApproval
→ ReadyForExecution
→ Failed

WaitingForApproval
→ ReadyForExecution
→ Rejected
→ Cancelled

ReadyForExecution
→ Executing

Executing
→ Completed
→ RetryPending
→ ManualReview
→ Failed

RetryPending
→ Investigating
→ Executing
→ Failed
```

Illegal transitions must be rejected explicitly.

Example:

```text
Completed → Executing
```

is invalid.

---

# 8. State transition invariant

Every state transition must be:

- authorized by the current workflow state;
- persisted;
- concurrency-safe;
- attributable to a cause;
- observable.

Recommended persisted metadata:

```text
WorkflowId
TenantId
CaseId
WorkflowType
CurrentState
Version
WaitingReason
LastTransitionAt
RetryCount
FailureCode
FailureCategory
StartedAt
CompletedAt
```

---

# 9. Optimistic concurrency

V0 should use optimistic concurrency.

Example:

```text
worker A reads version 7
worker B reads version 7

worker A updates → version 8

worker B attempts update with version 7
→ concurrency conflict
```

The loser reloads state and re-evaluates whether the transition is still valid.

Silent last-write-wins behavior is not acceptable for workflow state.

---

# 10. Human approval lifecycle

Decisioning owns approval business state.

Orchestration only waits for and reacts to the outcome.

Conceptual flow:

```text
Orchestration
    ↓
Decisioning.EvaluateAction
    ↓
REQUIRE_APPROVAL
    ↓
Decisioning creates ApprovalRequest
    ↓
Orchestration persists WAITING_FOR_APPROVAL
    ↓
application can restart
    ↓
operator approves/rejects
    ↓
Decisioning records decision
    ↓
Orchestration is notified/resumed
```

The workflow never decides who is authorized to approve.

---

# 11. Approval binding

Approval must be bound to the intended action.

Conceptual approval identity includes:

```text
ApprovalId
ActionType
TargetResourceId
NormalizedActionFingerprint
PolicyDecisionId
RequestedAt
```

If the proposed mutation materially changes, the old approval must not be reused automatically.

---

# 12. Approval expiration

Approval may have:

```text
ExpiresAt
```

If an approval expires while the workflow waits:

```text
WaitingForApproval
→ ManualReview / Re-evaluate / Cancel
```

The exact business rule belongs to Decisioning.

Orchestration executes the resulting transition.

---

# 13. Failure taxonomy

Not all failures are equivalent.

The architecture uses explicit failure categories.

## 13.1 Transient technical failure

Examples:

```text
HTTP timeout
connection reset
429
temporary provider unavailable
database transient failure
```

Typical treatment:

```text
retry
+
bounded exponential backoff
```

---

## 13.2 Permanent technical failure

Examples:

```text
invalid external endpoint configuration
unsupported provider capability
permanent contract incompatibility
```

Typical treatment:

```text
fail workflow
or
manual review
```

---

## 13.3 Business rule violation

Examples:

```text
refund exceeds original payment
payment already fully refunded
order not eligible
approval expired
```

Typical treatment:

```text
do not retry automatically
```

---

## 13.4 Authorization failure

Examples:

```text
actor lacks permission
tenant mismatch
approval authority invalid
```

Typical treatment:

```text
block
audit
do not retry automatically
```

---

## 13.5 Malformed / invalid model output

Examples:

```text
invalid JSON
schema violation
unknown action type
invalid arguments
```

Possible treatment:

```text
bounded model retry / repair attempt
then fail or escalate
```

The model never gets unlimited retries.

---

## 13.6 Outcome unknown

This is a special category.

Example:

```text
refund request sent
payment provider processes it
network fails before response arrives
```

The system cannot assume success or failure.

Expected handling:

```text
OutcomeUnknown
        ↓
Execution reconciliation / idempotency lookup
        ↓
known result
```

Workflow orchestration waits for the Execution module to resolve or reconcile the outcome.

---

# 14. Retry policy

Retry policy is split between orchestration and capability semantics.

Orchestration may decide:

```text
when
how many times
backoff timing
when to stop
```

The called module/adapter must classify whether the failure is retryable.

Example result:

```text
ExecutionFailure
- Category = Transient
- Retryable = true
- SuggestedDelay?
```

---

# 15. Retry limits

Every retry loop must be bounded.

No workflow step may retry forever.

Each retry policy should define:

```text
MaxAttempts
InitialDelay
BackoffStrategy
MaximumDelay
RetryableCategories
TimeoutPerAttempt
```

Exact numbers are implementation/configuration decisions.

---

# 16. Timeout model

Timeouts should exist at different levels.

## Per external call

Example:

```text
payment provider request timeout
```

## Per workflow activity/step

Example:

```text
investigation step max duration
```

## Human approval

Human approval is not treated as a normal short technical timeout.

It is a durable waiting state.

---

# 17. Idempotent continuation

Workflow recovery must not imply duplicate side effects.

Example:

```text
workflow state = Executing
process crashes
```

After restart:

```text
workflow resumes
```

but Execution checks persisted idempotency state before creating a new refund.

Therefore:

```text
workflow durability
≠
side-effect safety
```

Both are required.

---

# 18. V0 persistence mechanics

V0 uses the Orchestration PostgreSQL schema.

Minimum tables/concepts:

```text
workflow_runs
workflow_transitions
```

Potential `workflow_runs` shape:

```text
id
tenant_id
case_id
workflow_type
current_state
version
waiting_reason
retry_count
failure_category
failure_code
started_at
updated_at
completed_at
```

Potential transition history:

```text
workflow_id
sequence
from_state
to_state
reason
correlation_id
occurred_at
```

This history supports debugging and portfolio demonstration.

---

# 19. V0 execution model

V0 can use a simple background worker / application service.

Conceptually:

```text
API creates case
    ↓
Orchestration starts workflow
    ↓
worker processes runnable workflow
    ↓
persist state after meaningful transition
```

No sophisticated distributed scheduler is required initially.

Each delivery introduces the persistence/recovery needed by its new behavior. Before completing a critical handoff, define a minimal outbox or persisted-state reconciliation mechanism, stable operation identity and duplicate handling. Cover:

- case committed before workflow start;
- approval committed before resume;
- module result committed before workflow checkpoint;
- payment processed before local result persistence;
- business change committed before audit append.

Idempotent module operations or result lookup prevent repeated approvals, executions and audit facts when a workflow checkpoint lags behind a completed module operation. A version token on workflow state alone does not protect an external side effect. Payment-level invariants also protect different workflows targeting one payment.

---

# 20. V0 restart recovery

On startup, the orchestration worker can locate workflows in recoverable states.

Examples:

```text
Investigating
RetryPending
ReadyForExecution
Executing (requires reconciliation)
```

Waiting workflows do not hold active work or in-memory waits. A durable notification or bounded reconciliation pass through Decisioning contracts must detect committed approval outcomes even if notification was lost.

Example:

```text
WaitingForApproval
```

remains waiting until a persisted approval outcome is delivered or discovered by reconciliation. A lost in-memory resume signal cannot strand it indefinitely.

---

# 21. V0 waiting strategy

Do not hold:

- thread;
- request;
- in-memory Task;
- database transaction

while waiting for human approval.

Waiting is represented only as durable state.

---

# 22. V1 Temporal mapping

Once Temporal is introduced:

```text
Workflow definition
```

owns durable coordination.

Example conceptual mapping:

```text
RefundCaseWorkflow
├── LoadCaseActivity
├── InvestigateCaseActivity
├── EvaluateActionActivity
├── WaitForApproval signal
├── ExecuteRefundActivity
├── UpdateCaseActivity
└── NotifyCustomerActivity
```

Activities call the existing module contracts.

---

# 23. Temporal workflow rule

Temporal workflow code must remain orchestration logic only.

Forbidden inside workflow definitions:

```text
refund policy threshold code
direct SQL
OpenAI SDK
payment provider SDK
tenant authorization logic
```

Allowed:

```text
call activity
wait for signal
schedule retry
branch on returned domain result
complete workflow
```

---

# 24. Temporal activities

Activities are adapters from workflow runtime into application/module contracts.

Example:

```text
EvaluateActionActivity
    ↓
IDecisioningModule.EvaluateAction(...)
```

Not:

```text
EvaluateActionActivity
    ↓
DecisioningDbContext
```

The same module boundaries remain valid after Temporal adoption.

---

# 25. Temporal signals / external events

Human approval maps naturally to an external signal/event.

Conceptually:

```text
operator approval
    ↓
Decisioning commits approval
    ↓
signal workflow
    ↓
workflow resumes
```

Decisioning remains the authority for approval validity.

Temporal only receives the fact/outcome.

---

# 26. Temporal retries

Temporal may implement retry scheduling.

However:

- business-rule failures are non-retryable;
- authorization failures are non-retryable;
- outcome-unknown requires Execution reconciliation;
- idempotency remains owned by Execution.

Temporal retry configuration must not replace capability-level safety.

---

# 27. Temporal versioning concern

Long-running workflows may survive application deployments.

Therefore workflow evolution must account for:

- old running instances;
- new workflow code;
- compatible activity contracts;
- deployment/versioning strategy.

This becomes a V1 implementation concern and should be documented before introducing production-like long-running workflows.

---

# 28. Workflow observability

Every workflow should expose:

```text
WorkflowId
CaseId
TenantId
CurrentState
WaitingReason
RetryCount
LastFailure
StartedAt
LastTransitionAt
CompletedAt
```

Tracing should correlate:

```text
case
workflow
agent run
policy decision
approval
execution
audit
```

---

# 29. Workflow audit vs workflow history

These are related but different.

## Workflow history

Operational process state:

```text
Investigating
→ WaitingForApproval
→ Executing
→ Completed
```

Owned by Orchestration.

## Audit trail

Security/business evidence:

```text
who requested
which policy
who approved
what action executed
what resource changed
```

Owned by Audit.

Do not merge these concerns into one table.

---

# 30. V0 required tests

## State transition tests

- legal transitions succeed;
- illegal transitions fail.

## Concurrency test

Two competing transitions cannot both win.

## Restart test

Workflow survives application restart.

## Approval wait test

Workflow remains waiting with no in-memory dependency.

## Resume test

Approval resumes the correct workflow.

## Duplicate resume test

Same approval notification does not execute twice.

## Execution timeout test

Ambiguous refund outcome does not produce duplicate refund.

## Retry exhaustion test

Bounded retries eventually move to failure/manual review.

---

# 31. V1 Temporal tests

Additional tests:

- workflow survives worker restart;
- activity retry behavior;
- signal before/after waiting point;
- duplicate signal handling;
- long timer behavior;
- workflow replay/version compatibility;
- activity timeout;
- provider failure/fallback integration.

---

# 32. V0 → V1 migration criteria

Do not introduce Temporal merely because V0 is complete.

Migrate when the project has demonstrated that manual orchestration is beginning to own infrastructure complexity such as:

```text
durable timers
retry scheduling
long waits
workflow history
distributed workers
recovery coordination
workflow versioning
```

At that point a dedicated engine has earned its place.

---

# 33. What must remain unchanged during migration

The migration should preserve:

```text
Case Management contracts
Decisioning contracts
Execution contracts
AI contracts
Knowledge contracts
Audit contracts
integration ports
business rules
idempotency semantics
tenant model
```

Only the orchestration runtime mechanism changes materially.

---

# 34. Portfolio evidence

The project should preserve both stages in documentation.

## V0 demonstration

Show:

- persisted state machine;
- restart recovery;
- optimistic concurrency;
- approval wait;
- idempotent continuation.

## V1 demonstration

Show:

- Temporal workflow;
- worker restart;
- durable wait;
- signal;
- automatic retry/backoff;
- workflow history.

Then explain why the migration was justified.

This demonstrates architecture evolution rather than framework collection.

---

# 35. Decisions confirmed

- V0 uses application-owned PostgreSQL-persisted workflow state.
- V1 adopts Temporal as the target durable workflow engine.
- Orchestration contracts remain stable across the evolution.
- workflow engine is a coordinator, not a business-rule owner.
- Decisioning remains approval authority.
- Execution remains side-effect/idempotency authority.
- retries are bounded and failure-category-aware.
- `OutcomeUnknown` is modeled explicitly.
- approval waiting is durable, never in-memory.
- workflow history and audit remain separate concepts.

---

# 36. Decisions deferred

Implementation details still deferred (the Temporal product choice is accepted):

- exact Temporal hosting/deployment model;
- exact C# workflow interface names;
- exact retry numbers;
- exact activity timeouts;
- polling strategy for V0 worker;
- whether V0 uses Channels/background service or another scheduling mechanism;
- Temporal namespace/task queue structure;
- Temporal persistence/deployment infrastructure;
- workflow code versioning strategy.

These belong to implementation/runtime design after the `.NET` project architecture is established.
