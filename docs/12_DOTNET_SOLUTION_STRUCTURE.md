# ResolveAI .NET — Solution & Repository Structure

**Status:** Accepted planning baseline v1.1  
**Version:** 1.1  
**Goal:** map the accepted architecture into a practical `.NET` solution without project explosion.

---

# 1. Main decision

Use **one `.csproj` per bounded context**, not separate `Domain/Application/Infrastructure` projects for every module.

Each module keeps those concerns as folders/namespaces inside one assembly.

This gives us compile-time module boundaries without creating 30–40 projects.

---

# 2. Project count

Eventual target as capabilities are implemented (not a day-one scaffold):

```text
Production projects: 10
Tooling project:      1
Test projects:        3
------------------------
Total:               14
```

This is intentionally small enough for one developer.

---

# 3. Solution structure

```text
ResolveAI.sln

src/
├── ResolveAI.Host
├── ResolveAI.Platform
│
├── Modules/
│   ├── ResolveAI.CaseManagement
│   ├── ResolveAI.Orchestration
│   ├── ResolveAI.Decisioning
│   ├── ResolveAI.AI
│   ├── ResolveAI.Execution
│   ├── ResolveAI.Knowledge
│   └── ResolveAI.Audit
│
└── ResolveAI.Integrations

tools/
└── ResolveAI.Evals

tests/
├── ResolveAI.UnitTests
├── ResolveAI.IntegrationTests
└── ResolveAI.ArchitectureTests
```

---

# 4. `ResolveAI.Host`

ASP.NET Core composition root.

Responsibilities:

```text
HTTP endpoints
authentication wiring
authorization wiring
dependency injection
module registration
OpenTelemetry configuration
configuration/secrets binding
application startup
```

It should contain as little business logic as possible.

Allowed references:

```text
all production modules
Platform
Integrations
```

No other production project references `ResolveAI.Host`.

---

# 5. `ResolveAI.Platform`

Small cross-cutting platform kernel.

Contains only concepts genuinely shared across modules, for example:

```text
ExecutionContext / ActorContext
TenantId
Clock abstraction
correlation primitives
small shared result/error primitives if justified
```

It must NOT become:

```text
Common
Utils
SharedServices
BaseRepository
generic business model
```

Provider-specific identity code stays in Host/infrastructure wiring.

---

# 6. Module project structure

Each bounded context starts as one assembly.

Example:

```text
ResolveAI.Decisioning/
├── Contracts/
├── Domain/
├── Application/
├── Persistence/
├── Infrastructure/
├── DependencyInjection/
└── ResolveAI.Decisioning.csproj
```

Meaning:

```text
Contracts
    public module API

Domain
    entities/value objects/invariants

Application
    use cases / handlers / orchestration inside module

Persistence
    DbContext / mappings / migrations

Infrastructure
    module-specific technical adapters

DependencyInjection
    AddDecisioningModule(...)
```

Most implementation types should be `internal`.

Public surface should remain small.

---

# 7. Public contracts stay with their owning module

Do NOT create projects such as:

```text
ResolveAI.CaseManagement.Contracts
ResolveAI.Decisioning.Contracts
ResolveAI.Execution.Contracts
```

at the beginning.

Instead:

```text
ResolveAI.Decisioning
└── Contracts/
```

contains the public interfaces/DTOs needed by other modules.

Split contract assemblies only if a real deployment/versioning need appears later.

---

# 8. Module responsibilities

## CaseManagement

Owns:

```text
support cases
case lifecycle
case outcome
```

## Orchestration

Owns:

```text
workflow state
workflow transitions
V0 persisted state machine
V1 Temporal workflows
```

## Decisioning

Owns:

```text
business policies
policy decisions
approval requests/decisions
```

## AI

Owns:

```text
agent runs
LLM Gateway
structured proposals
provider adapter for reasoning
model invocation metadata
```

## Execution

Owns:

```text
Tool Gateway
tool definitions
execution records
idempotency
side-effect safety
```

