# MetaForge: Framework vs Features

MetaForge is a **metadata-driven admin framework** delivered as a **modular monolith**. The reusable platform lives under `Core/`; business capabilities are **modules** under `Modules/` (Hrm, Production, …). This document defines what belongs where and how to extend the solution without mixing concerns.

## Mental model

| Concern | Role | Changes when |
|---|---|---|
| **Framework (Core)** | Reusable platform: metadata model, dynamic CRUD/grid, security, menus, form builder | You improve MetaForge itself |
| **Building blocks** | Cross-cutting constants (`Shared`) and module plugin contracts (`Modules.Abstractions`) | You add shared permissions or module lifecycle hooks |
| **Modules (Features)** | Business domains: EF entities, module DbContext, optional custom UI | You model HR, production, sales, etc. |
| **Host (Web)** | Composition root: MVC, API, middleware, module registration | You enable a module or add bespoke Razor |

Features do **not** need new MVC controllers for standard CRUD — `ModuleController` renders any configured entity at runtime.

## Solution layout

```text
src/
├── BuildingBlocks/
│   ├── MetaForge.Shared/                 Constants, exceptions, culture (no project references)
│   └── MetaForge.Modules.Abstractions/   IMetaForgeModule, IModuleDbContextResolver
├── Core/
│   ├── MetaForge.Core.Domain/            Metadata, Security, Audit, Common, Enums
│   ├── MetaForge.Core.Application/       Service interfaces, DTOs, contracts
│   └── MetaForge.Core.Infrastructure/    MetaForgeDbContext, platform services, resolver
├── Modules/
│   ├── Hrm/
│   │   ├── MetaForge.Hrm.Domain/
│   │   ├── MetaForge.Hrm.Application/
│   │   └── MetaForge.Hrm.Infrastructure/   HrmDbContext, HrmModule : IMetaForgeModule
│   └── Production/
│       └── …
└── Hosts/
    └── MetaForge.Web/                    Program.cs, Framework/, Areas/, Modules/
```

Legacy entities may still live under `MetaForge.Domain.Features.*` or `MetaForge.Domain.Business` inside Core.Domain folders; new module work should use `MetaForge.{Module}.Domain.Entities`.

## BuildingBlocks boundaries

### MetaForge.Shared

| Folder | Contents |
|---|---|
| `Constants/` | Permission strings, system-setting keys, `FeatureDiscoveryConstants`, `PermissionAction` |
| `Culture/` | Locale catalogs, display formatting |
| `Exceptions/` | `BusinessException`, `NotFoundException` |

**Rules:** No project references. No service interfaces, EF entities, or ASP.NET types.

**Referenced by:** Core.Application, Core.Infrastructure, module Domain projects.

### MetaForge.Modules.Abstractions

| Type | Role |
|---|---|
| `IMetaForgeModule` | Name, area, schema, DbContext type, DI registration hook |
| `IModuleDbContextResolver` | Routes dynamic CRUD to Core vs module DbContexts |

**Rules:** Contracts only — no DbContext classes, migrations, or controllers.

**Implemented in:**

| Contract | Location |
|---|---|
| `IMetaForgeModule` | `{Module}.Infrastructure/DependencyInjection.cs` (e.g. `HrmModule`) |
| `IModuleDbContextResolver` | `Core.Infrastructure/Persistence/ModuleDbContextResolver.cs` |

**Referenced by:** Core.Application, Core.Infrastructure, module Infrastructure. Web gets this transitively — register modules in `Web/Modules/MetaForgeModuleRegistration.cs`.

## Core (framework) boundaries

### Domain (`MetaForge.Core.Domain`, root namespace `MetaForge.Domain`)

| Folder | Namespace | Contents |
|---|---|---|
| `Metadata/` | `MetaForge.Domain.Metadata` | `ForgeForm`, `ForgeField`, `ForgeGridColumn`, `ForgeRelation`, `ForgeMenu`, lookups |
| `Security/` | `MetaForge.Domain.Security` | Users, roles, permissions |
| `Audit/` | `MetaForge.Domain.Audit` | `AuditLog` |
| `Common/` | `MetaForge.Domain.Common` | `BaseEntity` / `BaseEntity<TKey>` |
| `Enums/` | `MetaForge.Domain.Enums` | Form types, control types, menu enums |
| `Features/` | `MetaForge.Domain.Features.*` | Legacy in-core feature entities (prefer modules for new work) |

### Application (`MetaForge.Core.Application`, namespace `MetaForge.Application`)

Interfaces and DTOs for dynamic forms, grids, lookups, master-detail, auth, menus, discovery. References Core.Domain, Shared, and Modules.Abstractions.

### Infrastructure (`MetaForge.Core.Infrastructure`, namespace `MetaForge.Infrastructure`)

| Area | Examples |
|---|---|
| Platform services | `FormMetadataService`, `GenericCrudService`, `GridService`, `EntityMetadataDiscoveryService` |
| Platform persistence | `MetaForgeDbContext`, metadata/security configurations |
| Module integration | `ModuleDbContextResolver`, entity discovery across modules |

Core must **not** reference Hrm, Production, or other module assemblies directly.

## Module (feature) boundaries

### Domain (`MetaForge.{Module}.Domain`)

- POCO entities inheriting `BaseEntity` from Core.Domain
- Namespace: `MetaForge.{Module}.Domain.Entities`
- References: Core.Domain + Shared

### Application (`MetaForge.{Module}.Application`)

