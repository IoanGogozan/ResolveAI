# ResolveAI .NET — Scope & Requirements

**Status:** Accepted planning baseline v1.1  
**Version:** 1.1  

---

## 1. Scope model

The project is divided into three delivery levels:

- **V0 — Walking Skeleton:** prove the complete vertical path with minimal infrastructure.
- **V1 — Portfolio-Complete System:** implement the architecture qualities that define ResolveAI.
- **Post-V1 — Optional Extensions:** add complexity only when it demonstrates a specific additional competency.

This separation prevents infrastructure work from overwhelming the core product.

---

# 2. V0 — Walking Skeleton

## 2.1 Functional scope

### FR-V0-01 — Create support ticket

The API accepts a support ticket and persists it.

### FR-V0-02 — Load synthetic business data

The system can load synthetic customer, order and payment data from PostgreSQL.

### FR-V0-03 — Generate structured AI proposal

The backend calls one configured LLM provider and receives a schema-validated structured response.

Example conceptual result:

```json
{
  "action": "refund",
  "amount": 649,
  "currency": "EUR",
  "reason": "damaged_item",
  "confidence": 0.94
}
```

### FR-V0-04 — Expose basic read tools/capabilities

Minimum:

- get customer;
- get order;
- get payment.

### FR-V0-05 — Deterministic approval rule

A backend-owned policy determines whether the proposed refund requires approval.

### FR-V0-06 — Persist process state

Ticket, AI decision and workflow status must survive application restart.

### FR-V0-07 — Minimal audit record

Important state transitions and decision events are recorded.

### FR-V0-08 — Initial eval dataset

Create at least 10 representative cases with the first AI delivery. Keep deterministic provider substitutes in normal CI; run real-provider evals separately with bounded cost, versioned inputs and saved reports.

### FR-V0-09 — Approval and safe execution

Implement persistent human approval when required, action binding and authority checks; execute refunds with durable idempotency, request fingerprints and payment-level concurrency protection.

### FR-V0-10 — Recovery across commits

Recover after interruption between case creation and workflow start, approval commit and resume, and external execution and local result persistence. Critical handoffs need a minimal transactional outbox or explicit reconciliation of persisted states; in-memory notification alone is insufficient.

### FR-V0-11 — Payment-level invariant

Concurrent independent cases/workflows for the same payment must not over-refund. The simulated Payments system atomically enforces the remaining refundable amount, persists its ledger across restart and supports idempotent execution/status lookup.

### FR-V0-12 — Audit and observability from first use

Persist minimal audit facts from the first business mutation and correlate logs/traces from Delivery 1. Audit delivery must survive a crash after the originating change commits.

---

## 2.2 Explicitly out of V0

- Temporal or another dedicated durable workflow platform;
- MCP;
- multiple LLM providers;
- complex RAG;
- Kafka;
- Kubernetes;
- microservice decomposition;
- advanced operator UI;
- policy-as-code engine;
- semantic caching.

---

# 3. V1 — Portfolio-Complete System

## 3.1 Ticket & case management

### FR-001

Create, view and track support tickets.

### FR-002

Associate a ticket with trusted tenant and customer context.

### FR-003

Expose current workflow/case state to the operator.

---

## 3.2 AI investigation

### FR-010

The agent runtime can request approved read capabilities.

### FR-011

Tool requests and outputs use explicit schemas.

### FR-012

Model output is validated before business code consumes it.

### FR-013

The system stores model invocation metadata required for observability/evaluation without depending on hidden chain-of-thought.

---

## 3.3 Policy enforcement

### FR-020

Refund rules are implemented deterministically outside prompts.

### FR-021

A policy decision can produce at least:

```text
ALLOW
REQUIRE_APPROVAL
BLOCK
```

### FR-022

The decision records the policy/version or rule set used.

---

## 3.4 Human approval

### FR-030

High-risk actions can enter a persistent waiting state.

### FR-031

Authorized operators can approve or reject.

### FR-032

Execution can resume after approval even if the application was restarted while waiting.

### FR-033

Approval is bound to the intended action and cannot be reused for a materially different mutation.

