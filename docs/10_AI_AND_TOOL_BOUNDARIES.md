# ResolveAI .NET — AI & Tool Boundaries

**Status:** Accepted planning baseline v1.1  
**Version:** 1.1  
**Scope:** LLM Gateway + Tool/Execution Gateway

---

# 1. Goal

ResolveAI must keep probabilistic AI reasoning separate from deterministic backend authority.

Core rule:

> **The LLM may propose. The backend validates, authorizes and executes.**

---

# 2. LLM Gateway

The application must not call a provider SDK directly from business modules.

Conceptually:

```text
AI module
   ↓
LLM Gateway
   ↓
Provider Adapter
   ↓
LLM Provider
```

## Responsibilities

The LLM Gateway owns provider-facing concerns such as:

- structured generation;
- provider/model selection;
- provider errors and timeouts;
- token/latency/cost metadata;
- provider-specific request/response mapping.

## It does NOT own

- business policy;
- authorization;
- approval rules;
- refund execution;
- tenant authority.

## Core result

AI should return a structured proposal, for example:

```text
StructuredProposal
- action type
- target reference
- arguments
- reason
- confidence
- evidence references
```

The proposal is untrusted input to the backend.

---

# 3. Provider isolation

Business modules depend on an internal contract, not OpenAI-specific types.

Target dependency direction:

```text
Business / AI logic
        ↓
internal LLM contract
        ↓
provider adapter
```

Success criterion:

> A provider can be replaced without rewriting Decisioning, Execution, Case Management or workflow business logic.

V0 needs only one provider.

A second provider is added later only when provider replacement/fallback is being demonstrated.

---

# 4. Malformed AI output

Structured output is validated before use.

Possible outcomes:

```text
Valid
InvalidSchema
UnsupportedAction
InvalidArguments
ProviderFailure
```

Invalid AI output never reaches side-effect execution directly.

Retries, if any, must be bounded.

---

# 5. Tool / Execution Gateway

The Tool Gateway is the backend safety boundary between AI requests and business capabilities.

Conceptually:

```text
AI proposal / tool request
        ↓
Tool / Execution Gateway
        ↓
validation
authorization
tenant check
policy
approval
idempotency
        ↓
Integration / business capability
```

---

# 6. Tool definition

Each exposed capability has explicit metadata.

Minimum:

```text
Tool
- name
- version
- input contract
- output contract
- risk level
- required permission
- timeout
- retry behavior
- idempotency requirement
```

Example:

```text
get_order
risk: LOW
permission: orders.read
side effect: no
```

```text
refund_payment
risk: HIGH
permission: payments.refund.execute
side effect: yes
approval: policy-dependent
idempotent: required
```

---

# 7. Read tools vs mutation tools

Read capabilities:

```text
get_customer
get_order
get_payment
get_shipment
search_knowledge
```

Mutation capabilities:

```text
refund_payment
cancel_shipment
update_customer_case
```

Mutation tools receive stricter validation and enforcement. `payments.refund.request` permits a proposal/request only; `refund_payment` execution requires `payments.refund.execute` on a trusted executor plus current policy/approval checks. The AI-facing contract never grants direct mutation execution.

---

# 8. Authority model

The AI may:

```text
read approved data
request a capability
propose a mutation
```

The AI may not:

```text
grant itself permission
choose authoritative tenant context
approve its own action
bypass policy
execute high-risk mutations directly
```

---

# 9. High-risk action flow

Example refund:

```text
AI proposes refund
      ↓
Execution validates request
      ↓
Decisioning evaluates policy
      ↓
approval required?
      ↓
human approval if needed
      ↓
Execution performs idempotent refund
      ↓
Audit records result
```

---

# 10. Integration boundary

Execution talks to external business systems through internal ports/adapters.

Example:

```text
Execution
   ↓
IPaymentOperations
   ↓
Payment Adapter
   ↓
Fake/External Payment System
```

Provider DTOs must not leak into core business modules.

---

# 11. V0 scope

Implement only what is required for one vertical slice:

```text
LLM Gateway
- one provider
- structured output
- basic metadata/errors

Tools
- get_customer
- get_order
- get_payment
- request/execute refund

Execution
- validation
- tenant context
- basic permission check
- policy/approval handoff
- idempotency
```

Do NOT add yet:

- multi-provider routing;
- automatic provider fallback;
- MCP;
- semantic tool discovery;
- complex tool registry;
- distributed tool execution.

---

# 12. V1 evolution

After the V0 flow works, add only where useful:

```text
provider replacement/fallback
richer model telemetry
MCP exposure (optional V1+)
more capabilities
advanced retry/reconciliation
tool versioning hardening
```

---

# 13. Acceptance criteria

The boundary is correct when:

- business modules do not depend on provider SDKs;
- malformed model output cannot reach execution;
- AI cannot determine trusted tenant/permissions;
- high-risk mutations require backend checks;
- side effects are idempotent where required;
- provider replacement does not require rewriting business logic;
- tools expose explicit contracts and risk metadata.
