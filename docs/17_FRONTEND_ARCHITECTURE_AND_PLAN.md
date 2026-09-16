# ResolveAI .NET — Frontend Architecture & Implementation Plan

**Status:** Accepted and implemented for F0–F3; F4–F6 remain planned and blocked on their deliveries
**Version:** 1.0
**Date:** 2026-09-16
**Scope:** operator/demo web client
**Related:** `11_TECHNOLOGY_STACK.md` section 10, `13_IMPLEMENTATION_ROADMAP.md` section 15, `09_IDENTITY_TENANCY_AUTHORIZATION.md`

---

# 1. Purpose

ResolveAI has a backend and no client. Every capability is currently demonstrated with `curl`, PowerShell scripts and database inspection. The roadmap already reserves a minimal operator UI as V1 Milestone 5, with the acceptance statement:

> A reviewer can understand the entire flow without reading database tables or logs manually.

This document turns that one-paragraph milestone into an architecture, a contract inventory, a backend prerequisite list and ordered implementation phases.

Phases F0–F3 are now implemented in `web/`, together with the blocking backend prerequisites. Section 16 records the decisions as taken and section 18 records where the implementation deliberately departed from this plan.

---

# 2. Position in the delivery order

Roadmap section 18 states an explicit guardrail:

> Do not [...] build the operator UI before the backend flow is stable.

That guardrail is respected as follows.

- The backend flow is stable for **Deliveries 1–3**: case intake, business facts and structured AI proposals are implemented, tested and contract-frozen. Phases **F1–F3** of this plan consume only those contracts.
- The backend flow is **not** stable for policy, approval, refund execution and workflow state (Deliveries 4–6). Phases **F4–F5** are specified here but must not start before their backing delivery is complete.
- **F6** covers V1 concerns (OIDC, RAG evidence, failure injection, demo packaging) and follows the V0 release gate in roadmap section 10.

Consequence: this plan does not authorize building the UI now. It authorizes building the *first three phases* against already-frozen contracts, and it defines the rest so that later deliveries can ship their UI slice immediately instead of designing it under pressure.

If the preference is to keep the repository strictly backend-only until the V0 gate passes, only F0 (documents and contract preparation) runs now and F1 waits. That choice belongs to section 16.

---

# 3. Frontend thesis

The backend thesis is *LLM proposes, backend disposes*. The client-side corollary is:

> **The UI renders trusted backend state and requests actions. It never computes authority, eligibility or business outcomes.**

Practical consequences, each of which is a review rule:

- No refund threshold, approval level, eligibility rule or remaining-refundable amount is computed in TypeScript. The UI displays what the backend returned.
- Permission data is used only to disable or hide controls. A hidden control is a usability affordance, never a security boundary; the backend still returns `403` if the request is made.
- Tenant scope is never sent by the client as an authoritative value. The client selects an *identity*; the backend derives the tenant.
- A `404` on a case is rendered as "not found for this identity". The UI must not tell the user that the case exists in another tenant, because the backend deliberately does not disclose that.
- Model confidence is displayed as a model attribute, never as a progress bar toward approval or as a reason the action is allowed.

The UI is also a portfolio artifact: its main job is to make the *enforcement* visible. Denials, mismatches, uncertain outcomes and idempotent replays are features to be shown, not errors to be hidden behind a generic toast.

---

# 4. Non-goals

Consistent with charter section 9 ("complex frontend design" is a non-goal) and technology stack section 10 ("do not build a large frontend application"):

- no design system, component library theming work or visual polish beyond legibility;
- no SSR, no Next.js, no meta-framework;
- no Redux/MobX/Zustand global store; server state lives in the query cache and UI state lives in components;
- no customer-facing portal, no ticket inbox workflow features the backend does not have;
- no client-side business validation duplicated as authority (limits are mirrored only as input hints);
- no offline mode, no service worker, no real-time transport (WebSocket/SSE); polling is sufficient and matches the backend's persisted-result model;
- no browser telemetry pipeline in the first version;
- no chat interface. ResolveAI is deliberately not a chatbot, and a chat UI would misrepresent the architecture.

Target size: roughly 25–35 components and under ~3,500 lines of TypeScript for F1–F3.

---

# 5. Technology choices

