# MetaForge: Framework vs Features

MetaForge is a **metadata-driven admin framework**. Your ERP/CRM/education screens are **features** built on top of that framework. This document defines what belongs where and how to extend the solution without mixing concerns.

## Mental model

| Concern | Role | Changes when |
|---|---|---|
| **Framework** | Reusable platform: metadata model, dynamic CRUD/grid, security, menus, form builder | You improve MetaForge itself |
| **Features** | Your business: EF entities, relationships, seed data, optional custom rules | You model a new domain (sales, HR, education, etc.) |

Features do **not** need new MVC controllers for standard CRUD — `ModuleController` renders any configured entity at runtime.

## Target solution layout (modular monolith)

```text
src/
├── MetaForge.Shared/                 Cross-cutting constants & exceptions
├── MetaForge.Framework.Domain/       (future) Metadata, Security, Audit, Common
├── MetaForge.Features.Domain/        (future) All feature EF entities only
├── MetaForge.Domain/                 Current: Framework + Features in one assembly
│   ├── Framework/                    Metadata, Security, Audit, Common, Enums
│   └── Features/                     Discoverable business entities
│       ├── MasterData/
│       ├── Sales/
│       └── Education/
├── MetaForge.Application/            Framework contracts (interfaces, DTOs)
├── MetaForge.Infrastructure/         Framework implementations + feature EF config
│   ├── Framework/                    Services, metadata/security persistence
│   └── Features/                     Feature entity configurations & seed
└── MetaForge.Web/                    Host shell
    ├── Framework/                    Form builder, security, menus, dynamic module
    └── Features/                     (optional) feature-specific UI overrides only
```

Today all layers live in the five projects above; folders mirror the future split so you can extract `MetaForge.Features.Domain` later without renaming types.

## Framework boundaries

### Domain (`MetaForge.Domain`)

| Folder | Namespace | Contents |
|---|---|---|
| `Metadata/` | `MetaForge.Domain.Metadata` | `ForgeForm`, `ForgeField`, `ForgeGridColumn`, `ForgeRelation`, `ForgeMenu`, lookups |
| `Security/` | `MetaForge.Domain.Security` | Users, roles, permissions |
| `Audit/` | `MetaForge.Domain.Audit` | `AuditLog` |
| `Common/` | `MetaForge.Domain.Common` | `BaseEntity` |
| `Enums/` | `MetaForge.Domain.Enums` | Form types, control types, menu enums |

### Application

Interfaces and DTOs for dynamic forms, grids, lookups, master-detail, auth, menus, discovery. No feature-specific DTOs unless you add custom workflows.

### Infrastructure

| Area | Examples |
|---|---|
| Framework services | `FormMetadataService`, `GenericCrudService`, `GridService`, `EntityMetadataDiscoveryService`, `SecurityManagementService` |
| Framework persistence | `MetadataConfigurations`, security configurations, migrations for platform tables |
| Framework dynamic | `EntityTypeResolver`, validation/conditional engines |

### Web

| Area | Examples |
|---|---|
| Framework UI | `FormConfigController`, `SecurityController`, `MenuController`, `ModuleController`, `/api/metaforge/*` |
| Framework assets | `wwwroot/js/metaforge/*` |

## Feature boundaries

### Domain (`MetaForge.Domain/Features/`)

- POCO entities inheriting `BaseEntity`
- Namespace: prefer `MetaForge.Domain.Features.{Area}` (e.g. `.Sales`, `.Education`)
- Legacy: `MetaForge.Domain.Business` — still discovered; migrate when convenient

### Infrastructure

- EF `IEntityTypeConfiguration<>` for feature tables under `Persistence/Configurations/Features/`
- Scaffold-generated configs under `Features/Generated/`
- Register `DbSet<>` in `MetaForgeDbContext`
- Optional seed in `Persistence/Seed/` (demo data only)

### Application / Web

Usually **empty** for CRUD features. Add code only for:

- Custom validators not expressible in `ForgeField` rules
- Integration events, reports, or non-metadata workflows
- Bespoke Razor when dynamic UI is insufficient

## Discovery rules

`EntityMetadataDiscoveryService` and `EntityTypeResolver` treat an entity as a **feature** when its namespace starts with:

- `MetaForge.Domain.Features` (preferred), or
- `MetaForge.Domain.Business` (legacy)

Framework namespaces (`Metadata`, `Security`, `Audit`) are excluded from discovery and dynamic CRUD.

Constants live in `MetaForge.Shared.Constants.FeatureDiscoveryConstants`.

## Dependency rules

```text
Features  →  Framework Domain (BaseEntity, enums)
Framework Application  →  Framework Domain
Framework Infrastructure  →  Application + Domain
Web  →  Application + Infrastructure (never EF directly)
```

Features must **not** reference Infrastructure or Web. Framework must **not** reference feature-specific types (only generic `entityName` strings and reflection).

## Adding a new feature screen

1. Add entity under `src/MetaForge.Domain/Features/{YourArea}/`.
2. Use namespace `MetaForge.Domain.Features.{YourArea}`.
3. Add EF configuration under `Infrastructure/Persistence/Configurations/Features/`.
4. Register `DbSet<>` in `MetaForgeDbContext`.
5. Add migration.
6. Run app → Form Builder → discover entity → configure → menu → permissions.

No new controller required for standard master / tabbed / master-detail screens.

## Migration path to separate projects

When the codebase grows:

1. Create `MetaForge.Framework.Domain` — move Metadata, Security, Audit, Common, Enums.
2. Create `MetaForge.Features.Domain` — move `Features/` entities; reference Framework.Domain.
3. Keep a single `MetaForgeDbContext` in Infrastructure (or split contexts only if you need separate databases).
4. Add `NetArchTest` rules: Features.Domain must not reference Infrastructure.

## Sample feature modules in this repo

| Module | Entities | Form types demonstrated |
|---|---|---|
| Master Data | Country, Region, Customer, Address, Product, Supplier | Master, Tabbed (Customer) |
| Sales | SalesOrder, SalesOrderItem, SalesOrderCharge | MasterDetailTabular |
| Education | Department, Student, Teacher, Semester | Master |

These are **reference features** for demos and tests, not part of the reusable framework package.