## Knowledge

Owns:

```text
documents
versions
chunks
retrieval
provenance
pgvector usage
```

## Audit

Owns:

```text
append-oriented audit events
audit queries
```

---

# 9. `ResolveAI.Integrations`

Contains adapters for systems outside the ResolveAI core.

Initial areas:

```text
CRM/
Orders/
Payments/
Shipping/
```

For the portfolio these may initially be fake/local implementations.

Later they can be replaced by HTTP/external adapters without changing core module contracts.

It may also contain infrastructure adapters required by module-owned ports where placing them inside a core module would create an unwanted dependency cycle.

---

# 10. Dependency direction

The canonical compile-time graph is the explicit project-reference list in section 12. Runtime flow diagrams elsewhere do not add references.

CaseManagement never references Orchestration. Host wires application entry points; Orchestration coordinates CaseManagement and the other modules through contracts.

Execution owns integration ports it consumes; Integrations implements them; Host registers adapters through DI. Runtime calls to an adapter do not imply an Execution → Integrations project reference.

---

# 11. Important refinement: Orchestration coordinates modules

Avoid dependency cycles by keeping process composition in Orchestration.

Example:

```text
Orchestration
    ↓
Knowledge.RetrieveEvidence(...)
    ↓
AI.GenerateProposal(... evidence ...)
    ↓
Decisioning.Evaluate(...)
    ↓
Execution.Execute(...)
```

This is preferable to making every module call every other module.

For AI tool/function calling, the AI module may invoke approved read/request capabilities through the public `Execution` contracts. `Knowledge` becomes an AI dependency when RAG is introduced. This preserves the Tool/Execution Gateway as the safety boundary rather than allowing provider code to call business integrations directly.

---

# 12. Canonical project references

Canonical allowed compile-time references (add only when used; Knowledge references begin in V1):

```text
ResolveAI.Platform
    → none

ResolveAI.Audit
    → Platform

ResolveAI.CaseManagement
    → Platform
    → Audit

ResolveAI.Knowledge
    → Platform

ResolveAI.Decisioning
    → Platform
    → Audit

ResolveAI.AI
    → Platform
    → Execution
    → Knowledge (V1, when RAG is introduced)

ResolveAI.Execution
    → Platform
    → Decisioning
    → Audit

ResolveAI.Orchestration
    → Platform
    → CaseManagement
    → Knowledge
    → AI
    → Decisioning
    → Execution
    → Audit

ResolveAI.Integrations
    → Platform
    → Execution
    → Knowledge

ResolveAI.Host
    → all production projects
```

These references are the allowed graph, not permission for arbitrary type access. A dependency is added only when implemented behavior uses it; Execution read capabilities do not require an empty Decisioning project in Delivery 2.

Only public module contracts should be consumed.

---

# 13. No generic `Shared` project

Do not create:

```text
ResolveAI.Shared
ResolveAI.Common
ResolveAI.Utils
```

If a concept is shared, first ask:

```text
Who owns this concept?
```

Only truly platform-wide primitives belong in `ResolveAI.Platform`.

---

# 14. Test projects

## `ResolveAI.UnitTests`

One project initially.

Folders by module:

```text
CaseManagement/
Orchestration/
Decisioning/
AI/
Execution/
Knowledge/
Audit/
```

Do not create seven unit-test projects until size justifies it.

---

## `ResolveAI.IntegrationTests`

Covers:

```text
ASP.NET Core API
PostgreSQL
EF Core
module integrations
workflow recovery
idempotency
tenant isolation
```

Use:

```text
WebApplicationFactory
Testcontainers
```

E2E scenarios can initially live here.

---

## `ResolveAI.ArchitectureTests`

Protects structural rules.

Examples:

```text
core modules do not reference Host
modules cannot access another module's Persistence namespace
provider SDK types do not leak into unrelated modules
Evals is not referenced by production projects
forbidden dependency cycles do not appear
```

The exact architecture-testing library can be selected during implementation.

---