| Concern | Choice | Reason |
|---|---|---|
| Framework | React + TypeScript (strict) | Already the accepted stack choice |
| Build/dev server | Vite | Accepted stack choice; dev proxy removes the CORS problem |
| Routing | React Router (data-free, component routes) | Small, no framework lock-in |
| Server state | TanStack Query | Polling, caching, request de-duplication and explicit error state are exactly the problems this UI has |
| HTTP | native `fetch` behind one thin client | No client library needed for ~12 endpoints |
| Types | `openapi-typescript` generated from `/openapi/v1.json` | The backend already publishes OpenAPI; hand-written DTOs would drift |
| Forms | controlled components + a small validation helper | Two forms exist; a form library is unjustified |
| Styling | CSS Modules (or plain CSS with custom properties) | No build complexity, no utility-class dependency |
| Unit/component tests | Vitest + React Testing Library | Standard, fast, Vite-native |
| API fakes in tests | MSW, handlers typed from the generated types | Contract drift breaks tests instead of production |
| E2E | Playwright, one thin suite | Needed only for the demo happy path and tenant isolation |
| Boundary enforcement | ESLint `import/no-restricted-paths` (or `eslint-plugin-boundaries`) | Frontend analogue of `ResolveAI.ArchitectureTests` |

Explicitly rejected for now: Next.js, Redux Toolkit, Axios, Tailwind, MUI/AntD, GraphQL, tRPC, Storybook, i18n framework, client-side OpenTelemetry. Each can be added later with a concrete reason, following the same discipline as the backend stack document.

Version pinning mirrors the backend approach: an exact Node version in `.nvmrc` plus `packageManager` in `package.json`, a committed lockfile and `npm ci` in CI, which is the frontend equivalent of `global.json` and `--locked-mode`.

---

# 6. Repository layout

The frontend is not a .NET project and does not belong in `src/`, which solution structure section 16 reserves for the solution's projects. Add a sibling top-level directory:

```text
ResolveAI/
├── src/                  (unchanged .NET projects)
├── tests/                (unchanged)
├── tools/                (unchanged)
├── api/
│   └── openapi-v1.json   committed OpenAPI snapshot, see B4
└── web/
    ├── package.json
    ├── vite.config.ts
    ├── .nvmrc
    └── src/
        ├── app/          router, providers, shell, error boundary
        ├── api/          generated types, fetch client, problem-details mapping
        ├── auth/         identity abstraction (dev header today, OIDC later)
        ├── features/
        │   ├── cases/
        │   ├── business-facts/
        │   ├── proposals/
        │   ├── audit/
        │   ├── workflow/     (F4)
        │   ├── approvals/    (F4)
        │   ├── execution/    (F5)
        │   └── diagnostics/
        ├── components/   small shared primitives (Outcome, Field, Timeline, Money)
        └── lib/          formatting, ids, polling helpers
```

Feature folders mirror the backend bounded contexts on purpose: `cases` ↔ CaseManagement, `proposals` ↔ AI, `business-facts` ↔ Execution reads, `audit` ↔ Audit, `approvals` ↔ Decisioning, `workflow` ↔ Orchestration, `execution` ↔ Execution mutations. A reviewer who understands the backend modules can navigate the UI without a tour.

Boundary rules, enforced by lint:

- a feature may import from `api/`, `auth/`, `components/`, `lib/`;
- a feature may not import another feature's internals; cross-feature composition happens in `app/` (the analogue of Orchestration composing modules in the Host);
- only `api/` may call `fetch`;
- only `auth/` may know how credentials are attached to a request.

The .NET solution, `Directory.Build.props` and `Directory.Packages.props` are untouched. `web/` is invisible to `dotnet build`.

---

# 7. Backend prerequisites

The current API cannot back a usable client. These are the gaps, each with the smallest contract that closes it. Every item needs the same treatment as existing endpoints: trusted actor context, tenant scoping, permission policy and integration tests including a cross-tenant negative case.

## 7.1 Blocking — implemented

All three shipped with tenant-scoped queries, permission policies and integration tests that include cross-tenant and insufficient-permission cases.

