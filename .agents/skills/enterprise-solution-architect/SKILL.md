---
name: enterprise-solution-architect
description: >
  Design and generate enterprise-grade Microsoft/.NET software solution architectures.
  Use this skill whenever a user asks to design, plan, scaffold, or review any enterprise
  application architecture — including modular monoliths, microservices, Clean Architecture,
  identity/auth systems, API gateways, event-driven systems, multi-tenancy, CQRS/DDD, frontend
  permission strategies, Azure infrastructure, CI/CD pipelines, or observability stacks.
  Trigger even for partial requests like "how should I structure my .NET solution?",
  "design an auth module", "what's the best way to do CQRS in .NET 9?", "help me plan
  microservices for an ERP", or "review my Clean Architecture layers". Always use this skill
  when enterprise architecture, solution design, or technical stack decisions are involved.
---

# Enterprise Solution Architect Skill

This skill guides the agent through producing **production-ready** enterprise solution architectures
using modern Microsoft/.NET technology. Follow the phases below in order; skip phases only when the
user's request clearly makes one unnecessary.

---

## Phase 1 — Clarify Requirements

Before designing, confirm the following (extract from conversation if already stated):

| Question | Why it matters |
|---|---|
| Application domain (ERP, SaaS, e-commerce, etc.) | Drives module boundaries |
| Deployment target (Azure, on-prem, hybrid) | Shapes infrastructure choices |
| Expected scale (users, TPS, data volume) | Monolith vs microservices decision |
| Auth requirements (Entra ID, custom IdP, B2C, multi-tenant) | Identity module design |
| Team size & structure | Conway's Law → deployment topology |
| Regulatory concerns (GDPR, HIPAA, SOC2) | Audit, encryption, data residency |
| Existing systems to integrate | Adapter / anti-corruption layer needs |
| Frontend framework preference (Angular, React, Blazor) | Permission-based rendering strategy |

If critical answers are missing, ask before proceeding. For requests with enough context, proceed and state assumptions clearly.

---

## Phase 2 — Architecture Decision

### 2a. Choose Deployment Topology

| Signal | Recommendation |
|---|---|
| Single team, < 500k users, monorepo preference | **Modular Monolith** |
| Multiple teams, independent deployability required | **Microservices** |
| Evolutionary — start simple, split later | **Modular Monolith → Microservices migration path** |

### 2b. Choose Auth Provider

| Signal | Recommendation |
|---|---|
| Corporate / employee-facing, Azure tenant available | **Microsoft Entra ID (Azure AD)** |
| Customer-facing / multi-tenant SaaS | **Microsoft Entra External ID (B2C successor)** |
| Self-hosted / full control required | **OpenIddict** on ASP.NET Core Identity |
| Multi-tenant with custom branding per tenant | **OpenIddict + tenant-aware claims pipeline** |

> For detailed Identity module design → see `references/identity-module.md`

### 2c. Choose Messaging Strategy

| Need | Recommendation |
|---|---|
| In-process eventing (monolith) | **MediatR domain events** |
| Cross-service async (microservices) | **Azure Service Bus** |
| High-throughput streaming | **Azure Event Hubs / Kafka** |
| Outbox pattern required | **MassTransit + EF Core Outbox** |

---

## Phase 3 — Solution Structure

### 3a. Canonical Folder Layout

```
src/
├── BuildingBlocks/              ← Cross-cutting shared packages
│   ├── SharedKernel/
│   ├── Domain/
│   ├── Application/
│   ├── Infrastructure/
│   ├── Authorization/
│   ├── Security/
│   ├── Caching/
│   ├── Messaging/
│   ├── Persistence/
│   ├── MultiTenancy/
│   ├── Observability/
│   ├── EventBus/
│   ├── Exceptions/
│   └── Contracts/
│
├── Modules/                     ← Vertical slices (one per business capability)
│   ├── Identity/
│   │   ├── Identity.Domain/
│   │   ├── Identity.Application/
│   │   ├── Identity.Infrastructure/
│   │   └── Identity.API/
│   ├── [YourModule]/
│   │   ├── [Module].Domain/
│   │   ├── [Module].Application/
│   │   ├── [Module].Infrastructure/
│   │   └── [Module].API/
│   └── ...
│
├── Gateways/                    ← API Gateway / BFF
│   └── ApiGateway/              (YARP or Azure API Management)
│
├── Hosts/                       ← Runnable entry points
│   ├── Monolith.Host/           (for modular monolith)
│   └── [ServiceName].Host/      (for microservices)
│
└── Frontend/
    ├── angular-app/
    ├── react-app/
    └── blazor-app/

tests/
├── UnitTests/
├── IntegrationTests/
└── ArchitectureTests/           ← NetArchTest.Rules enforcement

infra/
├── terraform/
├── bicep/
└── docker/
```

