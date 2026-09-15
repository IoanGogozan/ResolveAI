# ADR-004 — Evolve from Application-Owned Persistent Workflow State to Temporal

**Status:** Accepted  
**Date:** 2026-09-15  
**Related:** ADR-001, ADR-002, ADR-003

---

# 1. Context

ResolveAI contains long-running workflows that may:

- wait for human approval;
- survive process restart;
- call unreliable external providers;
- retry transient failures;
- pause for hours or days;
- recover after ambiguous network outcomes.

A dedicated durable workflow engine can solve many infrastructure concerns, but adopting one immediately would hide several important distributed-systems concepts the project is intended to demonstrate.

The project should demonstrate both:

1. understanding of durable workflow fundamentals;
2. ability to adopt mature workflow infrastructure when manual implementation stops being the best trade-off.

---

# 2. Decision

ResolveAI will evolve through two workflow-runtime stages.

## V0

Use an application-owned persisted state machine implemented in:

```text
.NET
+
PostgreSQL
```

Goals:

- understand state transitions;
- prove restart recovery;
- prove durable approval wait;
- prove optimistic concurrency;
- prove retry handling;
- prove idempotent continuation.

## V1

Adopt:

```text
Temporal
```

as the dedicated durable workflow engine.

Goals:

- long-running durable execution;
- retries/backoff;
- signals;
- timers;
- workflow history;
- worker recovery;
- production-grade orchestration.

---

# 3. Stable boundary

Business modules must not depend on the workflow-runtime implementation.

Conceptually:

```text
Host / application entry points
       ↓
Orchestration contracts
       ↓
workflow implementation
       ↓
PostgreSQL V0 / Temporal V1
```

The migration changes the internal orchestration mechanism, not the business architecture.

---

# 4. Workflow ownership

Orchestration owns:

- process state;
- process progression;
- waiting state;
- coordination;
- retry scheduling intent;
- failure state.

Orchestration does not own:

- authorization;
- tenant identity;
- policy rules;
- approval authority;
- LLM implementation;
- payment semantics;
- idempotency;
- audit authority.

---

# 5. Why not Temporal from V0

Starting directly with Temporal would reduce implementation work, but it would also make it easier to skip direct understanding and demonstration of:

- persisted state transitions;
- optimistic concurrency;
- restart recovery;
- explicit waiting state;
- duplicate resume handling;
- manual retry semantics.

These are valuable portfolio learning goals.

---

# 6. Why not keep the custom engine permanently

Once the project requires:

- durable timers;
- sophisticated retry scheduling;
- long-running coordination;
- workflow history;
- distributed workers;
- worker recovery;
- workflow versioning;

continuing to build orchestration infrastructure would become unnecessary reinvention.

These concerns are not ResolveAI's business differentiator.

---

# 7. Why Temporal for V1

Temporal is selected as the target V1 engine because its model fits ResolveAI's needs:

- long-running workflows;
- external signals;
- durable waits;
- activity retries;
- worker recovery;
- explicit workflow history;
- cloud-neutral architecture;
- active .NET support.

The decision does not make Temporal a domain dependency.

---

# 8. Alternatives considered

## Option A — Custom persisted workflow forever

Rejected for V1 because it risks recreating a workflow platform.

## Option B — Temporal from the first implementation

Rejected for V0 because it hides educational and portfolio-relevant fundamentals.

## Option C — Microsoft Durable Task

Considered viable, especially for a strongly Azure-centric portfolio.

Not selected as the primary V1 target because ResolveAI aims to demonstrate .NET backend engineering plus cloud-neutral AI/distributed-system architecture.

The choice may be revisited if the project focus becomes explicitly Azure-oriented.

---

# 9. Failure classification

The orchestration layer recognizes explicit failure categories:

```text
TransientTechnical
PermanentTechnical
BusinessRuleViolation
AuthorizationFailure
MalformedModelOutput
OutcomeUnknown
```

Retry behavior must depend on category.

No step retries indefinitely.

---

# 10. OutcomeUnknown rule

A timed-out side effect is not automatically a failed side effect.

Example:

```text
refund executed
response lost
```

is treated as:

```text
OutcomeUnknown
```

Execution performs idempotency/reconciliation.

Temporal or the V0 workflow engine must not blindly duplicate the mutation.

---

# 11. Approval rule

Decisioning owns approval validity.

Orchestration only:

- waits;
- receives the outcome;
- continues or stops.

Temporal signals in V1 transport/resume the workflow; they do not replace Decisioning's authority.

---

# 12. V0 completion criteria

V0 workflow architecture is complete when:

- workflow state survives restart;
- waiting approval survives restart;
- illegal state transitions are rejected;
- optimistic concurrency is tested;
- duplicate resume cannot duplicate side effects;
- retry exhaustion is handled;
- ambiguous refund outcome is safe.

---

# 13. V1 migration criteria

Temporal is introduced when:

- V0 vertical workflow is stable;
- business boundaries are stable;
- orchestration complexity becomes mostly infrastructure work;
- Temporal can be introduced without changing domain rules.

---

# 14. V1 completion criteria

The Temporal implementation is successful when:

- the same business workflow runs through Temporal;
- existing module contracts remain substantially unchanged;
- workflow survives worker restart;
- human approval resumes via signal/event;
- retries/backoff are durable;
- execution idempotency remains intact;
- workflow history is observable;
- business rules remain outside workflow definitions.

---

# 15. Consequences

## Positive

- demonstrates fundamentals before framework adoption;
- produces a strong architecture-evolution narrative;
- prevents premature infrastructure complexity;
- V1 gains mature durable-execution capabilities;
- domain boundaries remain stable.

## Negative

- V0 orchestration code is partially replaced later;
- requires discipline to avoid overbuilding the V0 engine;
- migration must be planned carefully;
- Temporal adds operational infrastructure in V1.

---

# 16. Guardrail

V0 must stop before becoming a general-purpose workflow engine.

If implementation work starts focusing on:

```text
generic timer scheduler
generic distributed task queues
generic workflow history engine
generic workflow replay framework
```

the project has crossed the boundary and should move to Temporal.

---

# 17. Verification

The architecture is verified by demonstrating both stages:

```text
V0:
kill application
restart
resume workflow safely
```

and later:

```text
V1:
kill Temporal worker
restart worker
workflow continues
```

with no duplicated business side effect.

---

# 18. Revisit triggers

Revisit Temporal as the V1 choice if:

- the project becomes explicitly Azure-focused;
- Temporal operational complexity outweighs portfolio value;
- .NET ecosystem constraints materially change;
- another workflow engine offers clearly better fit with the same architectural boundaries.

The orchestration contract must make such reconsideration possible.


---

## Baseline v1.1 clarification — 2026-09-15

Durability is introduced with each persisted behavior. Lost case-start or approval-resume notifications must be recovered through minimal outbox or explicit persisted-state reconciliation. Workflow checkpoint recovery must reuse module operation identities/results. The final V0 delivery verifies these guarantees end-to-end.