One contract narrowed during implementation: `GET /api/cases` takes `limit` and `cursor` only. The proposed `status` filter was dropped because `Created` is currently the sole case status, so the parameter would have had no observable behaviour to test. It returns when Workflow introduces a second state.

| ID | Gap | Why the UI needs it | Proposed contract |
|---|---|---|---|
| **B1** | No way to list cases | There is only `GET /api/cases/{id}`. A client cannot show a case list, and an operator has no way to obtain an ID other than remembering the `Location` header | `GET /api/cases?limit=&cursor=&status=` → `{ "items": CaseSummary[], "nextCursor": string \| null }`, policy `cases.read`, tenant-scoped. Keyset pagination on `Id` is sound because case IDs are `Guid.CreateVersion7()` and therefore creation-ordered |
| **B2** | No way to list a case's proposal runs | `GET /api/cases/{id}/proposals/{operationId}` requires an operation ID the server never lists. After a browser refresh the UI cannot find the run it just created, and generating a new operation ID would buy another provider invocation | `GET /api/cases/{id}/proposals` → `{ "items": ProposalRun[] }`, newest first, policies `cases.read` + `proposals.read` + the three business-read permissions, matching the existing GET |
| **B3** | No way to learn the caller's own context | The client would have to hard-code which fixture identity holds which permission, duplicating `DevelopmentAuthentication` in TypeScript. That breaks as soon as OIDC arrives | `GET /api/me` → `{ "actorId", "actorType", "tenantId", "permissions": string[] }`, requires only an authenticated user. Advisory data for enabling controls, never an authorization decision |

## 7.2 Recommended — B5 implemented, B4 and B6 deferred

**B5 is implemented.** `ApiExceptionHandler` now emits a stable `code` for every mapped failure: `AccessDenied`, `ValidationFailed`, `OperationConflict`, the proposal rejection codes (`InvalidProposalRequest`, `InvocationBudgetExceeded`), `InternalError` and the pre-existing business-read codes. The client matches on `code` and never on the title.

**B4 is deferred, and its premise changed.** An OpenAPI snapshot was intended to feed type generation, but the host's document currently describes *request* bodies only. Because every handler returns `IResult`, ASP.NET cannot infer a response type, so `GET /api/cases` publishes `200 OK` with no schema and `components.schemas` contains only `CreateCaseRequest`, `CaseBusinessReferences` and `GenerateRequest`. Generated response types would be `unknown`, so the client's types are hand-written from the module contracts instead (section 18). Making generation worthwhile first requires annotating endpoints with typed results or `Produces<T>()` — a backend change that improves the published document in its own right and should be judged on that basis, not as frontend tooling.

**B6 is deferred.** A case idempotency key needs a column, a unique index, a migration and concurrency tests: a backend delivery item, not a client enabler. The console mitigates the browser-specific risk by disabling the submit control while a creation is in flight and never retrying the mutation automatically, and the README continues to state the limitation.

| ID | Gap | Why | Proposal |
|---|---|---|---|
| **B4** | OpenAPI lacks response schemas and is only reachable from a running host | CI cannot generate or verify TypeScript types without booting the host and a database, and today the document carries no response shapes to generate from | Annotate endpoints so responses are described, then commit `api/openapi-v1.json` with an integration test asserting the live document equals the snapshot |
| **B5** | `ProblemDetails.code` is only populated for `BusinessReadException` | `ApiExceptionHandler` sets `code` to `null` for access denied, case validation and `OperationConflict`. The client would have to match on English `title` strings, which is fragile and blocks any future localization | Emit a stable `code` for every mapped exception: `AccessDenied`, `ValidationFailed`, `OperationConflict`, `ProposalRejected`, plus the existing business-read codes |
| **B6** | `POST /api/cases` has no idempotency key | Already documented as a limitation in the README. A double-submit or a retried request creates a second case, which a browser makes easy to trigger | Accept an optional client key and de-duplicate per tenant, consistent with the proposal `operationId` model |

## 7.3 Deliberately deferred

These arrive with their backend delivery and must not be invented client-side: workflow state and transition history (Delivery 4/6), policy decision and approval endpoints (Delivery 4), refund execution result and reconciliation state (Delivery 5), retrieved evidence and provenance (V1 Milestone 1), OIDC (V1 Milestone 2), failure-injection controls (V1 Milestone 4).

