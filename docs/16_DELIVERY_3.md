# Delivery 3: Structured proposals and initial evaluations

## Status

Implemented and verified locally with PostgreSQL and deterministic provider substitutes. The 10 fixture cases pass. **Live OpenAI verification remains pending credentials**; fixture results do not measure model quality or prove resistance to prompt injection by a real model.

## Implemented flow

`Host -> CaseManagement.Contracts -> AI.Contracts -> Execution.Contracts -> Integrations`

The host loads the tenant-owned case. AI reads the linked customer/order/payment through Execution, supplies minimal facts and case text to its internal OpenAI adapter, validates the structured output, then persists the result in `ai.agent_runs`. Provider SDK types do not appear in public contracts. The adapter uses OpenAI .NET 2.13.0 and strict JSON Schema with the pinned `gpt-4.1-mini-2025-04-14` snapshot. This is a small, non-reasoning baseline supporting structured outputs; model quality must be established by live evaluations.

The output has seven required fields: `action`, `paymentId`, `amount`, `currency`, `reasonCode`, `confidence`, `evidence`. Unknown, duplicate and missing fields, numeric strings, malformed JSON and unavailable evidence are rejected. Refund amounts must be positive, have at most two decimal places and stay within the captured-minus-refunded amount; payment/order/currency must match trusted facts. These checks constrain a proposal; Delivery 4 still owns eligibility and approval policy, and Delivery 5 owns execution with current facts.

No refund is executed by this delivery. The first prompt suggests a full remaining refund for damaged goods; this is an evaluation behavior, not an approved business rule. Confidence grants no authority.

## API

First apply migrations and seed the demo as described in the README. Both development operators have proposal and business-read permissions.

Configure `OPENAI_API_KEY` in the environment of the host process before starting it. Keep the key out of source control, command output and chat. Without it, generation persists `Failed / ProviderNotConfigured`; there is no fallback response presented as AI output.

Create a case with the business references from the README, then:

```powershell
$headers = @{ 'X-Development-User' = 'alpha-operator' }
$operationId = [guid]::NewGuid().ToString()
$body = @{ operationId = $operationId } | ConvertTo-Json
Invoke-RestMethod "http://localhost:5080/api/cases/$caseId/proposals" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
Invoke-RestMethod "http://localhost:5080/api/cases/$caseId/proposals/$operationId" -Headers $headers
```

Set `$caseId` to the created case ID. POST accepts only `operationId`; case content and business facts are loaded server-side. Both endpoints require case access, the corresponding proposal permission and all three business-read permissions. Missing/cross-tenant cases return 404; missing permissions return 403.

| Result | Behavior |
|---|---|
| `Completed` | HTTP 200, validated proposal and invocation metadata |
| `Failed` | HTTP 200, persisted error code, no proposal |
| `Interrupted` | HTTP 200, explicit uncertain/timeout outcome, no automatic retry |
| Concurrent existing `Running` | HTTP 202 with a polling Location |
| Same key and different request fingerprint | HTTP 409 `OperationConflict` |

GET returns the saved run (including `Running`), or 404 if absent. HTTP 200 means the run was retrieved; clients must inspect `status`.

## Durability, cost and audit

