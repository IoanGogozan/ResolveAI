# ResolveAI .NET — Implementation Roadmap & Milestones

**Status:** Accepted planning baseline v1.1  
**Version:** 1.1  
**Date:** 2026-09-15

# 1. Delivery strategy

Build small runnable deliveries. Each adds useful behavior, its persistence/recovery, minimal audit, tracing and appropriate tests. Deliveries describe scope, not fixed calendar sprints. The architecture is accepted; unchecked criteria still require implementation evidence.

# 2. V0 target

```text
case → trusted tenant → business facts → structured AI proposal
→ deterministic policy → human approval when required
→ idempotent refund → durable workflow → audit and trace
```

V0 uses one backend host and PostgreSQL. Temporal, RAG, OIDC provider infrastructure and UI follow in V1. MCP, messaging brokers, Redis and Kubernetes remain optional extensions.

# 3. Delivery 1 — Foundation & Case Intake

Implement:

- Git setup and connection to the new repository; organize plans in docs with working links.
- Pinned .NET SDK, central package versions, build settings, editor settings and ignore rules.
- Host, small Platform, CaseManagement and minimal Audit; create other projects only with real behavior.
- PostgreSQL via Docker Compose; module-owned migrations/history.
- Development/test authentication restricted to those environments, with two tenants and known users.
- Trusted immutable actor/tenant context; POST /api/cases and GET /api/cases/{id}.
- Health endpoint, structured logs, trace correlation and minimal durable case-created audit.
- Integration and architecture checks in GitHub Actions; README with local startup/demo commands.

Definition of Done:

- [ ] Host and PostgreSQL start from documented steps; build and CI pass.
- [ ] Case creation/read works and persists across restart.
- [ ] Tenant comes from trusted identity; cross-tenant lookup is denied.
- [ ] Development authentication cannot activate outside supported development/test environments.
- [ ] Migrations work on an empty database; integration tests use real PostgreSQL.
- [ ] Audit survives a crash between case commit and audit delivery; duplicate delivery is harmless.
- [ ] Architecture checks enforce no module reference to Host and no foreign persistence access.

# 4. Delivery 2 — Business Facts & Read Capabilities

Implement synthetic CRM, Orders and Payments data in PostgreSQL, behind module-owned read ports and Integrations adapters. Execution exposes get_customer, get_order and get_payment with permission/resource/tenant checks. Treat simulated systems as external domains, with separately owned simulation persistence. Persist simulation data across restarts.

Definition of Done:

- [ ] A damaged-monitor case resolves its customer, order and payment through contracts.
- [ ] Missing or mismatched references produce explicit outcomes.
- [ ] Cross-tenant tools and incorrect customer/order/payment associations are blocked.
- [ ] No core module directly accesses simulation tables or provider DTOs.
- [ ] The flow remains runnable without AI; new operations are correlated in traces.

# 5. Delivery 3 — Structured AI Proposal & Initial Evals

Implement AI, its internal LLM Gateway, one provider adapter, schema validation, persisted proposal/invocation metadata and Evals tooling. Approved AI reads pass through Execution. Introduce at least 10 versioned cases, including missing facts, malformed output, provider failure and prompt injection. Proposed amounts/confidence do not grant business authority.

Definition of Done:

- [ ] A case and trusted facts produce a validated structured proposal.
- [ ] Provider types stay inside AI infrastructure; invalid output is rejected.
- [ ] Prompt/model versions, usage and latency are recorded without default sensitive-context logging.
- [ ] Deterministic provider substitutes run in normal CI.
- [ ] Real-provider evals run separately with invocation/cost limits and saved comparable reports.
- [ ] Persisted operation identity/results support safe continuation after interruption.

# 6. Delivery 4 — Decisioning, Approval & Persistent Waiting

First specify scenario rules from requirements section 9: full/partial refunds, currency/precision, eligibility, exact approval thresholds, expiry and duplicate business intent. Examples in this pack are not binding policy.

Implement Decisioning policy evaluation (ALLOW / REQUIRE_APPROVAL / BLOCK), policy version, action fingerprint, approval endpoints and the initial persisted Orchestration flow. Orchestration coordinates CaseManagement, AI and Decisioning through public contracts.

Definition of Done:

- [ ] Business policy is deterministic; AI cannot approve its own proposal.
- [ ] Standard/senior authority, tenant, expiry and action binding are enforced.
- [ ] Approval/rejection and workflow transitions use concurrency protection.
- [ ] WaitingForApproval survives restart without retaining a request, transaction or Task.
- [ ] Case commit before workflow start and approval commit before resume recover safely.
- [ ] Minimal outbox or persisted-state reconciliation is documented for each critical handoff.
- [ ] Repeated module calls/checkpoint recovery cannot duplicate approval requests.
- [ ] Policy and approval audit facts survive interrupted publication.

# 7. Delivery 5 — Safe Refund & Recovery

Implement refund execution with executor permission, tenant/resource checks, current policy/approval validation, stable operation key, request fingerprint, persisted result and bounded retries. AI can request/propose only; the trusted executor requires payments.refund.execute.

The simulated Payments ledger must atomically enforce the remaining refundable amount, persist across restart, deduplicate operation keys and support outcome lookup. Two different workflow keys do not bypass payment-level rules.

Definition of Done:

- [ ] One permitted refund succeeds; repeated same operation produces one business effect.
- [ ] Reusing a key with different arguments is rejected.
- [ ] Concurrent duplicate requests and independent workflows for one payment cannot over-refund.
- [ ] Timeout after payment processing enters OutcomeUnknown and reconciles without blind mutation retry.
- [ ] Crash after provider processing or local execution commit recovers through stable identity/result lookup.
- [ ] Stale/mismatched approval, wrong tenant and missing execute permission block execution.
- [ ] Retry limits and manual-review/failure outcomes are visible.
- [ ] Execution and final case outcome have durable audit and trace evidence.

# 8. Delivery 6 — End-to-End V0 Verification

Complete and verify the workflow, using the canonical state model in workflow architecture section 6, including rejection, cancellation and manual review. Exercise interruptions at handoffs and external side effects. This delivery verifies durability already built in earlier deliveries.

Definition of Done:

- [ ] Submit → investigate → propose → decide → approve → refund → complete works.
- [ ] Illegal transitions, concurrent transitions and duplicate resume are tested.
- [ ] Restart while waiting and interruption after approval commit both recover.
- [ ] Crash between module result commit and workflow checkpoint cannot duplicate business effects.
- [ ] Failure/partial outcomes are explicit; uncertain execution is not reported as success.
- [ ] Audit and tracing reconstruct the full case and its retry history.
- [ ] Initial eval suite and all required deterministic checks pass.

# 9. Cross-cutting delivery rules

- Minimal audit and tracing begin in Delivery 1 and grow with each behavior.
- Persist source facts atomically with their owning module changes; deliver audit reliably and deduplicate append by source event identity.
- Critical handoffs use minimal outbox or persisted-state reconciliation, without a broker requirement.
- Add only the modules/infrastructure needed by the current delivery.
- Choose numeric business rules before Delivery 4; choose concrete retry limits before enabling retries.
- Use real PostgreSQL integration tests for constraints, migrations and concurrency.

# 10. Canonical V0 Release Gate

V0 is complete only when every item below has demonstrable evidence. Requirements section 7 maps to this checklist.

- [ ] Documented local startup and green CI from a clean database.
- [ ] Trusted tenant case intake and customer/order/payment lookup.
- [ ] Validated structured AI proposal and deterministic backend policy.
- [ ] Bound human approval with authority, tenant and expiry checks.
- [ ] Restart while waiting, then approval and successful continuation.
- [ ] Recovery after case commit before workflow start and approval commit before resume.
- [ ] One refund effect for repeated/concurrent requests for the same operation.
- [ ] Independent workflows for one payment cannot exceed its refundable amount.
- [ ] Safe reconciliation after payment processed but response/local result was lost.
- [ ] Recovery after module result commit before workflow checkpoint; duplicate resume is harmless.
- [ ] Unauthorized/cross-tenant actions and materially changed requests are blocked.
- [ ] Legal state transitions, bounded retries and visible rejection/failure/manual-review outcomes.
- [ ] Durable audit delivery and end-to-end trace with requester, approver and executor provenance.
- [ ] At least 10 real-provider eval cases with versioned reports and cost limits; deterministic regression checks pass separately.

Only after this gate introduce major V1 infrastructure.

---

# 11. V1 Milestone 1 — Knowledge & RAG

## Goal

Replace hard-coded/minimal policy context with real retrieval and provenance.

## Create

```text
ResolveAI.Knowledge
```

## Implement