Note also that there is no endpoint to add business references to an existing case. The UI therefore surfaces `409 MissingReferences` as an explanation and offers creating a new case with references, rather than implying an edit capability that does not exist.

## 7.4 Not required: CORS

A backend CORS policy is *not* a prerequisite. The Vite dev server proxies `/api`, `/health` and `/openapi` to `http://localhost:5080`, so browser requests are same-origin in development, and the F6 demo serves the built assets behind a reverse proxy in front of the host. CORS is added only if the UI is ever hosted on a genuinely different origin, and then it must be environment-gated in the same defensive way as `DevelopmentAuth:Enabled`.

---

# 8. Contracts the UI consumes today

Frozen and available after Deliveries 1–3. JSON is camelCase; `decimal` values arrive as JSON numbers.

| Method | Path | Policies | Notes for the client |
|---|---|---|---|
| `GET` | `/health/live`, `/health/ready` | none | `/health/ready` returns `503` when PostgreSQL is unreachable |
| `GET` | `/openapi/v1.json` | none | Type generation source |
| `POST` | `/api/cases` | `cases.create` | `201` + `Location`. Subject 1–200 chars, description 1–10000, references all-or-nothing and ≤100 chars each, untrimmed IDs rejected. Unknown JSON properties are rejected; request body limit is 64 KiB |
| `GET` | `/api/cases/{id}` | `cases.read` | `404` for missing *or* other-tenant |
| `GET` | `/api/cases/{id}/audit` | `cases.read` + `audit.read` | Eventually consistent: the delivery worker runs about every second, so an empty array right after creation is normal |
| `GET` | `/api/cases/{id}/business-facts` | `cases.read` + `customers.read` + `orders.read` + `payments.read` | `409 MissingReferences` when the case has no references; revalidates stored references on every call |
| `GET` | `/api/business/customers/{c}` | `customers.read` | |
| `GET` | `/api/business/customers/{c}/orders/{o}` | `customers.read` + `orders.read` | |
| `GET` | `/api/business/customers/{c}/orders/{o}/payments/{p}` | + `payments.read` | |
| `POST` | `/api/cases/{id}/proposals` | `cases.read` + `proposals.create` + three business reads | Body is only `{ "operationId": "<guid>" }`. `200` for a terminal run, `202` + `Location` when a run is already `Running`, `409` when the same key is reused with different input |
| `GET` | `/api/cases/{id}/proposals/{operationId}` | `cases.read` + `proposals.read` + three business reads | `200` also for `Running`; the client must inspect `status`. `404` when unknown |

Response shapes:

```text
CaseSummary        { id, subject, description, status, createdAt,
                     references: { customerId, orderId, paymentId } | null }

CaseBusinessFacts  { customer: { id, displayName },
                     order:    { id, customerId, product, total, currency, deliveryStatus },
                     payment:  { id, orderId, capturedAmount, refundedAmount, currency, status } }

ProposalRun        { runId, operationId, caseId, status,
                     proposal: { action, paymentId, amount, currency,
                                 reasonCode, confidence, evidence[] } | null,
                     errorCode, provider, model, promptVersion, schemaVersion,
                     inputTokens, outputTokens, estimatedCostUsd, latencyMs,
                     createdAt, completedAt }

AuditFact          { eventId, tenantId, caseId, actorId, actorType,
                     action, occurredAt, traceParent }

ProblemDetails     { status, title, traceId, code | null, ... }
```

Case `status` is `Created` only; the richer lifecycle arrives with Orchestration. The UI must render an unknown status string as-is rather than switching on an exhaustive union, so that Delivery 4 states appear without a frontend change.

---

# 9. Cross-cutting design

## 9.1 Identity and the OIDC migration

The backend authenticates with an `X-Development-User` header naming one of three fixtures (`alpha-operator`, `alpha-reader`, `beta-operator`). This is excellent for demonstration — one dropdown lets a reviewer reproduce tenant isolation and permission denial live — and it is a liability if it ever reaches a production build.

Design: a single `auth/` module exposes

```text
useIdentity()      → { actorId, actorType, tenantId, permissions } | null
authorizeRequest() → applies credentials to an outgoing request
```