---

## 3.5 Tool Gateway

### FR-040

All AI-requested business capabilities pass through an enforcement boundary.

### FR-041

For each tool the backend knows:

- name/version;
- input schema;
- output schema;
- risk level;
- required permission;
- timeout;
- retry policy;
- idempotency behavior;
- audit policy;
- tenant scope.

### FR-042

High-risk side effects cannot execute if authorization, policy or approval requirements fail.

---

## 3.6 Refund execution

### FR-050

The payment/refund operation is idempotent.

### FR-051

Retry after an ambiguous timeout must not create a second refund.

### FR-052

Concurrent requests for the same payment/action must not create duplicate business effects.

---

## 3.7 Knowledge & RAG

### FR-060

Policy documents can be ingested with metadata.

### FR-061

Retrieval supports policy lookup relevant to a support case.

### FR-062

Important AI-supported decisions retain provenance:

- document identity;
- version;
- relevant chunks/evidence;
- validity metadata where applicable.

---

## 3.8 Multi-tenancy

### FR-070

Every tenant-owned business entity is associated with a tenant.

### FR-071

Tenant context comes from trusted backend identity/context.

### FR-072

Tenant A can never access Tenant B resources through APIs, tool calls or retrieval.

---

## 3.9 Audit

### FR-080

Important decisions and side effects produce append-oriented audit events.

### FR-081

Audit data records enough context to answer:

- who/what requested the action;
- which resource was affected;
- which policy was applied;
- whether approval existed;
- what result occurred.

---

## 3.10 LLM provider abstraction

### FR-090

Business modules do not directly import/use provider-specific SDKs.

### FR-091

The LLM boundary supports structured generation.

### FR-092

A provider adapter can be replaced without rewriting business policy/workflow code.

---

## 3.11 Evals

### FR-100

The system maintains a versioned eval dataset.

### FR-101

V1 targets at least 100 representative cases.

### FR-102

The suite covers:

- intent/classification;
- tool selection;
- tool arguments;
- policy compliance;
- final outcome;
- retrieval quality;
- provenance;
- prompt injection;
- tenant isolation;
- malformed output;
- provider failure;
- cost/latency.

### FR-103

Changes to models/prompts/retrieval can be compared against a baseline.

---

## 3.12 Observability

### FR-110

A ticket can be traced end-to-end.

### FR-111

Important spans/events include correlation identifiers such as:

- trace id;
- ticket id;
- workflow id;
- tenant id;
- agent run id;
- model invocation id;
- tool call id.

### FR-112

The system measures AI cost/usage and latency.

### FR-113

Logs and traces must apply explicit PII/secrets redaction rules.

---

## 3.13 Failure engineering

### FR-120

The application supports controlled failure scenarios for demonstration/testing.

Minimum target scenarios:

- payment timeout;
- LLM timeout/429;
- malformed model output;
- duplicate execution attempt;
- process restart while waiting for approval.

### FR-121

A partial failure must not be reported as a complete success.

---

# 4. Non-functional requirements

## NFR-001 — Security

Unauthorized high-risk actions must be blocked by deterministic backend checks.

**Acceptance evidence:** negative integration/E2E tests.

---

## NFR-002 — Tenant isolation

Cross-tenant access must fail regardless of model behavior.

**Acceptance evidence:** automated cross-tenant test suite.

---

## NFR-003 — Idempotency

Critical financial mutations must tolerate retries and duplicates.

**Acceptance evidence:** timeout/retry and concurrent duplicate tests.

---

## NFR-004 — Recoverability

Persistent workflow/case state must survive application restart.

**Acceptance evidence:** restart while waiting and resume scenario.

---

## NFR-005 — Auditability

The system must reconstruct the important decision/execution chain without relying on hidden model reasoning.

**Acceptance evidence:** audit + workflow history + evidence/provenance.

---

## NFR-006 — Provider replaceability

A provider change must not force a rewrite of domain/policy code.

**Acceptance evidence:** adapter replacement exercise.

---

## NFR-007 — Testability

External providers/services must have test substitutes and deterministic boundaries where practical.

---

## NFR-008 — Observability

