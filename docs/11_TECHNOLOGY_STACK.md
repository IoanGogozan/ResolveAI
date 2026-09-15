# ResolveAI .NET — Final Technology Stack

**Status:** Accepted planning baseline v1.1  
**Version:** 1.1  
**Version snapshot:** September 2026

---

# 1. Core platform

| Area | Choice |
|---|---|
| Language | C# 14 |
| Runtime | .NET 10 LTS |
| Web | ASP.NET Core 10 Web API |
| Dependency Injection | Built-in .NET DI |
| Configuration | Options pattern + environment configuration |

Why:

- .NET 10 is the current LTS baseline.
- Keep the application close to the standard .NET platform.
- Avoid framework layers that do not solve a concrete project problem.

---

# 2. Persistence

| Area | Choice |
|---|---|
| Relational database | PostgreSQL 18 |
| ORM | EF Core 10 |
| PostgreSQL provider | Npgsql 10 |
| Vector search | pgvector |
| Migrations | EF Core, module-owned |

Architecture:

```text
one PostgreSQL database
+
schema per module
+
DbContext per persistent module
```

No separate vector database initially.

---

# 3. AI

| Area | Choice |
|---|---|
| Primary provider | OpenAI |
| .NET client | Official OpenAI .NET SDK |
| Integration | Internal LLM Gateway + provider adapter |
| Output | Structured output |
| Embeddings | Through internal LLM/embedding boundary |

V0:

```text
one provider
```

V1:

```text
provider replacement / optional fallback
```

Do not use Semantic Kernel or another AI orchestration framework in V0.

Reason:

ResolveAI should demonstrate its own architecture boundaries rather than hiding them behind an AI framework.

---

# 4. Workflow

## V0

```text
ASP.NET Core / .NET BackgroundService
+
PostgreSQL persisted workflow state
```

## V1

```text
Temporal
+
Temporal .NET SDK
```

Temporal coordinates durable workflows.

It does not own:

- business policy;
- authorization;
- idempotency;
- AI logic;
- payment semantics.

---

# 5. Authentication & authorization

## V0

```text
Development/Test Authentication Handler
```

Used for:

- local development;
- integration tests;
- predefined tenants/users/permissions.

## V1

```text
OIDC-compatible identity provider
```

Preferred portfolio implementation:

```text
Keycloak
```

Reason:

- standards-based OIDC;
- reproducible locally with Docker;
- no cloud account required;
- architecture remains compatible with providers such as Microsoft Entra ID.

ASP.NET Core policy-based authorization is used for application capabilities.

Business approval rules remain in Decisioning.

---

# 6. HTTP integrations

Use:

```text
HttpClientFactory
```

and, where required:

```text
Microsoft.Extensions.Http.Resilience
```

for:

- timeouts;
- bounded retries;
- circuit breaking where justified.

Do not create a generic resilience layer before actual external integrations exist.

---

# 7. Observability

## From V0

```text
Microsoft.Extensions.Logging
structured JSON logs
OpenTelemetry
```

Track at minimum:

```text
trace_id
tenant_id
case_id
workflow_id
agent_run_id
model_invocation_id
tool_execution_id
```

## V1 visualization

Preferred:

```text
OpenTelemetry
├── Prometheus
├── Grafana
└── trace backend (Tempo or equivalent)
```

The application stays OpenTelemetry-based so the backend can be changed.

---

# 8. Testing

| Test type | Technology |
|---|---|
| Unit tests | xUnit |
| API integration | ASP.NET Core WebApplicationFactory |
| Database integration | Testcontainers for .NET |
| PostgreSQL tests | Testcontainers.PostgreSql |
| AI evals | ResolveAI custom eval runner |
| E2E | xUnit + real local dependencies/fakes |

Important principle:

Prefer integration tests against real PostgreSQL instead of mocking EF Core behavior. Normal CI uses deterministic LLM substitutes. Real-provider evals run separately with explicit invocation limits, bounded cost and saved versioned reports.

---

# 9. Local infrastructure

Use:

```text
Docker
Docker Compose
```

V0 containers:

```text
PostgreSQL
```

V1 may add:

```text
Keycloak
Temporal
Prometheus
Grafana
Tempo
```

Only start a container when the corresponding feature is being implemented.

---

# 10. Frontend

The project remains backend-first.

For the operator/demo UI:

```text
React
TypeScript
Vite
```

Scope:

- case view;
- workflow state;
- approval screen;
- audit/evidence display;
- failure/demo controls where appropriate.

Do not build a large frontend application.

---

# 11. API style

Primary communication:

```text
REST + JSON
```

API documentation:

```text
OpenAPI
```

Do not introduce gRPC unless a real internal communication need appears.

---

# 12. Messaging

V0:

```text
no message broker
```

V1:

Introduce asynchronous messaging only when a concrete use case requires it.

Preferred choice if needed:

```text
RabbitMQ
+
MassTransit
```

Likely use cases:

- durable integration events;
- notification processing;
- decoupled background consumers.

Kafka is not part of the default stack.

---

# 13. Cache

V0:

```text
no Redis
```

Redis is introduced only if we identify a concrete need such as:

- distributed cache;
- rate-limit state;
- distributed coordination;
- semantic cache.

PostgreSQL remains the source of truth.

---

# 14. MCP

V0:

```text
no MCP
```

V1+:

MCP may expose selected capabilities after Tool Gateway and security boundaries are stable.

MCP does not replace the Execution safety boundary.

---

# 15. CI/CD

Use:

```text
GitHub
GitHub Actions
```

Pipeline target:

```text
restore
build
unit tests
integration tests
architecture checks
AI eval subset
container build
```

CI starts in Delivery 1 with build, relevant integration and architecture checks; add jobs when their behavior exists. Pin the SDK in global.json and package versions centrally. Deployment automation can be added after V0.

---

# 16. Explicitly excluded from the initial stack

Do not add initially:

```text
Kubernetes
Kafka
Redis
microservices
event sourcing
full CQRS framework
Semantic Kernel
MediatR as an architectural requirement
AutoMapper as an architectural requirement
dedicated vector database
multiple LLM providers
complex service mesh
```

Any of these can be introduced later only with a concrete reason.

---

# 17. Stack by milestone

## V0 — Walking Skeleton

```text
C# 14
.NET 10 LTS
ASP.NET Core 10
EF Core 10
Npgsql 10
PostgreSQL 18
official OpenAI .NET SDK
xUnit
Testcontainers
OpenTelemetry
Docker Compose
```

Workflow:

```text
application-owned PostgreSQL state machine
```

Authentication:

```text
development/test auth
```

---

## V1 — Portfolio Complete

Adds as features require them:

```text
Temporal
pgvector
Keycloak
Prometheus
Grafana
Tempo
React + TypeScript
RabbitMQ + MassTransit (only if justified)
MCP (optional V1+, after Tool Gateway is stable)
```

---

# 18. Final stack principle

> Use a small, modern .NET core and add infrastructure only when the architecture has a concrete need for it.

The portfolio should demonstrate engineering judgment, not the number of technologies in `docker-compose.yml`.
