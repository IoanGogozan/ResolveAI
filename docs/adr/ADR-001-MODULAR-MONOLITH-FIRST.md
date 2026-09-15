# ADR-001 — Start ResolveAI as a Modular Monolith

**Status:** Accepted  
**Date:** 2026-09-15  
**Decision owners:** project architecture  
**Scope:** application/deployment architecture

---

## 1. Context

ResolveAI contains several distinct business and technical capabilities:

- support tickets;
- customers;
- orders;
- payments;
- shipping;
- workflows;
- approvals;
- policy enforcement;
- AI/agent runtime;
- knowledge/RAG;
- audit;
- evals.

These boundaries are real and should be modeled explicitly.

However, the project is primarily implemented by one developer and does not currently have drivers such as:

- multiple independently operating teams;
- different deployment cadences;
- large scale differences between domains;
- regulatory isolation requiring separate processes;
- strict independent availability requirements.

Starting with many microservices would add:

- distributed transactions;
- network failure modes;
- service discovery;
- contract deployment/versioning;
- observability complexity;
- local development overhead;
- more CI/CD work;
- more containers and operational work.

That complexity could hide the actual goals: .NET backend architecture, safe AI integration, reliability and demonstrable engineering.

---

## 2. Decision

ResolveAI will start as a **modular monolith**.

One deployable backend host may contain multiple strongly separated modules.

Conceptually:

```text
ResolveAI.Host
│
├── Tickets
├── CustomerOperations
├── Orders
├── Payments
├── Workflows
├── Approvals
├── Policies
├── Agent
├── Knowledge
└── Audit
```

This historical sketch is not the current module list. ADR-002 finalizes the capability boundaries; document 12 owns their compile-time graph. CRM, Orders, Payments and Shipping remain external integrations.

The important decision is:

> We enforce boundaries in code before introducing distributed deployment boundaries.

---

## 3. Boundary expectations

Modules should:

- expose explicit application contracts;
- hide internal persistence details;
- avoid reaching directly into another module's tables/repositories;
- avoid provider SDK leakage;
- communicate through explicit calls/contracts and, where justified, domain/integration events.

Architecture tests should eventually enforce selected dependency rules.

---

## 4. Data decision in this ADR

This ADR does **not** require a separate database per module.

Initial direction:

- one PostgreSQL instance;
- clear data ownership;
- schema/table conventions that make module ownership visible.

Whether schemas-per-module are useful will be handled in the data architecture decision.

---

## 5. Why not microservices first?

### Rejected as default

Microservices are not selected merely to demonstrate "enterprise architecture."

Current disadvantages outweigh benefits:

- too much infrastructure relative to portfolio value;
- harder transactional consistency;
- much larger failure surface;
- slower development of the end-to-end AI safety scenario;
- increased test complexity.

### Important nuance

This ADR does not say microservices are bad.

It says the project currently lacks sufficient drivers for them.

---

## 6. Why not one unstructured monolith?

Also rejected.

A single project with arbitrary dependencies would make later changes difficult and would not demonstrate architecture discipline.

The target is:

```text
single deployment
≠
single undifferentiated codebase
```

---

## 7. Consequences

### Positive

- simpler local development;
- fewer operational dependencies;
- easier debugging;
- simpler transactions;
- faster vertical slices;
- clear place to demonstrate .NET modular design;
- future extraction remains possible when boundaries are strong.

### Negative

- modules cannot scale independently;
- deployment is coupled;
- poor discipline could degrade into a big ball of mud;
- architecture tests/conventions are needed to protect boundaries.

---

## 8. Extraction criteria

A module may be considered for service extraction only if one or more concrete drivers emerge, for example:

- independent scaling requirement;
- materially different availability requirement;
- independent security boundary;
- independent release lifecycle;
- external integration that benefits from isolation;
- fault containment requirement;
- explicit portfolio goal to demonstrate distributed-service communication after the core system is already stable.

---

## 9. Verification

This decision is successful if:

- V0 can be built with minimal infrastructure;
- module responsibilities remain understandable;
- architecture tests can enforce important dependencies;
- provider/infrastructure details do not leak into business modules;
- later extraction does not require rewriting the business model.

---

## 10. Revisit trigger

Revisit this ADR when:

- the first complete vertical workflow is stable;
- domain boundaries are documented;
- there is a real requirement that one module cannot satisfy within the modular-monolith constraints.

Do not revisit merely because "microservices would look better on the diagram."
