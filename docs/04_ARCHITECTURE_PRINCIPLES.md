# ResolveAI .NET — Architecture Principles

**Status:** Accepted planning baseline v1.1  
**Version:** 1.1  

These principles are intended to remain stable even if libraries, providers or infrastructure change.

---

## AP-01 — LLM proposes; backend disposes

An LLM may propose an action.

Only trusted backend code may authorize, validate and execute it.

---

## AP-02 — Probabilistic reasoning, deterministic enforcement

Use AI where ambiguity is valuable.

Use deterministic code for:

- authentication;
- authorization;
- tenant identity;
- business limits;
- approval rules;
- idempotency;
- side-effect execution;
- audit integrity.

---

## AP-03 — The model is a dependency, not the product core

Provider SDKs and model names must not leak into domain/business modules.

The business system should survive a provider replacement.

---

## AP-04 — Every side effect needs a safety story

For every mutation, answer:

1. who can request it?
2. who can approve it?
3. what policy applies?
4. can it be retried safely?
5. can it be compensated/reconciled?
6. how is it audited?
7. what happens on timeout?
8. what concurrency rule applies?

If these questions are unanswered, the side effect is not ready.

---

## AP-05 — Tenant identity is trusted context, never model output

The AI can use tenant-scoped data, but it cannot decide which tenant it represents.

---

## AP-06 — Tool access is capability access

A tool is not merely a function exposed to an LLM.

It is a backend capability with:

- contract;
- permission;
- risk classification;
- policy;
- timeout;
- retry behavior;
- audit semantics;
- tenant scope.

---

## AP-07 — High-risk mutations are never direct AI side effects

Conceptually:

```text
AI
↓
Proposal
↓
Tool Gateway
↓
Policy
↓
Approval
↓
Business Service
```

Not:

```text
AI → payment provider
```

---

## AP-08 — Persistence before orchestration cleverness

Important process state must survive restart before advanced orchestration frameworks are introduced.

Durability is a property, not a product name.

---

## AP-09 — Idempotency is a business invariant

Critical operations do not become safe because the HTTP client has retries disabled.

Idempotency must be enforced at the execution boundary. Independent workflows also require payment-level protection against over-refund; a workflow-scoped key alone does not provide that protection.

---

## AP-10 — Explicit contracts at boundaries

Use explicit contracts between:

- API and application;
- application and AI gateway;
- agent/tool boundary;
- domain and external adapters;
- asynchronous messages if introduced.

Avoid loosely typed dictionaries as long-lived internal contracts.

---

## AP-11 — Domain code should not know infrastructure details

Domain/business rules should not depend directly on:

- OpenAI SDK;
- EF Core APIs;
- RabbitMQ/Kafka APIs;
- Redis APIs;
- telemetry exporters.

Infrastructure implements contracts owned by inner/application layers.

---

## AP-12 — Start modular, distribute only with evidence

Deployment boundaries are not the same as code boundaries.

We will first enforce domain/module boundaries in one deployable application.

A module becomes a separate service only when a concrete driver exists.

---

## AP-13 — One source of truth per business concept

Ownership of mutable business state must be explicit.

Avoid multiple components independently mutating the same concept.

---

## AP-14 — Prefer transactional correctness over premature async complexity

A local transaction is preferable when it correctly solves the problem.

Messaging is introduced when there is a genuine need for:

- temporal decoupling;
- fan-out;
- asynchronous processing;
- independent failure/retry;
- integration boundary.

---

## AP-15 — Observability is part of the design

Important operations should emit:

- structured logs;
- traces;
- metrics;
- domain/audit events where appropriate.

Correlation identifiers start in Delivery 1. Minimal audit starts with the first business mutation; later deliveries complete coverage and presentation.

---

## AP-16 — Do not log sensitive context by default

Prompts, retrieved documents, customer data and tool arguments may contain sensitive information.

Logging them requires an explicit redaction/data-handling policy.

---

## AP-17 — Evidence over hidden reasoning

The system should explain decisions using stored facts, policy references, tool results and workflow history.

It should not depend on storing hidden chain-of-thought.

---

## AP-18 — Evals are regression tests for AI behavior

Every meaningful prompt/model/retrieval change should be measurable against a baseline.

Manual chat inspection is insufficient.

---

## AP-19 — Build vertical slices

A milestone is complete only when it produces a demonstrable end-to-end behavior.

Avoid milestones that only "install infrastructure."

---

## AP-20 — Complexity must earn its place

Before adding a framework/service, record:

```text
Problem:
Why current solution is insufficient:
Options:
Trade-offs:
Decision:
How we will verify the benefit:
```

This is the trigger for an ADR when the decision is architecturally significant.

---

## AP-21 — Prefer boring infrastructure for the core

The interesting part of ResolveAI should be the engineering problem, not operational novelty.

Stable, well-understood components are preferred unless a more complex component demonstrates an intentional competency.

---

## AP-22 — Security boundaries must remain valid when the model is wrong

Assume the model can:

- hallucinate;
- be manipulated;
- produce malformed output;
- choose the wrong tool;
- supply malicious/untrusted arguments.

The backend must remain safe under these conditions.

---

## AP-23 — Failure is a normal state

External dependencies fail.

Design explicitly for:

- timeout;
- retry;
- duplicate;
- unavailable provider;
- malformed response;
- restart;
- partial completion.

---

## AP-24 — Portfolio architecture must be explainable

Every major architecture decision should be explainable in an interview in terms of:

- context;
- alternatives;
- decision;
- trade-offs;
- evidence.

If a decision cannot be explained without saying "because it is enterprise," it is probably weak.
