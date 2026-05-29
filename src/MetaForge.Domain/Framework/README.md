# Framework domain (platform)

Platform entities live alongside this folder at the project root:

| Folder | Purpose |
|---|---|
| `Metadata/` | Form/grid/menu metadata (`ForgeForm`, `ForgeField`, …) |
| `Security/` | Users, roles, permissions |
| `Audit/` | Audit trail |
| `Common/` | `BaseEntity` |
| `Enums/` | Shared enumerations |

Do not add business tables here. See [framework-vs-features.md](../../../docs/architecture/framework-vs-features.md).
