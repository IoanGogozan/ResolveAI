# Delivery 1 — implementation notes

## Scope and choices

Four production projects implement foundation and persistent case intake. No unused business modules are scaffolded. PostgreSQL 18.3 is pinned consistently in Compose and Testcontainers. SDK and NuGet dependencies are pinned, with lock files committed for reproducible restore.

The public module API consists of Contracts and registration/migration entry points. Runtime implementation and persistence types are internal. Generated EF migration classes are a tooling exception. HTTP identity is translated into immutable ActorContext; application operations enforce permissions again and scope reads by tenant before accessing resources.

## Audit delivery

CaseManagement commits SupportCase and CaseCreatedOutbox in the same EF SaveChanges transaction. The source event contains only identifiers, actor provenance, timestamp and trace parent. A host worker calls ICaseAuditDelivery, which reads its own outbox, invokes IAuditWriter and acknowledges only after successful append.

Audit stores events under a unique `(TenantId, EventId)` source identity. Concurrent/repeated append uses PostgreSQL ON CONFLICT DO NOTHING. No case content, tokens or connection strings are included in audit payloads. The API provides tenant-authorized reads only; there is no audit mutation endpoint.

| Interruption | Recovery |
|---|---|
| Before case/outbox commit | Both roll back |
| After case commit, before audit append | Persisted pending entry is delivered after restart |
| After audit commit, before source acknowledgement | Redelivery finds the same audit identity and safely acknowledges |
| Concurrent delivery by two scopes | Unique source identity yields one audit row |

This is a narrow transactional outbox for case-created audit facts. There is no broker, generic event bus or workflow engine. The worker reads batches of at most 100 and retries pending publication on subsequent one-second passes. Pending records remain persisted during outages; log entries report delivery errors without exception payloads.

## Validation and limits

Integration tests use real PostgreSQL and fresh host instances. Fault injection replaces the append contract to interrupt immediately after the Audit transaction commits; a separate database trigger test proves atomic case/outbox rollback. These establish commit-boundary recovery; they do not claim to simulate every infrastructure failure.

Audit is append-only through the application contract; the local database credential can still modify tables. Database-role hardening and retention are deferred. Delivered outbox entries are retained in this delivery. Operational alerting/backoff/retention can be added when load or deployment needs justify them.

Development auth is explicitly local-only. Production startup is blocked until OIDC is implemented. Readiness currently checks connectivity; schema deployment is an explicit migration step. Concurrent deployment/migration automation is outside Delivery 1.

Real OpenTelemetry export is optional through OTLP; no collector is bundled. The tests verify propagated trace identity. Workflow/refund idempotency, business rules and provider evals belong to later deliveries.

## Local verification — 2026-09-15

- Release build: zero warnings and errors.
- Locked dependency restore: passed.
- Integration tests: 8 passed against disposable PostgreSQL.
- Architecture tests: 3 passed.
- Normal host process: explicit migrations, case creation, forced termination before audit delivery, restart and exactly one recovered audit fact passed.
- PowerShell demo: case read, cross-tenant 404 and audit delivery passed.
- [GitHub Linux verification](https://github.com/IoanGogozan/ResolveAI/actions/runs/35001783598): passed (build, all 11 tests and uploaded reports).

## References

- [Canonical roadmap](13_IMPLEMENTATION_ROADMAP.md)
- [Persistence architecture](07_DATA_ARCHITECTURE_AND_PERSISTENCE.md)
- [Module graph](12_DOTNET_SOLUTION_STRUCTURE.md)
- [Run and test instructions](../README.md)
