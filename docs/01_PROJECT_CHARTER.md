# ResolveAI .NET — Project Charter

**Status:** Accepted planning baseline v1.1  
**Version:** 1.1  
**Decision scope:** project-level

---

## 1. Project identity

**Project name:** ResolveAI  
**Project type:** production-minded AI backend portfolio system  
**Primary implementation platform:** .NET  
**Primary engineering focus:** enterprise backend engineering + AI backend engineering

ResolveAI is a multi-tenant customer-operations platform that receives support requests, investigates cases using internal capabilities and knowledge, applies deterministic policies, proposes actions using an LLM and executes only actions permitted by backend controls.

The system is intentionally more than a chatbot.

---

## 2. Product objective

Build a support-operations system that can:

1. receive a customer support request;
2. identify the relevant customer, order, payment or shipment;
3. retrieve relevant policy evidence;
4. use an LLM for probabilistic interpretation and recommendation;
5. validate the proposed action deterministically;
6. require human approval where policy demands it;
7. execute allowed actions safely;
8. maintain durable workflow state;
9. record an audit trail;
10. expose enough observability to reconstruct what happened.

---

## 3. Portfolio objective

The project must demonstrate that the developer can build a modern AI-enabled system while retaining strong traditional backend engineering.

### .NET / backend evidence

The project should demonstrate:

- ASP.NET Core API design;
- domain modeling;
- dependency boundaries;
- persistence and transactions;
- authorization;
- concurrency handling;
- idempotency;
- background processing;
- resilience;
- integration patterns;
- testing;
- structured logging and tracing;
- containerized local development.

### AI engineering evidence

The project should demonstrate:

- structured LLM output;
- tool/function calling;
- provider abstraction;
- context engineering;
- RAG and provenance;
- prompt-injection defenses;
- human-in-the-loop;
- AI evals;
- model/prompt regression measurement;
- cost and latency measurement.

### Distributed-systems evidence

The project should demonstrate:

- durable state;
- retries;
- timeout handling;
- duplicate delivery handling;
- partial failures;
- eventual recovery;
- safe financial side effects.

---

## 4. Central system thesis

> **LLM proposes. Backend disposes.**

The LLM is allowed to:

- classify ambiguous intent;
- synthesize evidence;
- choose among exposed read capabilities;
- propose an action;
- explain a recommendation using available evidence.

The LLM is not allowed to be the authority for:

- authentication;
- tenant identity;
- authorization;
- financial limits;
- approval requirements;
- idempotency;
- transaction ownership;
- audit integrity;
- execution of high-risk side effects.

---

## 5. Primary business scenario

The canonical scenario is the **damaged-item refund**.

Example:

```text
Customer:
"I ordered a monitor last week.
It arrived broken.
I'd like a refund."
```

Expected system behavior:

```text
create ticket
    ↓
identify customer
    ↓
load order/payment
    ↓
retrieve refund policy
    ↓
LLM proposes refund
    ↓
backend validates proposal
    ↓
policy determines approval level
    ↓
human approves when required
    ↓
idempotent refund execution
    ↓
customer/CRM update
    ↓
audit + trace + metrics
```

This scenario is the reference vertical slice for early implementation.

---

## 6. Project stakeholders / actors

### Customer

Submits a support request and receives the outcome.

### Operator

Reviews cases requiring human intervention and approves/rejects actions.

### Administrator

Configures tenants, policies, users and selected system settings.

### AI Agent Runtime

Investigates and proposes actions but has no direct production authority.

### Backend

Owns business state, enforcement, execution and durable process state.

### External / simulated systems

Examples:

- CRM;
- Orders;
- Payments;
- Shipping;
- identity provider;
- LLM provider.

---

## 7. Success criteria

The project succeeds when it demonstrates a coherent end-to-end system rather than isolated AI demos.

Minimum success evidence includes:

- one complete support workflow;
- deterministic policy enforcement;
- human approval for a high-risk action;
- idempotent side effect;
- recovery after process interruption;
- structured LLM output;
- tenant-aware backend enforcement;
- auditable decision trail;
- measurable AI behavior;
- end-to-end traceability.

The stronger v1 target additionally includes:

- RAG with policy provenance;
- provider abstraction;
- AI regression suite;
- prompt-injection tests;
- fault injection;
- durable orchestration through the accepted Temporal V1 target.

MCP is an optional V1+ extension, after the Tool Gateway is stable.

---

## 8. Constraints

### C1 — portfolio scale

The architecture must be credible but implementable by one developer.

### C2 — .NET is the primary application platform

The main backend and business logic are to be implemented on .NET unless a later ADR establishes a compelling exception.

### C3 — AI provider must be replaceable

Business logic must not depend directly on a provider SDK.

### C4 — no direct LLM production access

No LLM credential or tool path may allow direct bypass of backend authorization/policy boundaries.

### C5 — tenant identity is backend-owned

Tenant context must originate from trusted identity/session data, not from model output.

### C6 — high-risk side effects require a safety story

For each mutation we must know:

- who may request it;
- who may approve it;
- whether it can be safely retried;
- whether it can be compensated;
- how it is audited;
- what happens on timeout.

### C7 — incremental delivery

Every milestone must leave the system in a runnable and demonstrable state.

---

## 9. Non-goals

ResolveAI is not intended to prove:

- hyperscale traffic handling;
- global multi-region deployment;
- fully autonomous financial authority;
- generic AGI-style multi-agent behavior;
- custom foundation-model training;
- complex frontend design;
- production Kubernetes operations in the first version.

These may be future extensions only if they add clear portfolio value.

---

## 10. Portfolio evidence principle

Each important architectural claim must have a corresponding artifact or test.

| Claim | Evidence |
|---|---|
| Refund is idempotent | Integration/E2E test with repeated request |
| Workflow state is durable | Restart/recovery scenario |
| Tenant isolation works | Cross-tenant negative tests |
| LLM is not authority | Unauthorized action test blocked by backend |
| Provider is replaceable | Adapter swap + regression run |
| Prompt injection is handled | Security eval case |
| Decisions are explainable | Evidence/provenance + audit record |
| System is observable | End-to-end trace for a ticket |

---

## 11. Initial delivery strategy

The first executable delivery combines repository/runtime foundation with useful case behavior:

```text
POST /api/cases
    → trusted development identity and tenant
    → persist SupportCase in PostgreSQL
    → minimal audit and trace
GET /api/cases/{id}
    → tenant-scoped response, including after restart
```

Prove isolation with two tenants and run build/integration checks in CI. Add synthetic business data next, then structured AI with the initial 10-case eval dataset. Introduce modules only when their first behavior is implemented.

The canonical V0 release gate and delivery sequence are in `13_IMPLEMENTATION_ROADMAP.md`. Advanced RAG, Temporal, OIDC infrastructure and UI follow V0; MCP remains optional V1+.

---

## 12. Charter acceptance

The charter is accepted when the following statements are agreed:

- ResolveAI remains a backend-first project.
- .NET is the main implementation platform.
- AI is a replaceable dependency, not the system of record.
- Safety and reliability properties are part of the product definition.
- Portfolio evidence matters as much as feature implementation.
- Complexity will be introduced only after an explicit architectural reason exists.
