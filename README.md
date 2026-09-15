# ResolveAI

Multi-tenant support operations backend. AI proposes; the backend validates, authorizes and executes.

## Planning

- [Planning index](docs/00_PLAN_INDEX.md)
- [Implementation roadmap](docs/13_IMPLEMENTATION_ROADMAP.md)
- [Architecture decisions](docs/adr/ADR-001-MODULAR-MONOLITH-FIRST.md)

## Implemented deliveries

Implemented: persistent case intake, trusted development identity, tenant isolation, transactional audit outbox, background delivery, OpenTelemetry instrumentation, PostgreSQL migrations and automated checks.

Delivery 2 adds persistent simulated customer/order/payment data, permission-checked read capabilities and validated case references.

Delivery 3 adds persisted structured AI proposals, an OpenAI adapter and 10 versioned evaluation cases. Deterministic checks pass; live-provider verification is pending a configured API key. See [Delivery 3](docs/16_DELIVERY_3.md) for API and evaluation commands.

### Prerequisites

- .NET SDK 10.0.300 (or a compatible patch in that SDK feature band).
- Docker with Linux containers; Docker must also be running for integration tests.
- Git. PowerShell is needed only for the optional demo script.

### Run locally

From the repository root:

```sh
docker compose -f deploy/docker-compose.yml up -d --wait
dotnet restore --locked-mode
dotnet build --no-restore
dotnet run --no-build --project src/ResolveAI.Host -- --migrate
dotnet run --no-build --project src/ResolveAI.Host -- --seed-demo
dotnet run --no-build --project src/ResolveAI.Host
```

The launch profile selects Development and binds to `http://localhost:5080`. The explicit migration command applies Audit, CaseManagement, Simulation and AI migrations and exits. `--seed-demo` inserts missing synthetic fixtures without resetting existing data. Run these commands separately, in that order. Normal startup does not migrate or seed.

PostgreSQL uses loopback port 55432 and a named persistent volume. The committed password is a local demo fixture only. Override `ConnectionStrings__Database` to use another database. `docker compose -f deploy/docker-compose.yml down` stops the local database without deleting its volume.

### Try the API

In PowerShell, while the host is running:

```powershell
./scripts/demo.ps1
```

Or use curl (`curl.exe` on Windows PowerShell):

```sh
curl -i http://localhost:5080/api/cases \
  -H 'Content-Type: application/json' \
  -H 'X-Development-User: alpha-operator' \
  -d '{"subject":"Damaged monitor","description":"The monitor arrived broken."}'

curl http://localhost:5080/api/cases/CASE_ID \
  -H 'X-Development-User: alpha-operator'

curl http://localhost:5080/api/cases/CASE_ID/audit \
  -H 'X-Development-User: alpha-operator'
```

Creation returns `201` and a `Location` header. Audit is delivered asynchronously, normally on the next one-second worker pass. Retry the audit GET if the array is initially empty. Stop the host, restart it, and GET the same case ID to verify persistence.

| Fixture identity | Tenant | Permissions |
|---|---|---|
| `alpha-operator` | `11111111-1111-1111-1111-111111111111` | create/read cases, read audit |
| `alpha-reader` | same Alpha tenant | read cases |
| `beta-operator` | `22222222-2222-2222-2222-222222222222` | create/read cases, read audit |

Both operator identities also have `customers.read`, `orders.read`, `payments.read`, `proposals.create` and `proposals.read`; `alpha-reader` does not.

The server maps these fixed identities to tenants and permissions. JSON cannot supply tenant/actor/permission fields; unknown JSON properties are rejected. Anonymous/unknown identities receive `401`, missing permissions `403`, and cross-tenant or missing cases `404`.

Development authentication requires both an explicit enabled setting and Development/Testing environment. Startup fails in Production/Staging if development auth is enabled. With it disabled, startup also fails until a real provider is configured. OIDC is a V1 task. Local fixture authentication is intended for a trusted local machine only.

### Endpoints and observability

- `GET /health/live`: process liveness.
- `GET /health/ready`: PostgreSQL connectivity (does not assert migrations are current).
- `GET /openapi/v1.json`: OpenAPI document.
- `POST /api/cases`, `GET /api/cases/{id}`, `GET /api/cases/{id}/audit`.

Console output is structured JSON. Application logs contain identifiers, not subjects/descriptions or credentials. ASP.NET and `ResolveAI` spans correlate the HTTP operation and background audit delivery through a persisted W3C trace parent. Set `OTEL_EXPORTER_OTLP_ENDPOINT` to export to an OTLP collector; none is required locally. Audit remains persistent even when telemetry is not exported.

