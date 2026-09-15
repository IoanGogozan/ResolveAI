# ResolveAI

Multi-tenant support operations backend. AI proposes; the backend validates, authorizes and executes.

## Planning

- [Planning index](docs/00_PLAN_INDEX.md)
- [Implementation roadmap](docs/13_IMPLEMENTATION_ROADMAP.md)
- [Architecture decisions](docs/adr/ADR-001-MODULAR-MONOLITH-FIRST.md)

## Delivery 1

Implemented: persistent case intake, trusted development identity, tenant isolation, transactional audit outbox, background delivery, OpenTelemetry instrumentation, PostgreSQL migrations and automated checks.

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
dotnet run --no-build --project src/ResolveAI.Host
```

The launch profile selects Development and binds to `http://localhost:5080`. The explicit migration command applies Audit then CaseManagement migrations and exits. Normal startup does not change the database schema.

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

GitHub Actions runs the same build/test sequence on Linux and uploads test results. There are no AI dependencies or paid-provider calls in Delivery 1.

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
- `tests/`: PostgreSQL integration and compiled architecture checks.
- [Delivery 1 design and limits](docs/14_DELIVERY_1.md).

Current scope ends at intake. Business integrations, workflow/approval, refund execution and AI follow in subsequent deliveries. Case creation itself does not yet accept a client idempotency key; retrying POST can create a second case. The audit event for each committed case is deduplicated independently.