- A unique `(TenantId, OperationId)` claim is committed before the provider call. The request fingerprint binds the case, text and reference IDs. Replay returns the original result even after host restart or a prompt deployment; it does not re-read mutable external facts or buy another invocation.
- SDK retries are disabled. The default invocation timeout is 30 seconds. A running claim has an additional 60-second persistence margin; expired claims become `Interrupted / OutcomeUnknown` on polling or the background worker. Recovery never silently reissues an uncertain call. A deliberate new attempt needs a new operation ID and can incur a new charge.
- No database transaction remains open during provider calls. A returned result is saved with an independent five-second deadline even if the HTTP request was cancelled. A process/database failure before saving can lose the response; recovery records uncertainty rather than inventing a result or usage.
- Persisted metadata includes prompt/schema/model versions, timestamps, trusted-fact snapshot, actor, trace parent, latency and usage/cost when available. Unknown usage and cost remain null. Raw model output and full prompts are not logged or stored in the AI module; case text already belongs to CaseManagement.
- The completed/failed/interrupted row is also the durable audit source. AI now references only Audit's append contract in addition to Platform and Execution. The worker appends a stable server-generated audit event and acknowledges delivery afterward; redelivery is deduplicated. This concrete dependency is reflected in the canonical graph.
- Default limits: 16,384 input bytes, 512 output tokens, estimated USD 0.02 per invocation. Configuration uses `AI` / environment variables such as `AI__MaxInvocationCostUsd`. Estimates use USD 0.40 input and 1.60 output per million tokens, conservatively ignoring cached-input discounts. Input reservation uses a pessimistic UTF-8 byte bound plus prompt/schema/framing allowance. These are application estimates, not a provider billing cap; update rates together with model changes. The API has no tenant-wide cumulative budget yet.

Provider behavior and snapshot/pricing were checked against the official [structured output documentation](https://developers.openai.com/api/docs/guides/structured-outputs) and [GPT-4.1 mini model documentation](https://developers.openai.com/api/docs/models/gpt-4.1-mini). Refusal, incomplete output, authentication/rate-limit failures and timeout are explicit outcomes. Provider error bodies are not returned to clients.

## Evaluations

Dataset: `tools/ResolveAI.Evals/Data/initial-cases.json`. Ten versioned cases cover three damaged-item phrasings (including Romanian), duplicate charge, status question, unsupported cancellation, missing facts, ambiguous intent, instruction injection and an already-refunded payment. Integration tests separately inject malformed output, refusal, truncation, provider failure and timeout.

Run the deterministic harness, which validates stored responses and expected action/reason labels without invoking a model:

```sh
dotnet run --configuration Release --project tools/ResolveAI.Evals -- --mode fixture --output artifacts/evals-fixture.json
dotnet run --configuration Release --project tools/ResolveAI.Evals -- --mode fixture --baseline artifacts/evals-fixture.json --output artifacts/evals-fixture-comparison.json
```

Once `OPENAI_API_KEY` is configured, run the separate paid evaluation:

```sh
dotnet run --configuration Release --project tools/ResolveAI.Evals -- --mode live --max-calls 10 --budget-usd 0.20 --output artifacts/evals-live.json
```

Each attempted call reserves its estimated maximum cost before invocation, including attempts whose billing becomes unknown. Reaching either limit stops the run with a partial report and failure exit code. Each live call uses the same adapter, prompt and validator as the application; the CLI supplies synthetic facts directly and does not test HTTP or persistence. Those are covered by integration tests. No live evaluation runs in normal CI.

Reports contain dataset hash, prompt/schema/model, configured prices, timestamp, invocation count, reserved budget, per-case verdicts, token counts, estimated cost and latency. `--baseline` compares pass-to-fail regressions; mode, dataset hash and schema must match. Change the versioned prompt/model configuration in code to compare a candidate. Exit codes: 0 all passed; 1 failure or partial run; 2 invalid arguments/baseline or missing credentials. Generated reports are ignored locally; CI uploads the fixture report with test artifacts. Preserve live reports as review artifacts when that gate is run.

## Verification and remaining gate

The automated suite passes 37 tests (33 integration, 4 architecture), covering tenant/permission boundaries, concurrent duplicate requests, changed fingerprints, restart replay, expired claims, durable audit, budget rejection before invocation, provider faults, strict validation and provider-independent public contracts. Deterministic fixture reports pass 10/10, including baseline comparison. Locked restore and Release build/test are the required CI checks. A normal local host also verified migration, explicit `ProviderNotConfigured`, POST replay and GET of the same saved run without credentials.

Remaining: run all 10 cases against OpenAI, inspect any failures, save the live report and verify an API proposal with real credentials. The roadmap deliberately leaves this gate open. Decisioning, approval, automatic workflow coordination, refund execution and aggregate API spending controls are outside Delivery 3.