# 15. `ResolveAI.Evals`

Separate tooling/console project.

Responsibilities:

```text
load eval datasets
call ResolveAI API / test harness
grade results
compare baseline
produce reports
```

Dependency direction:

```text
Evals → ResolveAI public API/contracts
```

Never:

```text
production modules → Evals
```

---

# 16. Repository structure

Recommended repository:

```text
ResolveAI/
├── ResolveAI.sln
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── .editorconfig
├── .gitignore
├── README.md
│
├── src/
│   ├── ResolveAI.Host/
│   ├── ResolveAI.Platform/
│   ├── ResolveAI.Integrations/
│   │
│   └── Modules/
│       ├── ResolveAI.CaseManagement/
│       ├── ResolveAI.Orchestration/
│       ├── ResolveAI.Decisioning/
│       ├── ResolveAI.AI/
│       ├── ResolveAI.Execution/
│       ├── ResolveAI.Knowledge/
│       └── ResolveAI.Audit/
│
├── tools/
│   └── ResolveAI.Evals/
│
├── tests/
│   ├── ResolveAI.UnitTests/
│   ├── ResolveAI.IntegrationTests/
│   └── ResolveAI.ArchitectureTests/
│
├── docs/
│   ├── architecture/
│   └── adr/
│
├── deploy/
│   └── docker-compose.yml
│
└── scripts/
```

---

# 17. Central package management

Use:

```text
Directory.Packages.props
```

for NuGet package versions.

Benefits:

- one version location;
- avoids version drift between modules;
- cleaner `.csproj` files.

Use:

```text
Directory.Build.props
```

for shared compiler/build settings.

Examples:

```text
Nullable = enable
ImplicitUsings = enable
TreatWarningsAsErrors = true (or phased in early)
AnalysisLevel = latest/recommended
```

---

# 18. Project naming rule

Use business capability names.

Preferred:

```text
ResolveAI.CaseManagement
ResolveAI.Decisioning
ResolveAI.Execution
```

Avoid technical names such as:

```text
ResolveAI.Services
ResolveAI.Repositories
ResolveAI.Business
ResolveAI.Data
```

because they erase the bounded contexts.

---

# 19. Namespace rule

Namespaces follow project + capability.

Example:

```text
ResolveAI.Decisioning.Contracts
ResolveAI.Decisioning.Domain
ResolveAI.Decisioning.Application
ResolveAI.Decisioning.Persistence
```

Internal implementation namespaces should not be consumed from other modules.

---

# 20. When to split a module into multiple `.csproj`

Do NOT pre-split.

Split later only if there is concrete pressure such as:

```text
independent deployment
large compile-time dependency problem
provider adapter isolation
module becomes large enough to justify stricter internal boundaries
separate worker process
reusable public contract package
```

Example future evolution:

```text
ResolveAI.AI
```

could later become:

```text
ResolveAI.AI
ResolveAI.AI.OpenAI
```

if provider-adapter isolation becomes valuable.

But not in V0.

---

# 21. Projects introduced by delivery

Delivery 1 implements Host, Platform, CaseManagement and minimal Audit, plus the integration/architecture tests that verify their behavior. Introduce UnitTests when the first unit-testable rule exists.

Delivery 2 adds Execution read contracts and Integrations for persistent synthetic business facts. Delivery 3 introduces AI and Evals. Delivery 4 introduces Decisioning and the initial persisted Orchestration behavior. Delivery 5 extends Execution and Orchestration for refunds. Knowledge starts in V1.

Create no empty future module solely to match the eventual solution diagram. Each persistent module adds its own DbContext and migration history with its first persisted behavior. Audit/tracing begin in Delivery 1 and grow with every delivery.

---

# 22. Architecture rule of thumb

The solution should optimize for:

```text
clear ownership
+
simple navigation
+
compile-time boundaries
+
low ceremony
```

not for the maximum number of layers.

The guiding principle is:

> **One bounded context = one project initially. Split only when evidence justifies it.**