Today `authorizeRequest` sets the development header; under V1 Milestone 2 it attaches a bearer token from an OIDC authorization-code-with-PKCE flow. No feature code changes. This is the frontend analogue of the backend's provider-replaceability proof, and it is worth capturing as evidence when the swap happens.

Safety requirements:

- the development identity switcher is compiled out unless an explicit build-time flag is set, and the app refuses to start if that flag is set in a production build;
- selected identity is stored in `sessionStorage`, never a cookie, and is cleared on switch together with the entire query cache, so no tenant's data can be shown under another identity;
- permissions from `GET /api/me` gate UI affordances only.

## 9.2 Outcome mapping

The client keeps one table from `(status, code)` to a rendered outcome. Every entry is deliberate, because these outcomes are the product demonstration.

| Status / code | Rendered as |
|---|---|
| `401` | Identity not recognized; open the identity picker |
| `403 AccessDenied` | "This identity lacks *permission*"; name the missing permission and keep the control visible but disabled |
| `404` (case, proposal) | "Not found for this identity" — never "belongs to another tenant" |
| `400 ValidationFailed` | Field-level messages on the form |
| `400 InvalidReference` | Reference set incomplete or malformed |
| `409 MissingReferences` | Case has no business references; explain and offer creating a linked case |
| `409 OperationConflict` | Operation ID reused with different input; requires a new deliberate run |
| `422 AssociationMismatch` | Banner: customer/order/payment do not belong together, or currency mismatch |
| `503 Unavailable` | Retryable; offer an explicit retry, no automatic loop |
| `500` | Generic failure plus the `traceId`, with a copy affordance |

Every error surface shows `traceId`. That single detail is what lets a reviewer jump from the UI to the trace and close the observability story.

## 9.3 Asynchronous proposal runs and cost safety

The proposal endpoint is the only expensive, non-idempotent-by-default operation in the current API, and it has explicit durability semantics that the UI must not undermine.

Rules:

1. The client generates `operationId` with `crypto.randomUUID()` **once**, persists it (keyed by case ID) before the request, and reuses it for polling and after a refresh. Replay returns the original result without a new charge.
2. On `202`, follow the `Location` header and poll `GET` with backoff (1s, 2s, then 5s) up to roughly 95 seconds, covering the 30-second invocation timeout plus the 60-second running-claim margin.
3. `Failed` and `Interrupted` are terminal and must **never** trigger an automatic new run. Recovery deliberately records uncertainty instead of reissuing an uncertain paid call, and the UI must preserve that property.
4. "Generate again" is an explicit, confirmed action that mints a new `operationId` and states plainly that it may incur a new provider charge.
5. `ProviderNotConfigured` is rendered as its own state ("no API key configured on the host"), not as a generic failure, because that is the expected local state without credentials.
6. The run panel always shows provider, model, prompt version, schema version, token counts, estimated cost and latency. This metadata is the AI-engineering evidence; hiding it would waste it.
7. The proposal is labelled as a proposal. No control anywhere in F1–F3 suggests it can be executed, because no execution capability exists until Delivery 5.

## 9.4 Eventual consistency

Audit events are delivered by a background worker after the originating transaction commits. The audit timeline distinguishes "no events yet — delivery pending" from "no events", and polls briefly after a mutation instead of rendering an empty state that looks like data loss.

## 9.5 Money and numbers

Amounts are displayed with their currency exactly as returned, formatted with `Intl.NumberFormat`. No arithmetic on monetary values — no subtracting `refundedAmount` from `capturedAmount` to show a "remaining refundable" figure, because that value is a backend invariant enforced atomically by the payments simulation, and a stale client computation would misrepresent it.

## 9.6 Accessibility and content

Baseline only: semantic landmarks, labelled inputs, visible focus, errors associated with their fields via `aria-describedby`, keyboard-operable controls, and status regions announced politely. UI copy is English, matching the repository. No i18n framework, though the eval dataset does contain a Romanian case, so the app must render non-ASCII case text correctly.

---

# 10. Screen inventory

Mapped to roadmap Milestone 5's required views and to the delivery that unlocks each.