### 3b. Clean Architecture Layer Rules (enforced via ArchitectureTests)

```
Domain ← Application ← Infrastructure
                     ↑
                    API
```

| Layer | Contains | Must NOT reference |
|---|---|---|
| **Domain** | Entities, Value Objects, Domain Events, Business Rules, Specs | EF Core, MediatR, any framework |
| **Application** | Commands, Queries, DTOs, Interfaces, Validators, Auth contracts | EF Core, Infrastructure, external SDKs |
| **Infrastructure** | EF Core DbContexts, Repos, JWT, OAuth clients, Redis, Bus, Storage | Domain business logic |
| **API / Presentation** | Controllers, Minimal API endpoints, Middleware, DI wiring, Swagger | Direct DB access |

---

## Phase 4 — Technology Stack

### Core Backend
| Concern | Technology |
|---|---|
| Runtime | .NET 9 (LTS) |
| Web API | ASP.NET Core + Minimal APIs for performance-critical endpoints |
| ORM | Entity Framework Core 9 + Dapper for complex read queries |
| Mediator | MediatR 12 |
| Validation | FluentValidation |
| Mapping | Mapperly (source-gen, zero reflection) |
| Resilience | Polly v8 / Microsoft.Extensions.Resilience |
| API Versioning | Asp.Versioning.Http |
| OpenAPI | Scalar (modern replacement for Swashbuckle) |

### Identity & Security
| Concern | Technology |
|---|---|
| Auth Server (self-hosted) | OpenIddict 5 |
| Auth Server (enterprise) | Microsoft Entra ID |
| ASP.NET Identity | ASP.NET Core Identity |
| Token format | JWT (RS256) + Refresh Tokens |
| Token storage | HttpOnly Secure Cookie (web) / SecureStorage (mobile) |
| Secrets | Azure Key Vault + `IOptions<>` |
| API protection | JWT Bearer middleware + Policy-Based Authorization |

> Full identity flows, token design, and permission model → `references/identity-module.md`

### Data
| Concern | Technology |
|---|---|
| Primary DB | SQL Server 2022 / Azure SQL |
| Read cache | Redis (StackExchange.Redis) |
| Full-text search | Azure AI Search or Elasticsearch |
| Blob storage | Azure Blob Storage |
| Migrations | EF Core Migrations (per-module, not global) |

### Messaging & Events
| Concern | Technology |
|---|---|
| In-process | MediatR Notifications |
| Service-to-service | Azure Service Bus (MassTransit abstraction) |
| Streaming | Azure Event Hubs |
| Outbox pattern | MassTransit EF Core Outbox |
| Saga orchestration | MassTransit State Machines |

### Infrastructure & Azure
| Concern | Technology |
|---|---|
| API Gateway | Azure API Management or YARP (reverse proxy) |
| Container runtime | Azure Container Apps or AKS |
| IaC | Bicep (primary) or Terraform |
| Service mesh | Dapr (optional, for microservices) |
| Config | Azure App Configuration + Feature Flags |

### Observability
| Concern | Technology |
|---|---|
| Structured logging | Serilog → Application Insights / Seq |
| Distributed tracing | OpenTelemetry → Azure Monitor |
| Metrics | OpenTelemetry Metrics → Prometheus / Azure Monitor |
| Health checks | `Microsoft.Extensions.Diagnostics.HealthChecks` |
| APM | Azure Application Insights |

> Full observability setup → `references/observability.md`

### Frontend Permission Rendering
| Framework | Strategy |
|---|---|
| Angular | `PermissionGuard`, `*hasPermission` structural directive, route guards |
| React | `<PermissionGate>` HOC, `usePermission()` hook, route wrappers |
| Blazor | `<AuthorizeView Policy="...">` component, `IAuthorizationService` |

> Full frontend patterns → `references/frontend-permissions.md`

---

## Phase 5 — CQRS & DDD Patterns

### Command/Query Separation
```csharp
// Command (write side) — via MediatR
public record CreateOrderCommand(Guid CustomerId, List<OrderItemDto> Items)
    : ICommand<Guid>;

// Query (read side) — direct Dapper / EF projection, no domain model
public record GetOrderSummaryQuery(Guid OrderId)
    : IQuery<OrderSummaryDto>;
```

