# ResolveAI .NET — Architectural Drivers

**Status:** Accepted planning baseline v1.1  
**Version:** 1.1  

Architectural drivers are the requirements that materially influence system structure. Not every feature is an architectural driver.

---

## 1. Driver summary

| ID | Driver | Architectural consequence |
|---|---|---|
| AD-01 | LLM must not be an authority | deterministic enforcement boundary |
| AD-02 | High-risk side effects | Tool Gateway + policy + approval |
| AD-03 | Ambiguous outcomes/retries | idempotency + execution records |
| AD-04 | Long-running approval | persistent workflow/process state |
| AD-05 | Multi-tenancy | trusted tenant context + data isolation |
| AD-06 | Replaceable AI provider | internal LLM abstraction/adapters |
| AD-07 | Explainable operational decisions | provenance + audit + traces |
| AD-08 | AI regressions | eval dataset + regression gates |
| AD-09 | One-developer portfolio scale | modular architecture, limited operational complexity |
| AD-10 | Demonstrable resilience | fault injection + integration/E2E tests |

---

# 2. AD-01 — Probabilistic reasoning, deterministic enforcement

## Context

The model is useful for ambiguity:

- interpreting natural language;
- selecting relevant evidence;
- proposing actions.

It is not reliable enough to be the only enforcement mechanism for permissions, financial limits or irreversible effects.

## Architectural impact

We need distinct responsibilities:

```text
LLM / Agent
    ↓
Structured Proposal
    ↓
Backend Validation
    ↓
Policy
    ↓
Authorization
    ↓
Approval if needed
    ↓
Execution
```

### Required properties

- structured contracts;
- schema validation;
- deterministic policy code;
- no direct provider/database credential exposure to the model.

---

# 3. AD-02 — High-risk side effects

## Context

Refunds and similar actions have financial/contractual impact.

## Architectural impact

A mutation path must include:

```text
request
→ authorization
→ tenant validation
→ argument validation
→ policy decision
→ approval validation
→ idempotency
→ execution
→ audit
```

This is the reason a Tool Gateway/enforcement boundary exists.

---

# 4. AD-03 — Ambiguous network outcomes and duplicate execution

## Context

The payment provider may execute a refund successfully while the response is lost.

Retrying blindly can produce a second refund.

## Architectural impact

Critical side effects need:

- stable idempotency keys;
- durable execution result records;
- concurrency protection;
- retry-safe adapters;
- explicit ambiguous-outcome handling.

The architecture must assume **at-least-once attempts can occur**, even if the application tries to issue an action once.

---

# 5. AD-04 — Long-running human approval

## Context

A workflow may wait minutes, hours or longer for an operator.

In-memory orchestration is insufficient.

## Architectural impact

Process state must be persistent and resumable.

Required state includes at minimum:

- workflow/case identifier;
- current state;
- requested action;
- approval requirement;
- approval status;
- execution outcome;
- retry/failure metadata.

### Design consequence

V0 uses persisted application-owned workflow state, introduced with the first workflow behavior.

ADR-004 accepts Temporal for V1, after the V0 flow is stable and migration criteria are met.

---

# 6. AD-05 — Multi-tenancy

## Context

ResolveAI is multi-tenant and cross-tenant leakage is a critical security failure.

## Architectural impact

- tenant id comes from trusted auth/backend context;
- tenant filters are centralized/enforced;
- tools are tenant-scoped;
- knowledge retrieval is tenant-scoped;
- audit is tenant-aware;
- model output cannot choose tenant identity.

Optional database RLS is a defense-in-depth decision, not a substitute for application authorization.

---

# 7. AD-06 — Replaceable LLM provider

## Context

Providers, models, prices and capabilities change.

## Architectural impact

Business code depends on an internal contract, conceptually:

```text
IModelGateway
    GenerateAsync(...)
    GenerateStructuredAsync(...)
```

Provider-specific concerns stay in adapters/infrastructure.

### Success test

Swap the provider implementation and rerun the same eval suite without rewriting policy/domain/workflow code.

---

# 8. AD-07 — Auditability and provenance

## Context

The project must explain operationally what led to an action.

## Architectural impact

Important decisions must retain evidence such as:

- ticket/input reference;
- relevant customer/order facts;
- policy version;
- retrieved knowledge references;
- proposed action;
- backend policy outcome;
- approval decision;
- executed side effect;
- result;
- cost/latency identifiers.

This is not hidden chain-of-thought storage. It is operational evidence.

---

# 9. AD-08 — AI regression management

## Context

A prompt/model/retrieval change can improve one case and silently break others.

## Architectural impact

AI behavior is treated as a versioned, measurable system property.

Need:

- eval cases in source control;
- repeatable execution;
- deterministic graders where possible;
- security cases;
- baseline comparison;
- report artifacts.

---

# 10. AD-09 — Portfolio scale and developer capacity

## Context

The system is built primarily by one developer.

## Architectural impact

The architecture should maximize learning and evidence while minimizing accidental operational work.

Therefore:

- modular monolith is preferred initially;
- one primary relational database is preferred;
- local Docker composition may be used;
- microservices require a specific justification;
- message brokers require a specific asynchronous use case;
- Kubernetes is postponed.

---

# 11. AD-10 — Demonstrable resilience

## Context

Reliability claims should be demonstrated, not stated.

## Architectural impact

Failure modes must be injectable/testable.

Target scenarios:

```text
LLM timeout
LLM malformed output
payment timeout after execution
duplicate mutation attempt
application restart while awaiting approval
cross-tenant request
unauthorized tool invocation
```

---

# 12. Quality attribute scenarios

## QA-01 — Security

**Stimulus:** model proposes a refund above its allowed authority.  
**Response:** backend policy/approval boundary blocks direct execution.  
**Measure:** zero unauthorized execution.

## QA-02 — Recoverability

**Stimulus:** application restarts while a case waits for approval.  
**Response:** case remains recoverable and resumes after approval.  
**Measure:** no lost state / no duplicate action.

## QA-03 — Idempotency

**Stimulus:** refund execution response is lost and the operation is retried.  
**Response:** existing result is returned or the operation reconciles safely.  
**Measure:** one business refund.

## QA-04 — Tenant isolation

**Stimulus:** request attempts to read another tenant's order.  
**Response:** backend denies access independent of LLM content.  
**Measure:** zero cross-tenant data returned.

## QA-05 — Provider portability

**Stimulus:** model provider adapter is replaced.  
**Response:** domain/policy/workflow code remains unchanged.  
**Measure:** same build boundaries and successful regression suite.

## QA-06 — Observability

**Stimulus:** operator investigates a failed case.  
**Response:** trace and records show the key workflow steps, tool calls, policy decision and failure.  
**Measure:** end-to-end correlation exists.

---

# 13. Drivers that do NOT yet justify architecture complexity

The following are not current drivers:

- internet-scale throughput;
- independently scaling each domain;
- strict multi-region availability;
- dozens of engineering teams;
- independent service deployment cadence;
- event sourcing requirements.

Therefore they cannot currently justify a microservice-heavy architecture.