| Screen | Contents | Unlocked by |
|---|---|---|
| App shell | Identity switcher, tenant badge, navigation, global error boundary, backend health indicator | D1 + B3 |
| Case list | Paginated, tenant-scoped, status and created-at, link to detail | D1 + B1 |
| New case | Subject, description, optional complete reference set, backend-mirrored input limits, single-submit guard | D1 |
| Case detail — overview | Identifiers, status, references, created-at, copyable case ID | D1 |
| Case detail — business facts | Customer, order, payment; mismatch, missing-reference and unavailable outcomes | D2 |
| Case detail — AI proposal | Generate, poll, structured proposal, evidence list, invocation metadata, cost, failure and interrupted states, run history | D3 + B2 |
| Case detail — audit timeline | Ordered facts with actor, action, timestamp, trace parent, pending-delivery state | D1 |
| Diagnostics / demo | Health checks, tenant-isolation walkthrough, permission-denial walkthrough, OpenAPI link | D1–D3 |
| Case detail — workflow status | Canonical state, waiting reason, retry count, failure category, transition history | D4/D6 |
| Case detail — policy decision | `ALLOW` / `REQUIRE_APPROVAL` / `BLOCK`, policy version, required approval level | D4 |
| Approval | Pending queue, bound action summary, approve/reject, authority/expiry/fingerprint errors | D4 |
| Case detail — execution result | Refund outcome, `OutcomeUnknown` and `ManualReview` states, retry history, idempotent replay evidence | D5 |
| Case detail — evidence | Retrieved policy document, version, cited chunks | V1 M1 |
| Failure injection | Clearly labelled, strongly gated demo controls | V1 M4 |

---

# 11. Implementation phases

Each phase leaves the application runnable and demonstrable, per charter constraint C7.

## F0 — Decision and contract preparation

No UI code. Confirm the open decisions in section 16, record an ADR for the operator-UI boundary and technology, update `11_TECHNOLOGY_STACK.md` section 10 and `12_DOTNET_SOLUTION_STRUCTURE.md` section 16 with the `web/` directory, and open the backend work for B1–B3 (plus B4–B5 if accepted) as normal delivery items with tests.

- [x] Section 16 decisions answered and recorded.
- [ ] ADR added for the UI boundary, technology and the "UI computes no authority" rule. *(outstanding: this document plus README carry the rules; the ADR has not been written)*
- [x] B1–B3 implemented with tenant, permission and cross-tenant tests, and documented in the README.
- [ ] OpenAPI snapshot committed with a drift test. *(B4 deferred, section 7.2)*
- [x] `ProblemDetails` carries a stable `code` for all mapped failures.

## F1 — Shell, identity, case list and intake

- [x] `web/` builds; typecheck, lint, unit tests and production build run in CI.
- [ ] Generated API types are produced from the committed OpenAPI document, not hand-written. *(not met: hand-written, section 18)*
- [x] Identity switcher works for all three fixtures; switching clears the cache.
- [x] Case list is tenant-scoped and paginated; `beta-operator` never sees Alpha cases.
- [x] Case creation validates to the backend's real limits and cannot double-submit.
- [x] `401`, `403`, `404`, `400` and `503` render as distinct, correct outcomes with `traceId`. Framework rejections arrive without a body, so the client synthesizes the code from the status.
- [x] `alpha-reader` sees the creation control replaced by the named missing permission, and a forced request still yields a handled `403`.
- [x] No credential, key or provider string is present in the built bundle; the development identity path is absent from a production build and CI asserts it.

## F2 — Business facts and AI proposal

- [x] Business facts render for a linked case; `409`, `422`, `404` and `503` each have a distinct explanation.
- [x] A proposal can be generated, polled through `Running`, and displayed with all invocation metadata.
- [x] Reloading the page during a run resumes polling the same `operationId` and never issues a second paid invocation.
- [x] `Failed`, `Interrupted` and `ProviderNotConfigured` are explicit; nothing retries automatically.
- [x] "Generate again" requires confirmation and states the cost implication.
- [x] Run history for a case is visible.
- [x] The proposal is presented as unapproved and unexecutable.

## F3 — Audit timeline and demo surface

