# MetaForge Admin Platform

Metadata-driven ASP.NET Core MVC admin platform for .NET 10. MetaForge discovers EF Core entities (platform and module DbContexts), generates form and grid configuration, and renders dynamic CRUD, master-detail, tree, report, and security screens at runtime — without hand-coding every back-office screen.

---

## Table of Contents

- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [User Manual](#user-manual)
- [API Reference](#api-reference)
- [MVC Routes](#mvc-routes)
- [Extending MetaForge](#extending-metaforge)

---

## Architecture

MetaForge follows **Clean Architecture** with a metadata-first admin shell. Platform metadata and services live under `Core/`; business entities live in **modules** under `Modules/`. The Web host renders screens from cached `ForgeForm` definitions stored in SQL Server.

### Solution structure

```text
MetaForge.slnx
├── src/
│   ├── BuildingBlocks/
│   │   ├── MetaForge.Shared/              Constants, exceptions, culture helpers (no dependencies)
│   │   └── MetaForge.Modules.Abstractions/  IMetaForgeModule, IModuleDbContextResolver
│   ├── Core/
│   │   ├── MetaForge.Core.Domain/         Platform entities (metadata, security, audit, email)
│   │   ├── MetaForge.Core.Application/    Service interfaces, DTOs, contracts
│   │   └── MetaForge.Core.Infrastructure/ MetaForgeDbContext, generic CRUD, discovery, auth
│   ├── Modules/
│   │   └── Hrm/                           Domain, Application, Infrastructure (sample module)
│   └── Hosts/
│       └── MetaForge.Web/                 Composition root (MVC, API, module wiring, Areas/)
├── tools/
│   ├── MetaForge.Scaffold/                CLI scaffold (module + entity)
│   ├── MetaForge.Scaffold.Core/           Shared scaffold logic
│   └── MetaForge.Scaffold.WinForms/       Windows Forms scaffold UI
└── tests/
    ├── MetaForge.UnitTests/
    └── MetaForge.Scaffold.Tests/
```

**Framework vs features:** The platform (metadata-driven CRUD, security, menus, reports, email) is the **framework** under `Core/`. Business capabilities (Hrm, Inventory, …) are **modules** under `Modules/`. Full boundaries and folder rules: [docs/architecture/framework-vs-features.md](docs/architecture/framework-vs-features.md).

Enabled modules are listed in [`metaforge.json`](metaforge.json) at the solution root (currently **Hrm**).

### Layer dependencies

```text
MetaForge.Shared                    ← references nothing
MetaForge.Modules.Abstractions      ← module plugin contracts only
        ↑
MetaForge.Core.Domain               ← platform domain (no EF / ASP.NET)
        ↑
MetaForge.Core.Application          ← interfaces, DTOs, contracts
        ↑
MetaForge.Core.Infrastructure       ← EF Core, services, ModuleDbContextResolver
        ↑
MetaForge.{Module}.Infrastructure   ← module DbContext + IMetaForgeModule
        ↑
MetaForge.Web                       ← MVC, API, middleware, AddMetaForgeModules()
```

| Layer | Responsibility | Must not contain |
|---|---|---|
| **Shared** | Permission constants, discovery rules, shared exceptions, culture helpers | Service interfaces, EF, module logic |
| **Modules.Abstractions** | `IMetaForgeModule`, `IModuleDbContextResolver` contracts | DbContext implementations, controllers |
| **Core.Domain** | Metadata (`ForgeForm`, `ForgeReport`, …), RBAC, audit, email, `BaseEntity` | EF Core, ASP.NET, service logic |
| **Core.Application** | Service interfaces, DTOs, repository contracts | EF Core, infrastructure implementations |
| **Core.Infrastructure** | `MetaForgeDbContext`, generic CRUD, grid/export, discovery, auth, caching, reports | UI or controller code |
| **Module.*** | Business entities, module DbContext, `IMetaForgeModule` implementation | Direct references from Core to module types |
| **Web** | `ModuleController`, Form Builder, Report Builder, Security UI, REST API, `Areas/{Module}/` overrides | Direct database access, business rules |

### Metadata model

All dynamic screens are driven by database-backed metadata:

| Entity | Purpose |
|---|---|
| `ForgeForm` | Maps a screen to an EF entity — code, name, group, form type, display order |
| `ForgeField` | Form field definition — property, label, control type, lookup, cascade, validation, conditional rules |
| `ForgeGridColumn` | List grid column — property, label, sortable, searchable, visible |
| `ForgeRelation` | Parent/child relationship — OneToOne, OneToMany, ManyToOne, tab labels |
| `ForgeTreeLevel` | Multi-level tree grid — entity, parent key, display field per level |
| `ForgeMenu` | Hierarchical sidebar — folders, form links, external URLs |
| `LookupConfiguration` | Display/value field mapping for dropdown lookups |
| `ForgeReport` | Report definition — entity, columns, filters, grouping, aggregates |

### Form types

| Type | Use case |
|---|---|
| **Master** | Standard list + form CRUD (e.g. Department, Product) |
| **Tabbed** | Single-entity form with fields grouped in tabs via `SectionName` |
| **MasterDetail** | Header with a single inline detail grid (e.g. order + line items) |
| **MasterDetailTabular** | Header with multiple child grids in tabs (e.g. Sales Order → Line Items + Charges) |
| **TreeViewMultiTable** | Hierarchical tree grid across related entities (e.g. Country → Region → City) |
| **Detail** | Child entity form used by master-detail screens (not shown as a standalone module) |

### Core services

| Service | Role |
|---|---|
| `EntityMetadataDiscoveryService` | Scans module entity namespaces (`MetaForge.{Module}.Domain.Entities`), legacy `MetaForge.Domain.Features.*`, and `MetaForge.Domain.Business`; auto-generates draft form config |
| `FormConfigurationService` | Form Builder CRUD — fields, columns, relations, tree levels, screen preview |
| `FormHealthCheckService` | Validates form metadata against live EF schema (missing properties, stale relations) |
| `FormMetadataService` | Loads cached form definitions for runtime rendering |
| `GenericCrudService` | Dynamic create/read/update/delete against any configured entity |
| `GridService` | Paging, sorting, search, Excel/CSV export |
| `MasterDetailService` | Loads and saves master + detail (and tabular multi-detail) transactions |
| `TreeGridService` | Multi-level tree navigation and lazy-loaded child nodes |
| `LookupService` | Dropdown data with optional cascade filtering and search |
| `ReportConfigurationService` / `ReportService` | Report Builder metadata and runtime execution (tabular, grouped, summary) with Excel/PDF export |
| `FormAuthorizationService` | Form-level and system-level permission checks |
| `MenuManagementService` / `MenuSyncService` | Sidebar tree administration and form-to-menu linking |
| `SecurityManagementService` | Users, roles, permissions administration |
| `AuditService` | Queued audit log writes and query API |
| `EmailTemplateService` / `EmailDispatchService` | Template-based outbound email with channels, retry policies, and delivery log |

### Runtime flow

```text
1. Discovery   EntityMetadataDiscoveryService scans EF Core entities (core + module DbContexts)
2. Configure   Form Builder / Report Builder persist metadata to SQL Server
3. Authorize   Role + form-level permissions gate every module and API action
4. Render      Web layer loads cached metadata → dynamic grid, form, master-detail, tree, or report UI
5. Persist     GenericCrudService / MasterDetailService write through EF Core
```

### Platform stack

- **Runtime:** .NET 10, ASP.NET Core MVC
- **ORM:** Entity Framework Core (SQL Server) with code-first migrations
- **Auth:** Cookie authentication + RBAC + security-stamp validation
- **Validation:** FluentValidation (dynamic rules from metadata) + conditional field rules
- **UI:** Bootstrap 5, jQuery, Font Awesome, DataTables
- **Export:** Excel and CSV from grid metadata; Excel and PDF from report metadata
- **Email:** MailKit / SendGrid providers with template rendering
- **Logging:** Serilog (console + rolling file)

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB or full instance)
- Update the connection string in `src/MetaForge.Web/appsettings.json` if needed:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=MetaForgeDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

---

## Getting Started

```bash
dotnet restore MetaForge.slnx
dotnet build MetaForge.slnx
dotnet run --project src/MetaForge.Web
```

Open the app in your browser (typically `https://localhost:5001` or the URL shown in the console).

### Demo credentials

| Username | Password | Role |
|---|---|---|
| `admin` | `admin` | Administrator (full access) |

### Database

MetaForge uses **SQL Server** (LocalDB or a full instance). Point `ConnectionStrings:DefaultConnection` in `src/MetaForge.Web/appsettings.json` at your server. The database is created automatically when migrations run (on startup or via the commands below).

On startup, MetaForge applies pending **EF Core migrations** for the core and enabled modules, then runs idempotent **platform** seeding (admin user, security permissions, email defaults, system settings, menus).

```bash
# Apply migrations and seed (no web server)
dotnet run --project src/MetaForge.Web -- --seed-only

# Reset and reseed the database (drops all data, reapplies migrations)
dotnet run --project src/MetaForge.Web -- --reset-db
```

```bash
# Apply core migrations manually
dotnet ef database update --project src/Core/MetaForge.Core.Infrastructure --startup-project src/MetaForge.Web --context MetaForgeDbContext

# Add a core migration after changing platform entities
dotnet ef migrations add <MigrationName> --project src/Core/MetaForge.Core.Infrastructure --startup-project src/MetaForge.Web --output-dir Persistence/Migrations --context MetaForgeDbContext

# Apply module migrations (example: Hrm)
dotnet ef database update --project src/Modules/Hrm/MetaForge.Hrm.Infrastructure --startup-project src/MetaForge.Web --context HrmDbContext
```

Core migrations live in `src/Core/MetaForge.Core.Infrastructure/Persistence/Migrations/`.

On first run, MetaForge creates the database and seeds the **admin** account plus platform defaults. Add module entities with the scaffold tool (see below), then configure forms in Form Builder.

---

## User Manual

### 1. Sign in

1. Open the landing page and click **Open Dashboard** (or go to `/Account/Login`).
2. Sign in with `admin` / `admin`.
3. You are redirected to the dashboard with the sidebar navigation.

Forgot-password and reset flows are available at `/Account/ForgotPassword` and `/Account/ResetPassword`.

### 2. Navigation

The left sidebar is a **hierarchical tree menu** built from `ForgeMenu` records:

- **Folders** — group related modules (e.g. Master Data, Transaction)
- **Form links** — open a dynamic module at `/Modules/{formCode}`
- **URL links** — external or custom links

Administrators manage menus at **Menu** (`/Menu`).

### 3. Working with dynamic modules

Each configured form appears as a module under `/Modules/{formCode}`.

#### List grid

- **Search** — filter across searchable columns
- **Sort** — click column headers
- **Paging** — navigate large datasets
- **Create** — opens a new record (requires Create permission)
- **Edit / Delete** — row actions (requires Edit/Delete permission)
- **Export** — Excel or CSV (requires Export permission)

#### Standalone form

Open `/Modules/{formCode}/Form/{id?}` for a full-page form. Omit `id` to create; include `id` to edit.

Supported control types: TextBox, TextArea, Number, Date, DateTime, Checkbox, Dropdown, Radio, FileUpload, Hidden.

Dropdowns load from `/api/metaforge/lookups/{entityName}`. Cascade lookups (e.g. Region filtered by Country) are configured in the Form Builder.

#### Master-detail screens

| Screen type | Behavior |
|---|---|
| **MasterDetail** | Master fields at top; one inline detail grid below |
| **MasterDetailTabular** | Master fields at top; multiple child grids in tabs |

- Click **Create** or **Edit** on a grid row to open the master-detail panel.
- Add, edit, or remove detail lines inline.
- **Save** persists the header and all detail rows atomically.

#### Tree screens

**TreeViewMultiTable** screens show a multi-level hierarchy (e.g. Country → Region → City). Each level is a related entity configured in Form Builder. Expand nodes to lazy-load children.

### 4. Form Builder

Access at **Form Builder** (`/FormBuilder` or `/ModuleConfig`). Requires `config.View` / `config.Manage` permissions.

#### Create a new module

1. Click **New Form**.
2. **Screen Setup**
   - Select a **Database Entity** from discovered EF types (core or module DbContexts)
   - Choose **Screen Type** (Master, Tabbed, Master + Detail, Master + Tabular Details, or Tree)
   - Set **Group**, **Code**, and **Display Name**
   - Click **Auto-Build** to draft fields, grid columns, and relations from EF metadata
3. Configure tabs:
   - **Master Form** — field labels, control types, lookups, cascade, validation rules, conditional rules, read-only flags
   - **Detail Form** — (MasterDetail / Tabular only) child line field layout
   - **List Grid** — visible columns, sortable/searchable flags
   - **Relations** — parent/child mappings for master-detail
   - **Tree Levels** — (Tree only) entity and parent-key mapping per level
4. **Validation rules** (Master Form / Detail Form tabs):
   - Scroll right in the field table to the **Validation** column
   - Click the **Rules** button on any field row
   - Add rules (max/min length, numeric range, email, phone, URL, regex, compare to another field)
   - Click **Apply Rules**, then **Save Form**
5. **Save** — the module is stored and can be linked to the sidebar menu.

#### Form health check

At **Form Builder → Health** (`/FormBuilder/Health`), review metadata drift — missing EF properties, stale relations, or entities no longer registered in any DbContext.

#### Edit an existing module

Open **Form Builder** → select a form → **Edit**. Entity name is locked after creation; fields, grid, relations, and tree levels can be updated.

### 5. Report Builder

Access at **Report Builder** (`/ReportBuilder`). Requires `reportconfig.View` / `reportconfig.Manage` permissions.

1. Create a report linked to a discovered entity.
2. Configure columns, filters, grouping, and summary aggregates.
3. Run the report at `/Reports/{reportCode}`.
4. Export to Excel or PDF from the report screen or API.

### 6. Menu administration

At **Menu** (`/Menu`):

1. Create **Folder** nodes to organize the sidebar.
2. Create **Form** items linked to a `ForgeForm` — URL is generated automatically (`/Modules/{code}`).
3. Create **URL** items for custom links.
4. Set **Display Order** and **Parent** to build the tree.
5. Toggle **Active** to show or hide items.

Use **Sync** (Security → Permissions) after adding forms to generate form-level permission records.

### 7. Security administration

At **Security** (`/Security`):

| Area | Path | Actions |
|---|---|---|
| **Users** | `/Security/Users` | Create/edit users, assign roles, set passwords |
| **Roles** | `/Security/Roles` | Create roles, assign permission groups |
| **Permissions** | `/Security/Permissions` | View all permissions; sync new form permissions |
| **Audit log** | `/Security/Audit` | Search audit entries by entity, action, user, date |

#### Permission model

**Form-level permissions** (per module):

- View, Create, Edit, Delete, Export, Approve

**System permissions:**

- `config.View` / `config.Manage` — Form Builder access
- `reportconfig.View` / `reportconfig.Manage` — Report Builder access
- `emailconfig.View` / `emailconfig.Manage` — Email administration
- `systemsettings.View` / `systemsettings.Manage` — System settings
- `security.ViewUsers`, `security.ManageUsers`, etc. — Security administration

Permissions are enforced on both MVC pages and REST API endpoints. Users without access see a forbidden response or hidden UI actions.

### 8. Email administration

At **Email Admin** (`/EmailAdmin`):

- **Channels** — SMTP / SendGrid delivery channels
- **Retry policies** — backoff rules for failed sends
- **Templates** — HTML/text templates with token bindings
- **Log** — outbound message history

### 9. System settings & user preferences

- **System settings** (`/SystemSettings`) — localization defaults, appearance, and platform-wide options (requires `systemsettings.View` / `systemsettings.Manage`).
- **User preferences** (`/Account/Preferences`) — culture and date/time format overrides per user.
- **Appearance** (`/Account/Appearance`) — theme selection (stored per user).

### 10. Typical workflows

#### Add a master-data screen (module entity)

1. Scaffold an entity into a module (e.g. Hrm) or add it manually under `MetaForge.{Module}.Domain.Entities`.
2. Run the app (applies module migrations) or apply migrations manually.
3. Open Form Builder → New Form → select entity → Auto-Build → Save.
4. Add a menu item under **Menu**.
5. Sync permissions in Security → assign to roles.

#### Build a transaction screen

1. Create master and child entities with a OneToMany relationship.
2. In Form Builder, choose **Master + Detail** or **Master + Tabular Details**.
3. Configure master fields, detail line fields, and relations.
4. Publish via menu and test Create/Edit/Save on the module grid.

#### Restrict access for a role

1. Security → Roles → Edit role.
2. Uncheck unwanted form permissions (e.g. Delete, Export).
3. Users with that role immediately lose those actions in the UI and API.

---

## API Reference

All endpoints require authentication (cookie session) unless noted. Form-scoped endpoints also require the matching form permission.

### Form catalog & discovery

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/metaforge/form-catalog` | Sidebar navigation menu |
| `GET` | `/api/metaforge/form-catalog/{formCode}/permissions` | Current user's permissions for a form |
| `GET` | `/api/metaforge/form-catalog/discover` | List all discoverable business entities |
| `POST` | `/api/metaforge/form-catalog/discover/{entityName}` | Auto-generate draft form config for an entity |

### Form configuration

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/metaforge/formconfig` | All form definitions |
| `GET` | `/api/metaforge/formconfig/{id}` | Form by ID |
| `GET` | `/api/metaforge/formconfig/screen/{id}` | Full screen definition (fields, grid, relations, tree levels) |
| `GET` | `/api/metaforge/formconfig/discovered` | Discoverable entities |
| `GET` | `/api/metaforge/formconfig/draft/{entityName}` | Draft config for an entity |
| `GET` | `/api/metaforge/formconfig/health` | Platform-wide form health report |
| `POST` | `/api/metaforge/formconfig` | Save form configuration |

### Form & grid definitions (runtime)

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/metaforge/forms/{formCode}` | Form field definition |
| `GET` | `/api/metaforge/grid/{formCode}` | Grid column definition |
| `POST` | `/api/metaforge/grid/data` | Grid data (paging, sort, search) |
| `GET` | `/api/metaforge/grid/{formCode}/export/excel` | Excel export |
| `GET` | `/api/metaforge/grid/{formCode}/export/csv` | CSV export |

### Generic CRUD

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/metaforge/crud/{entity}/{id}` | Get record by ID |
| `POST` | `/api/metaforge/crud/{entity}` | Create record |
| `PUT` | `/api/metaforge/crud/{entity}/{id}` | Update record |
| `DELETE` | `/api/metaforge/crud/{entity}/{id}` | Delete record |

### Master-detail

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/metaforge/masterdetail/{formCode}/{id?}` | Load master-detail screen |
| `POST` | `/api/metaforge/masterdetail/{formCode}` | Save master + details (supports `DetailSections` for tabular) |
| `DELETE` | `/api/metaforge/masterdetail/{formCode}/detail/{detailId}` | Delete a detail row |

### Tree grids

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/metaforge/tree/{formCode}/screen` | Tree screen definition |
| `POST` | `/api/metaforge/tree/{formCode}/level` | Lazy-load nodes for a tree level |

### Lookups

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/metaforge/lookups/{entityName}` | Dropdown items; optional `filterField` + `filterValue` for cascade |
| `GET` | `/api/metaforge/lookups/{entityName}/search` | Typeahead search |
| `GET` | `/api/metaforge/lookups/{entityName}/item/{value}` | Single item by value |

### Reports

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/metaforge/reports/{reportCode}` | Run report (query params for filters) |
| `GET` | `/api/metaforge/reports/{reportCode}/export/excel` | Excel export |
| `GET` | `/api/metaforge/reports/{reportCode}/export/pdf` | PDF export |
| `GET` | `/api/metaforge/reportconfig` | Report Builder catalog |
| `GET` | `/api/metaforge/reportconfig/draft/{entityName}` | Draft report for an entity |

### Security & audit

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/metaforge/security/overview` | Security dashboard counts |
| `GET` | `/api/metaforge/security/users` | User list |
| `GET` | `/api/metaforge/security/roles` | Role list |
| `GET` | `/api/metaforge/security/permissions` | Permission catalog |
| `GET` | `/api/metaforge/audit` | Search audit log entries |

### Email, settings & preferences

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/metaforge/emailconfig/channels` | Email delivery channels |
| `GET` | `/api/metaforge/emailconfig/templates` | Email templates |
| `GET` | `/api/metaforge/systemsettings/localization` | System localization settings |
| `GET` | `/api/metaforge/userpreferences/theme` | Current user's theme |

---

## MVC Routes

| Route | Description |
|---|---|
| `/` | Landing page |
| `/Account/Login` | Sign in |
| `/Account/ForgotPassword`, `/Account/ResetPassword` | Password reset flow |
| `/Account/Preferences`, `/Account/Appearance` | User culture and theme |
| `/Modules/{formCode}` | Dynamic list/grid screen (master-detail panel when applicable) |
| `/Modules/{formCode}/Form/{id?}` | Standalone create/edit form |
| `/Modules/{formCode}/MasterDetail/{id?}` | Redirects to grid with master-detail panel open |
| `/FormBuilder`, `/ModuleConfig` | Form Builder list |
| `/FormBuilder/Create`, `/ModuleConfig/Create` | New form wizard |
| `/FormBuilder/Edit/{id}`, `/FormBuilder/Health` | Edit form metadata; health check |
| `/ReportBuilder` | Report Builder list and editor |
| `/Reports/{reportCode}` | Run a configured report |
| `/Menu` | Navigation menu administration |
| `/Security` | Security overview |
| `/Security/Users`, `/Security/Roles`, `/Security/Permissions`, `/Security/Audit` | RBAC and audit management |
| `/EmailAdmin` | Email channels, templates, and delivery log |
| `/SystemSettings` | Platform-wide settings |

---

## Extending MetaForge

### Add a new business module

Scaffold a full module slice (Domain, Application, Infrastructure, DbContext, Web wiring):

```bash
# Preview
dotnet run --project tools/MetaForge.Scaffold -- scaffold module --name Inventory --dry-run

# Create module under src/Modules/Inventory/, patch MetaForge.slnx, metaforge.json, and MetaForge.Web
dotnet run --project tools/MetaForge.Scaffold -- scaffold module --name Inventory --migration
```

New modules are created at **`src/Modules/{Name}/`** and registered in the solution, [`metaforge.json`](metaforge.json), and `MetaForge.Web/Modules/MetaForgeModuleRegistration.cs`.

| Output | Location |
|---|---|
| Domain / Application / Infrastructure projects | `src/Modules/{Name}/MetaForge.{Name}.*` |
| DbContext + `IMetaForgeModule` | `src/Modules/{Name}/MetaForge.{Name}.Infrastructure/` |
| Web area (optional) | `src/MetaForge.Web/Areas/{Name}/` |
| Solution + host wiring | `MetaForge.slnx`, `metaforge.json`, `MetaForge.Web.csproj`, `Modules/MetaForgeModuleRegistration.cs` |

### Add a new business entity

#### Option A — Scaffold (recommended)

Module paths and namespaces come from [`metaforge.json`](metaforge.json) at the solution root. Use **`--module Hrm`** (CLI) or the **Target module** dropdown (WinForms).

From the solution root, with SQL Server reachable via `appsettings.json` or `METAFORGE_CONNECTION`:

```bash
# Greenfield into Hrm module (entity + EF config with schema hrm)
dotnet run --project tools/MetaForge.Scaffold -- scaffold entity --module Hrm \
  --name LeaveRequest --columns "RequestNo:string:20!,FullName:string:200"

# Reverse scaffold from an existing table
dotnet run --project tools/MetaForge.Scaffold -- scaffold entity --module Hrm --table LeaveRequests

# Preview without writing files
dotnet run --project tools/MetaForge.Scaffold -- scaffold entity --module Hrm \
  --name LeaveRequest --columns "RequestNo:string:20!,FullName:string:200" --dry-run

# Add EF migration for HrmDbContext in the same step
dotnet run --project tools/MetaForge.Scaffold -- scaffold entity --module Hrm \
  --name LeaveRequest --columns "RequestNo:string:20!,FullName:string:200" --migration
```

Generated output (example Hrm module):

| File | Location |
|---|---|
| Entity | `src/Modules/Hrm/MetaForge.Hrm.Domain/Entities/{Entity}.cs` |
| EF config | `src/Modules/Hrm/MetaForge.Hrm.Infrastructure/Persistence/Configurations/Generated/{Entity}Configuration.cs` |

Namespaces: `MetaForge.Hrm.Domain.Entities` and `MetaForge.Hrm.Infrastructure.Persistence.Configurations.Generated`. Tables use schema **`hrm`**. DbSet patch is **skipped by default** (EF discovers entities via configuration).

Then:

1. `dotnet build`
2. Run the app (applies module migrations) or `dotnet ef database update --context HrmDbContext ...`
3. Form Builder → select entity → **Auto-Build** → Save
4. Menu item + role permissions

#### Windows Forms UI

```bash
dotnet run --project tools/MetaForge.Scaffold.WinForms
```

1. Set **Solution folder** (contains `MetaForge.slnx` and `metaforge.json`)
2. Select **Target module** (e.g. Hrm)
3. Greenfield or reverse mode → **Preview** → **Scaffold**
4. Leave **Do not add DbSet** checked for modules; use **Create EF migration** to update `HrmDbContext`

**Load conn...** reads `DefaultConnection` from `src/MetaForge.Web/appsettings.json`.

Install as a local CLI tool (optional, after pack):

```bash
dotnet pack tools/MetaForge.Scaffold -o .artifacts/tools
dotnet tool install --local MetaForge.Scaffold --add-source .artifacts/tools
dotnet metaforge scaffold entity --help
```

**Requirements:** Primary key must be named `Id` and use a supported type (`int`, `long`, `Guid`, or `string`). Prefer `int` unless you need another key. System tables (`Forge*`, `Users`, etc.) cannot be scaffolded.

#### Option B — Manual (legacy core features folder)

1. Create a class in `src/Core/MetaForge.Core.Domain/Features/{Area}/` with namespace `MetaForge.Domain.Features.{Area}`, inheriting from `BaseEntity` (int) or `BaseEntity<TKey>` (e.g. `BaseEntity<Guid>`).
2. Add a `DbSet<T>` and configure relationships in `MetaForgeDbContext`.
3. Restart the app — the entity appears in Form Builder's entity list.
4. Use **Auto-Build** or POST `/api/metaforge/form-catalog/discover/{entityName}` to generate metadata.
5. Link the form to a menu item and sync permissions.

**Preferred for new work:** place entities in a module (`MetaForge.{Module}.Domain.Entities`) so each domain has its own DbContext and schema.

Discovery accepts:

- `MetaForge.{Module}.Domain.Entities` (module entities)
- `MetaForge.Domain.Features.*` (legacy core features folder)
- `MetaForge.Domain.Business` (legacy namespace)

### Metadata cache

Form definitions are cached in memory (`MetadataCache` in `appsettings.json`):

```json
"MetadataCache": {
  "AbsoluteExpirationMinutes": 60,
  "SlidingExpirationMinutes": 15,
  "NotFoundExpirationMinutes": 2
}
```

Restart the app or wait for cache expiry after major metadata changes in development.

---

## License

Internal / project use. Update this section if you publish under a specific license.