### Domain Entity Pattern
```csharp
public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public OrderStatus Status { get; private set; }

    public static Order Create(CustomerId customerId, IEnumerable<OrderItem> items)
    {
        var order = new Order(OrderId.New(), customerId);
        order.AddDomainEvent(new OrderCreatedEvent(order.Id));
        return order;
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("Only pending orders can be confirmed.");
        Status = OrderStatus.Confirmed;
        AddDomainEvent(new OrderConfirmedEvent(Id));
    }
}
```

### Pipeline Behaviors (cross-cutting via MediatR)
```
Request → LoggingBehavior → ValidationBehavior → AuthorizationBehavior → TransactionBehavior → Handler
```

---

## Phase 6 — Security Architecture

### Permission Naming Convention
```
{Module}.{Action}
Examples: Orders.View | Orders.Create | Orders.Approve | Users.Manage | Reports.Export
```

### Policy Registration
```csharp
services.AddAuthorization(options =>
{
    // Auto-register a policy for every permission
    foreach (var permission in Permissions.All)
        options.AddPolicy(permission, p =>
            p.RequireClaim(CustomClaims.Permission, permission));
});
```

### Endpoint Authorization
```csharp
// Controller
[Authorize(Policy = Permissions.Orders.Create)]
[HttpPost]
public Task<IActionResult> Create(CreateOrderCommand cmd) { ... }

// Minimal API
app.MapPost("/orders", CreateOrder)
   .RequireAuthorization(Permissions.Orders.Create);
```

### Multi-Tenancy Strategy
| Isolation Level | DB Strategy | When to Use |
|---|---|---|
| Silo (full isolation) | Separate DB per tenant | High-compliance, enterprise |
| Pool (shared DB) | `TenantId` column + global query filter | SaaS, cost-sensitive |
| Hybrid | Shared schema + optional separate DB | Tiered SaaS plans |

> Full identity, tenant, and auth design → `references/identity-module.md`

---

## Phase 7 — Output Deliverables

Always produce the following for a full architecture engagement:

1. **Architecture Overview** — narrative explaining the chosen topology and rationale
2. **Solution Structure** — full folder tree with project names
3. **Layer Dependency Diagram** — ASCII or Mermaid
4. **Module Boundary Map** — which modules exist, their responsibilities, how they communicate
5. **Identity & Auth Design** — token flow, permission model, policy registration
6. **Infrastructure Diagram** — Azure services and how they interconnect
7. **Key Code Scaffolds** — representative csharp snippets per layer
8. **ADRs (Architecture Decision Records)** — one per major decision (topology, auth, DB, messaging)
9. **CI/CD Pipeline Outline** — GitHub Actions / Azure DevOps stages
10. **Non-Functional Requirements Checklist** — security, resilience, observability, scalability

For smaller requests (e.g., "design just the auth module"), produce only the relevant subset.

---

## Phase 8 — Architecture Decision Records (ADR Template)

```markdown
## ADR-001: [Decision Title]

**Date:** YYYY-MM-DD
**Status:** Accepted

### Context
[What problem are we solving? What forces are at play?]

### Decision
[What did we decide?]

### Rationale
[Why this option over the alternatives?]

### Consequences
✅ [Positive outcomes]
⚠️ [Trade-offs or risks]
```

---

## Reference Files

| File | When to Read |
|---|---|
| `references/identity-module.md` | Any question about auth, identity, permissions, JWT, multi-tenancy |
| `references/frontend-permissions.md` | Angular/React/Blazor permission-based UI rendering |
| `references/observability.md` | Logging, tracing, metrics, health check setup |
| `references/ci-cd-pipelines.md` | GitHub Actions / Azure DevOps pipeline design |
| `references/microservices-patterns.md` | Service decomposition, inter-service comms, sagas, outbox |

---

## Agent Behaviour Rules

- **Always state assumptions** when proceeding without full requirements.
- **Always justify technology choices** — never list tech without explaining why.
- **Prefer composition over monolithic God-classes** in all designs.
- **Never violate Clean Architecture layer rules** — call them out if user's existing code does.
- **Default to Modular Monolith** unless the user gives strong signals for microservices.
- **Default to OpenIddict** for self-hosted identity; Entra ID for enterprise/corporate.
- **Generate working C# snippets** — not pseudocode — for all code examples.
- **Include security considerations** (threat model, OWASP top 10 relevance) in every design.
- **Scale output to request scope** — full architecture doc for broad requests, focused answer for narrow ones.