- [x] Audit timeline is ordered and shows actor, action, timestamp and trace parent.
- [x] Pending audit delivery is distinguishable from an absence of events.
- [x] Diagnostics page reproduces tenant isolation (an Alpha case ID under `beta-operator` → not found) and permission denial in two clicks.
- [x] Health status is visible.
- [ ] Health degradation with the database stopped has not been exercised.
- [x] README documents starting the UI alongside the host.
- [x] A reviewer can follow intake → facts → proposal → audit without a terminal.

## F4 — Workflow, policy and approval *(requires Delivery 4)*

- [ ] Workflow state and transition history render from the canonical state model, including unknown future states.
- [ ] Policy decision, policy version and required approval level are shown.
- [ ] Approve and reject work; insufficient authority, expiry, fingerprint mismatch and wrong tenant each render as their own outcome.
- [ ] Restart-while-waiting is demonstrable in the UI: the waiting case survives a host restart and then resumes.
- [ ] No approval threshold logic exists in the frontend.

## F5 — Execution result and reliability narrative *(requires Delivery 5)*

- [ ] Refund outcome, including `OutcomeUnknown` and `ManualReview`, is rendered without ambiguity.
- [ ] A repeated request visibly produces one business effect.
- [ ] Retry count, limits and failure category are visible.
- [ ] No partial or uncertain outcome is ever rendered as success.

## F6 — V1 completion *(after the V0 gate)*

- [ ] OIDC login replaces the development header with changes confined to `auth/`.
- [ ] Retrieved evidence with document identity, version and cited chunks is displayed.
- [ ] Failure-injection controls exist, are clearly labelled and strongly gated.
- [ ] The full demo starts from documented commands with the UI served same-origin.
- [ ] Milestone 5 acceptance holds: a reviewer understands the entire flow without reading tables or logs.

---

# 12. Testing and CI

Mirroring the backend's principle of testing real boundaries rather than mocking internals:

- **Component and hook tests** (Vitest + RTL) cover the outcome mapping table, the polling and operation-ID persistence logic, and permission-based disabling. These are the parts where a bug would misrepresent backend behavior.
- **API fakes** use MSW handlers typed against the client's own contract types, so a contract change breaks tests rather than production. Every error status in section 9.2 has at least one handler, including the bodyless framework rejections.
- **E2E** (Playwright) is *not* implemented. F0–F3 were instead verified manually against a real host and PostgreSQL: intake, facts, proposal, audit, cross-tenant not-found and read-only permission denial. Automating those two scenarios remains open work; until then the end-to-end claims rest on a manual pass rather than a gate.
- No snapshot-only tests. Assertions use accessible roles and names.

CI addition to `.github/workflows/ci.yml`: a `web` job (Node pinned by `web/.nvmrc`, `npm ci`, lint, typecheck, unit tests, production build) running in parallel with the existing `build-and-test` job, which stays untouched. The job also greps the built bundle for the development authentication header and fails if it is present, so the claim that development identities cannot ship is enforced rather than asserted.

---

# 13. Serving and demo

- **Development:** `dotnet run --project src/ResolveAI.Host` on `:5080` plus `npm run dev` on `:5173`, with the Vite proxy forwarding `/api`, `/health` and `/openapi`. Same-origin, no CORS, no backend change.
- **Demo (F6):** build static assets and serve them behind a reverse proxy that also forwards `/api` to the host, added to `deploy/docker-compose.yml` at that phase. This keeps the Host free of static-file hosting and keeps requests same-origin.
- The alternative — publishing the build into the Host's `wwwroot` with a SPA fallback — is simpler to run but couples the .NET build to Node. Recommendation: the proxy, decided in F0 either way.
- Per the stack document's guardrail, the container is added when the feature needs it, not earlier.

---

# 14. Risks

| Risk | Mitigation |
|---|---|
| Development auth header reaching a production build | Build-time flag plus a runtime startup assertion; covered by an F1 checklist item |
| A browser making paid AI invocations easy to repeat | Client-minted persisted `operationId`, no automatic retry, explicit confirmation with a cost statement |
| Business rules leaking into TypeScript | Stated as an ADR rule, enforced in review; no threshold or eligibility constant may exist in `web/` |
| Frontend work displacing backend deliveries | Phases are gated on their backing delivery; F4–F5 cannot start early |
| Contract drift between backend and generated types | Committed OpenAPI snapshot, backend drift test, typed MSW handlers |
| UI pressure distorting backend design | Only the four blocking prerequisites are accepted; anything else waits for its delivery |
| Empty audit array read as data loss | Explicit pending-delivery state and short post-mutation polling |
| Scope creep into a full support product | Non-goals in section 4 and the stated size target |

