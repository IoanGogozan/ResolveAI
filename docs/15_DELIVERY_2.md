# Delivery 2 — business facts and read capabilities

## Implemented boundary

Execution exposes typed `get_customer`, `get_order` and `get_payment` capabilities through `IBusinessReads`. Each has an explicit permission, low risk, no side effects and a five-second cooperative cancellation deadline per adapter read. There is no automatic retry. Order reads require customer access; payment reads require all three read permissions.

Execution owns `ICustomerReadPort`, `IOrderReadPort` and `IPaymentReadPort`. Integrations implements these ports, maps its private entities to Execution-owned fact records and scopes every database query by trusted tenant and external ID. Core modules never reference Integrations or its DbContext. The Host registers adapters and composes case intake with capability calls.

## Storage and fixtures

Integrations owns the `simulation` schema and its own EF context/migrations/history. Customers, orders and payments are simulated external domains packaged in one local adapter implementation. They are not new ResolveAI business modules. Relationships within this simulation use tenant-aware composite foreign keys. No FK or join links simulation to core case tables.

`--seed-demo` is a separate local setup command, after `--migrate`. Both commands run under the existing development/test startup guard. The seed transaction inserts missing rows with conflict-ignore semantics; normal startup does not seed, and reseeding does not overwrite changed payment state. There are two tenants with shared external IDs plus Alpha-only resources. Demo money uses EUR with two fractional digits; support for other monetary rules is not implied.

Payment storage includes captured/refunded amounts and a basic nonnegative/remaining-amount constraint. This is read-only simulation data for now; refund execution, concurrent refund semantics and payment operation idempotency remain Delivery 5 work.

## Case association

The intake API accepts an optional complete `references` object. Execution resolves the customer, verifies the order's customer and verifies the payment's order and currency before the API saves the case. CaseManagement validates reference shape and stores only external IDs. Its module contract does not claim these references remain verified forever.

`GET /api/cases/{id}/business-facts` first authorizes tenant-scoped case access, then resolves the saved IDs through Execution again. It returns current facts, not an immutable historical snapshot. References may become missing or invalid after creation; that produces an explicit failure rather than silently returning stale associations. There is no distributed transaction with external systems and no refund authorization implied by successful lookup.

Existing cases without references remain readable and creatable. Their business-facts endpoint returns `409 MissingReferences`. Missing/foreign resources return `404`; inconsistent in-tenant associations return `422`; invalid input returns `400`; source unavailability returns `503`. No raw database/provider error is returned.

## Observability and validation

Capability spans share the case request trace. The parent `case.intake` or `case.business_facts` span carries case/tenant identifiers; the latter has the case ID before lookup. Traces do not contain customer names or case content. Read calls do not add mutation audit events; case creation retains the existing durable audit outbox.

Tests cover fresh/repeated migrations and seed, new host instances, changed payment state surviving reseed, missing references, same IDs under different tenants, Alpha-only resources, incorrect customer/order/payment links, changed external associations, permission checks at HTTP and module boundaries, currency mismatch, unavailable source and trace correlation. Architecture checks include Execution and Integrations in the allowed graph.

## Verification — 2026-09-15

- Locked restore and Release build passed, with zero warnings/errors.
- 21 integration tests and 3 architecture tests passed (24 total, including Delivery 1 regressions).
- Migrations applied successfully to the existing local Delivery 1 database.
- Explicit demo seed, normal HTTP demo, incorrect payment rejection and forced host restart passed.
- [GitHub CI history](https://github.com/IoanGogozan/ResolveAI/actions/workflows/ci.yml) records the published build/test verification.

## References

- [Run and demo](../README.md#business-data-demo)
- [Canonical roadmap](13_IMPLEMENTATION_ROADMAP.md)
- [Module graph](12_DOTNET_SOLUTION_STRUCTURE.md)