```text
KnowledgeDocument
DocumentVersion
KnowledgeChunk
pgvector
RetrieveEvidence
EvidenceReference
```

AI receives retrieved evidence through Knowledge contracts.

Decisioning remains the authority.

## Definition of Done

- [ ] relevant policy document is retrieved;
- [ ] version/provenance is stored;
- [ ] cross-tenant retrieval is blocked;
- [ ] decision/demo can identify evidence used.

---

# 12. V1 Milestone 2 — Production-like Identity

## Goal

Replace development authentication with a standards-based provider.

## Implement

Preferred:

```text
Keycloak
+
OIDC
```

Maintain normalized execution context.

## Definition of Done

- [ ] user authenticates through OIDC;
- [ ] tenant membership is validated;
- [ ] operator vs senior operator permissions work;
- [ ] business modules remain independent of provider-specific claims.

---

# 13. V1 Milestone 3 — Temporal Migration

## Goal

Replace the V0 persisted state-machine runtime with Temporal while preserving domain contracts.

## Implement

```text
RefundCaseWorkflow
activities
signals
retry policies
worker recovery
workflow history
```

Existing module responsibilities remain unchanged.

## Definition of Done

- [ ] same damaged-monitor scenario runs through Temporal;
- [ ] worker restart does not lose progress;
- [ ] approval resumes via durable signal/event;
- [ ] retries/backoff are durable;
- [ ] Execution still owns idempotency;
- [ ] Decisioning still owns approval authority.

---

# 14. V1 Milestone 4 — Evals & Observability Hardening

## Goal

Turn AI behavior into measurable regression evidence and prove that the main infrastructure boundaries remain replaceable and failure-tolerant.

## Expand eval suite

Target:

```text
100+ cases
```

Categories:

```text
intent
tool choice
tool arguments
policy compliance
prompt injection
tenant isolation
retrieval quality
provider failure
malformed output
cost
latency
```

Add dashboards for:

```text
success rate
policy compliance
unauthorized actions
token usage
cost
p50/p95 latency
```

Add two explicit architecture verification exercises:

```text
Provider replacement proof
- replace/swap the LLM provider adapter without rewriting business, policy or workflow code
- no multi-provider routing/fallback is required for this proof

Fault injection
- LLM/provider timeout or failure
- payment timeout / ambiguous result
- workflow/worker restart
- unavailable external dependency
```

## Definition of Done

- [ ] model/prompt change can be compared with baseline;
- [ ] unauthorized-action regression is visible;
- [ ] traces cover end-to-end workflow;
- [ ] cost and latency are measurable;
- [ ] provider adapter replacement is demonstrated without business-layer rewrite;
- [ ] fault-injection scenarios are demonstrated and produce safe/recoverable outcomes.

---

# 15. V1 Milestone 5 — Portfolio UI & Final Demo

## Goal

Make the architecture easy to demonstrate.

Minimal operator UI:

```text
case details
workflow status
retrieved evidence
AI proposal
policy decision
approval
execution result
audit timeline
```

Use:

```text
React + TypeScript
```

Keep frontend scope small.

## Definition of Done

A reviewer can understand the entire flow without reading database tables or logs manually.

---

# 16. Optional V1+ Milestones

Only after the core system is stable:

```text
MCP
second LLM provider / fallback
RabbitMQ + MassTransit
Redis
policy-as-code
advanced model routing
Kubernetes
```

Each requires a concrete architectural reason.

---

# 17. Milestone dependency order

```text
1. Foundation + case intake + audit/tracing/CI
2. Business facts and read capabilities
3. Structured AI and initial evals
4. Policy, approval and persistent orchestration
5. Safe refund and recovery
6. End-to-end V0 verification
→ canonical V0 release gate
→ Knowledge/RAG → OIDC → Temporal
→ evals/observability hardening → portfolio UI
```

# 18. Scope guardrails

Do not scaffold unused projects, build a generic workflow engine, introduce infrastructure before its feature, or build the operator UI before the backend flow is stable. Temporal remains the accepted V1 target, introduced under ADR-004 migration criteria. MCP remains optional V1+.

# 19. First coding target

```text
POST /api/cases → trusted tenant → persist SupportCase + durable audit source
GET /api/cases/{id} → tenant-scoped case, including after restart
```

Prove this with two tenants, real PostgreSQL integration tests and CI. Then add business data, followed by structured AI.