---

# 15. Decisions confirmed by this plan

- The UI renders trusted backend state and never computes authority, eligibility or monetary outcomes.
- React + TypeScript + Vite, with no meta-framework and no global state library.
- The frontend lives in a top-level `web/` directory, outside the .NET solution.
- Feature folders mirror backend bounded contexts, with lint-enforced boundaries.
- ~~API types are generated from the backend's OpenAPI document.~~ Superseded: hand-written from the module contracts, see sections 7.2 and 18.
- Credentials are attached in one `auth/` module so the development header can be replaced by OIDC without touching feature code.
- Development is same-origin via the Vite proxy; no backend CORS policy is introduced.
- Backend prerequisites are limited to listing cases, listing proposal runs and a caller-context endpoint, plus recommended contract hygiene.
- Phases F4–F6 are specified but blocked on Deliveries 4, 5 and the V0 gate.

# 16. Decisions as taken

| Question | Decision |
|---|---|
| Start F1 now, or wait for the V0 gate? | Started now. F1–F3 consume only the frozen Delivery 1–3 contracts, so the roadmap guardrail against building UI on unstable flows is respected; F4–F6 stay blocked on their deliveries |
| Accept B4, B5, B6? | B5 accepted and implemented. B4 and B6 deferred with recorded reasons (section 7.2) |
| Directory | `web/`, outside the .NET solution, as proposed |
| Demo serving | Unchanged and still open: development uses the Vite proxy. The reverse-proxy container is deferred to F6, when the demo packaging actually needs it |
| Frontend CI job required from the start? | Yes. The `web` job runs on every push and pull request with no `continue-on-error` |
| Accepted baseline, or proposal? | Accepted as the baseline for F0–F3. The ADR recording the "UI computes no authority" rule is still outstanding |

---

# 17. Acceptance criteria for this plan

- [x] Section 16 is fully answered.
- [x] The backend prerequisite list is agreed and scheduled as normal delivery work with tests.
- [x] The phase order does not violate roadmap section 18's guardrail.
- [x] The screen inventory covers every view required by roadmap Milestone 5.
- [x] No proposed UI behavior duplicates a backend authority decision.
- [x] `00_PLAN_INDEX.md` references this document with its real status.

---

# 18. Where the implementation departed from the plan

Recorded so the difference is visible rather than quietly absorbed.

| Planned | Built | Reason |
|---|---|---|
| API types generated from OpenAPI | Hand-written in `web/src/api/types.ts` from the module contracts | The host's OpenAPI document has no response schemas because handlers return `IResult`, so generation would yield `unknown`. Section 7.2 records the backend change that would make generation worthwhile |
| Playwright E2E for two scenarios | Manual verification against a real host and PostgreSQL | Left as open work; the two scenarios are specified in section 12 and the end-to-end claims currently rest on a manual pass, not a gate |
| `GET /api/cases?status=` | `limit` and `cursor` only | `Created` is the only case status today, so the filter had no behaviour to test |
| Lint-enforced boundaries using relative-path patterns | Cross-directory imports go through an `@/` alias, and the boundary rules name layers explicitly | Relative-path globs could not distinguish `../other-feature/x` from `../../api/x`, so they rejected legitimate imports. The alias makes each rule exact |
| Case idempotency key (B6) | Not implemented | Backend delivery work rather than a client enabler; the console disables the submit control while a creation is in flight and never retries it |

Two behaviours were added that the plan did not anticipate, both because the alternative would have misrepresented the backend:

- **Bodyless failures.** Framework rejections — anonymous requests, missing permissions, unmatched routes — never reach `ApiExceptionHandler` and therefore carry no `code`. The client synthesizes one from the status so a `403` from a policy reads identically to a `403` from an exception.
- **Unresolved caller context.** While `GET /api/me` is in flight there are no known permissions, and gating on that state alone made panels claim the backend had refused something it was never asked. Permission checks now distinguish "not granted" from "not yet known".