Every critical path must have structured logs/traces sufficient to diagnose failure and latency.

---

## NFR-009 — Maintainability

Architecture boundaries must be enforceable in code and tests.

---

## NFR-010 — Performance

Initial project performance goals are portfolio-scale, not hyperscale.

We will define concrete latency/SLO targets only after the first working baseline is measured.

---

# 5. Scope priorities

## P0 — must exist for the project thesis

- support ticket flow;
- structured model output;
- deterministic policy enforcement;
- Tool Gateway boundary;
- approval;
- idempotency;
- persistent state;
- audit;
- tenant isolation;
- test/eval evidence.

## P1 — expected for strong v1

- RAG with provenance;
- provider abstraction and replacement demo;
- OpenTelemetry tracing;
- failure injection;
- operator UI;
- richer eval suite;
- Temporal durable orchestration, as accepted in ADR-004;
- OIDC identity.

## P2 — optional extension

- MCP;
- second provider live routing/fallback;
- message broker;
- PostgreSQL RLS;
- OPA/Cedar;
- advanced dashboards;
- Kubernetes.

Some P2 items may move into V1 later if an ADR establishes that they are necessary to demonstrate a target competency.

---

# 6. Explicit non-goals for initial implementation

The initial implementation will not attempt to optimize for:

- millions of requests/day;
- dozens of independently deployable microservices;
- multi-region active-active;
- full event sourcing;
- complex CQRS;
- distributed transactions;
- autonomous irreversible AI actions;
- fine-tuning;
- custom model training;
- large agent swarms.

---

# 7. Definition of Done — V0

The canonical release checklist is `13_IMPLEMENTATION_ROADMAP.md`, section 10. V0 requires all of it, including approval, refund execution and recovery; persisted proposals alone do not complete V0.

Requirement mapping:

| Capability | Requirements |
|---|---|
| Case intake and trusted business facts | FR-V0-01, FR-V0-02, FR-V0-04 |
| Validated AI proposal and evals | FR-V0-03, FR-V0-08 |
| Deterministic policy and bound approval | FR-V0-05, FR-V0-09 |
| Durable workflow and critical handoffs | FR-V0-06, FR-V0-10 |
| Idempotent execution and payment-level concurrency | FR-V0-09, FR-V0-11 |
| Audit, tracing and failure evidence | FR-V0-07, FR-V0-12, NFR-001 through NFR-009 |

---

# 8. Definition of Done — V1

V1 is complete when the project can demonstrate:

- [ ] real end-to-end workflow;
- [ ] structured LLM output;
- [ ] no high-risk side effect directly from LLM;
- [ ] Tool Gateway validation/auth/policy/audit;
- [ ] idempotent critical side effects;
- [ ] recoverable persistent workflow state;
- [ ] human approval;
- [ ] Temporal workflow/worker recovery;
- [ ] OIDC identity with validated tenant membership;
- [ ] RAG with provenance;
- [ ] tenant isolation;
- [ ] prompt-injection defense test;
- [ ] 100+ eval cases;
- [ ] cost and latency measurement;
- [ ] distributed tracing;
- [ ] provider abstraction;
- [ ] provider replacement without business-layer rewrite;
- [ ] fault injection demo;
- [ ] architecture documentation and reproducible demo instructions.

MCP remains a candidate V1+/extension capability until its value relative to the core portfolio goal is reviewed.


---

# 9. Scenario rules to resolve before Decisioning/refund implementation

The damaged-item refund is the initial scenario. Complete the following as concrete rules and acceptance examples before Delivery 4:

- Full vs partial refunds and how earlier refunds affect the remaining amount.
- Currency, monetary precision/rounding and amount validation.
- Eligibility evidence, time window and behavior when required facts are missing.
- Automatic allowance vs standard/senior approval, including exact threshold boundaries.
- Approval expiry, changed action/resource/policy and whether re-evaluation is required.
- Handling duplicate cases for the same payment and business refund intent.

Amounts, thresholds and confidence scores elsewhere in this pack are examples, not approved business rules. Model confidence never grants authority. These choices do not block Delivery 1.