### Tests and CI

```sh
dotnet restore --locked-mode
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release --logger trx --results-directory TestResults
```

Integration tests start disposable PostgreSQL 18.3 containers and isolate each scenario in a new database. They cover fresh/repeated migrations, host restart, tenant isolation, permission checks, invalid payloads, atomic rollback, audit failure after commit, concurrent redelivery, background delivery and trace correlation. Architecture tests inspect compiled dependencies, foreign contract usage and implementation visibility.

GitHub Actions runs the same build/test sequence plus the fixture evaluation harness on Linux and uploads reports. Business-data tests cover shared external IDs across tenants, missing/mismatched references, persistence after restart/reseed, changed external associations, provider failures and capability spans. AI tests cover validated proposals, faults, permissions, concurrency, durable results and audit. Normal CI makes no paid-provider calls.

### Add a migration

```sh
dotnet tool restore
dotnet ef migrations add CHANGE_NAME --project src/Modules/ResolveAI.CaseManagement --startup-project src/ResolveAI.Host --context CaseDbContext --output-dir Persistence/Migrations
```

For Audit, use the Audit project and `AuditDbContext`. Each module has a separate schema, context, snapshot and migration history. Design-time factories generate migrations without starting the host or needing a real database.

### Structure and delivery notes

- `src/ResolveAI.Host`: HTTP, auth mapping, telemetry and worker wiring.
- `src/ResolveAI.Platform`: immutable actor context and tracing primitive.
- `src/Modules/ResolveAI.CaseManagement`: case state and its audit outbox.
- `src/Modules/ResolveAI.Audit`: append/read contracts and event persistence.
- `src/Modules/ResolveAI.Execution`: typed read capabilities, permissions and association checks; owns adapter ports.
- `src/Modules/ResolveAI.AI`: LLM gateway, structured validation, proposal runs and audit delivery.
- `tools/ResolveAI.Evals`: versioned fixtures, separate live evaluation and report comparison.
- `src/ResolveAI.Integrations`: PostgreSQL simulation and adapters implementing Execution ports.
- `tests/`: PostgreSQL integration and compiled architecture checks.
- [Delivery 1 design and limits](docs/14_DELIVERY_1.md).
- [Delivery 2 design and limits](docs/15_DELIVERY_2.md).
- [Delivery 3 design, evaluation commands and limits](docs/16_DELIVERY_3.md).

Current scope covers intake, read-only business data and AI proposals. Workflow/approval and refund execution follow in subsequent deliveries. Case creation itself does not yet accept a client idempotency key; retrying POST can create a second case. Proposal generation uses a tenant-scoped operation key. Audit events are deduplicated independently.

## Business-data demo

After migrations and `--seed-demo`, run `./scripts/demo-business.ps1` with the host running. It creates a linked damaged-monitor case, reads its facts and checks an incorrect payment association.

The case request can include an optional complete reference set:

```json
{
  "subject": "Damaged monitor",
  "description": "The monitor arrived broken.",
  "references": {
    "customerId": "customer-monitor",
    "orderId": "order-monitor",
    "paymentId": "payment-monitor"
  }
}
```

`GET /api/cases/{id}/business-facts` returns the current facts after revalidating the stored references. It does not infer a customer from free text. References are optional for existing intake clients; without them, this endpoint returns `409 MissingReferences`.

Read endpoints (use `X-Development-User: alpha-operator`):

- `GET /api/business/customers/customer-monitor`
- `GET /api/business/customers/customer-monitor/orders/order-monitor`
- `GET /api/business/customers/customer-monitor/orders/order-monitor/payments/payment-monitor`

| Outcome | HTTP status / code |
|---|---|
| Invalid/partial reference set | `400 InvalidReference` |
| Missing or another tenant's resource | `404 NotFound` |
| Wrong customer/order/payment association or currency mismatch | `422 AssociationMismatch` |
| Unavailable data source / capability timeout | `503 Unavailable` |
| Missing permission | `403` |

Fixtures exist for Alpha and Beta with deliberately identical external IDs (`*-monitor` and `*-other`), but tenant-specific names and amounts: 649 EUR for Alpha and 749 EUR for Beta. Alpha also has `*-alpha-only` resources. These are synthetic amounts, not approved refund-policy thresholds. No refund operation is implemented.