Optional — add only for module-specific workflows not expressible via metadata.

### Infrastructure (`MetaForge.{Module}.Infrastructure`)

- Module `DbContext` and migrations (separate schema, e.g. `hrm`)
- EF configurations under `Persistence/Configurations/`
- `IMetaForgeModule` implementation + `Add{Module}Module()` extension

### Web (`MetaForge.Web/Areas/{Module}/`)

Usually **empty** except a landing `HomeController`. Add bespoke Razor only when dynamic UI is insufficient.

## Host (Web) boundaries

```text
MetaForge.Web/
├── Program.cs
├── Modules/                    MetaForgeModuleRegistration (enable modules + migrate)
├── Framework/
│   └── Controllers/            Platform MVC + /api/metaforge/* (namespaces unchanged)
├── Views/                      Platform Razor (Form Builder, Security, dynamic Module)
├── Areas/{Module}/             Module-specific UI overrides
├── Middleware/
├── Models/                     ViewModels only
├── Localization/
└── wwwroot/js/metaforge/       Dynamic grid/form scripts
```

| In Web | Not in Web |
|---|---|
| Controllers, Razor, static assets | `GenericCrudService`, repositories |
| ViewModels | Domain entities |
| `AddMetaForgeModules()` orchestration | EF migrations, business rules |

**Project references:** Core.Infrastructure + enabled `{Module}.Infrastructure` only — never Domain projects directly.

**Startup flow:**

```text
Program.cs
  → AddInfrastructure()           Core platform
  → AddMetaForgeModules()         Hrm + Production + resolver
  → MigrateAllModulesAsync()      Core + each IMetaForgeModule DbContext
```

## Discovery rules

`EntityMetadataDiscoveryService` treats an entity as a **feature** when its namespace matches rules in `MetaForge.Shared.Constants.FeatureDiscoveryConstants`:

- `MetaForge.Domain.Features.*` (legacy in-core)
- `MetaForge.Domain.Business.*` (legacy)
- `MetaForge.{Module}.Domain.*` (module entities)

Framework namespaces (`Metadata`, `Security`, `Audit`) are excluded.

## Dependency rules

```text
MetaForge.Shared                         ← nothing
MetaForge.Modules.Abstractions           ← minimal (DI + EF contract types)
MetaForge.Core.Domain                    ← optional Shared
MetaForge.Core.Application               ← Domain + Shared + Abstractions
MetaForge.Core.Infrastructure            ← Application + Domain + Shared + Abstractions
MetaForge.{Module}.Domain                ← Core.Domain + Shared
MetaForge.{Module}.Infrastructure        ← Module.Domain + Abstractions (+ Core.Application if needed)
MetaForge.Web                            ← Core.Infrastructure + enabled Module.Infrastructure
```

**Forbidden:**

- Shared → any other MetaForge project
- Abstractions → Core.Domain, module Domain, Web
- Core → Hrm/Production types (use `IMetaForgeModule` + entity name strings)
- Web → direct Domain or DbContext usage in controllers
- Module Domain → Infrastructure or Web

Architecture tests in `tests/MetaForge.UnitTests/Architecture/` enforce key rules.

## Adding a new business module

1. Scaffold or create `MetaForge.{Name}.Domain`, `.Application`, `.Infrastructure`.
2. Implement `IMetaForgeModule` in `{Name}.Infrastructure`.
3. Add `services.Add{Name}Module(configuration)` in `Web/Modules/MetaForgeModuleRegistration.cs`.
4. Add project reference in `MetaForge.Web.csproj` to `{Name}.Infrastructure`.
5. Optional: `Areas/{Name}/` for custom pages.
6. Scaffold entities → Form Builder → menu → permissions.

Use the CLI or WinForms scaffold (`tools/MetaForge.Scaffold*`) — it generates the module pattern automatically.

```bash
dotnet run --project tools/MetaForge.Scaffold -- scaffold module --name Inventory --dry-run
```

New modules default to **`src/Modules/{Name}/`** on disk and **`/src/Modules/{Name}/`** in `MetaForge.slnx`. Legacy modules keep their `folder` value in `metaforge.json` (e.g. `src/Hrm`).

Entity scaffold paths always come from the module entry in `metaforge.json` (`domainOutputDir`, `configOutputDir`, `entityNamespace`).

## Adding a feature entity (within a module)

1. Add entity under `src/{Module}/MetaForge.{Module}.Domain/Entities/`.
2. Add EF configuration under `{Module}.Infrastructure/Persistence/Configurations/`.
3. Run migration for the module DbContext.
4. Form Builder → discover → configure → menu → permissions.

No new controller required for standard master / tabbed / master-detail screens.

## Adding a legacy in-core feature entity

For entities still in Core.Domain (not recommended for new work):

1. Add under `src/Core/MetaForge.Core.Domain/Features/{Area}/`.
2. Use namespace `MetaForge.Domain.Features.{Area}`.
3. Add EF configuration in Core.Infrastructure and register in `MetaForgeDbContext`.
4. Add Core migration.

## Sample modules in this repo

| Module | Location | Notes |
|---|---|---|
| Hrm | `src/Hrm/` | Module DbContext, schema `hrm`, Web Area |
| Production | `src/Production/` | Module DbContext, Web Area |
| Master Data / Sales / Education | Core.Domain `Features/` | Legacy in-core demo entities |

Demo entities in Core.Domain are reference data for tests and Form Builder demos, not part of the reusable platform package.
