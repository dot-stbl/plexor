---
description: hybrid migrator CLI — `seed` sub-command, environment selection, EF Core migration owner
globs: ["src/host/Hybrid.Migrator/**", "src/shared/Hybrid.Shared.Configuration/**"]
priority: high
always: true
---

# Migrator CLI — `seed` sub-command and EF Core migrations

The migrator (`src/host/Hybrid.Migrator`) is a one-shot deployment tool that runs
BEFORE `Hybrid.Host`. It applies EF Core migrations to every module database and
(optionally) seeds bootstrap data. Two CLI modes, selected by argv[0]:

```
dotnet run --project src/host/Hybrid.Migrator                            # default
dotnet run --project src/host/Hybrid.Migrator -- seed [--list|<name>]     # seed sub-command
```

Both modes apply pending migrations first (`MigratorPipeline.ApplyMigrationsAsync`).
Migrations are idempotent at the EF Core level — re-running against an up-to-date
schema is a logged no-op per DbContext.

## CLI surface

| Form | Effect |
|---|---|
| `dotnet run` | migrate + admin auto-seed (default Phase 1 + Phase 2 of `MigrationRunner`) |
| `dotnet run -- seed --list` | print catalog of registered `ISeeder` instances |
| `dotnet run -- seed` | run **every** registered `ISeeder` (in registration order) |
| `dotnet run -- seed <name>` | run the `ISeeder` whose `Name == <name>` (case-insensitive) |
| `dotnet run -- seed nonexistent` | exit code **2** with `seed: unknown seeder name=...` |

The seed sub-command is dispatched by `Hybrid.Migrator.Seeders.SeedDispatcher`
(`IHostedService`) — the **only** one. Both Phase 1 migrate and the seeder
resolution go through shared helpers:

- `MigratorPipeline.ApplyMigrationsAsync(services, logger)` — applies all 6 module
  DbContexts in FK-dependency order (Outbox → Tenants → Identity → Campaigns →
  AdLibrary → Tasks).
- `services.GetServices<ISeeder>()` — DI lookup, keyed by `ISeeder.Name`.

### Exit codes

| Code | Meaning |
|---|---|
| `0` | Success (or transient infra failure that was logged+swallowed). |
| `2` | Unknown seeder name (typo or unregistered). |
| `3` | Seeder configuration error (missing required settings). |

## Environment selection — `DOTNET_ENVIRONMENT`, NOT `ASPNETCORE_ENVIRONMENT`

`UseHybridEnvironmentVariablesOnly()` (`Hybrid.Shared.Configuration`) drops the
default no-prefix env source. The Hybrid Host uses **`Microsoft.Extensions.Hosting`
(Generic Host)**, NOT `WebApplication.CreateBuilder`, so environment selection
goes through **`DOTNET_ENVIRONMENT`**, not `ASPNETCORE_ENVIRONMENT`.

```bash
# Correct — flips to appsettings.Development.json, `Auth:Seed:Enabled=true`
DOTNET_ENVIRONMENT=Development dotnet run --project src/host/Hybrid.Migrator -- seed samples

# Wrong — silently stays in Production, admin seed no-ops, no error
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/host/Hybrid.Migrator
```

The XML doc on `UseHybridEnvironmentVariablesOnly` previously claimed both worked;
that is wrong and has been corrected.

## Adding a new seeder

1. Implement `ISeeder` in `src/host/Hybrid.Migrator/Seeders/<Name>Seeder.cs`:
   ```csharp
   public sealed class FooSeeder(IServiceScopeFactory scopeFactory, ...) : ISeeder
   {
       public string Name => "foo"; // kebab-case, unique across registered seeders
       public async Task SeedAsync(CancellationToken ct) { ... }
   }
   ```
2. Register in `Program.cs`:
   ```csharp
   builder.Services.AddSingleton<ISeeder, FooSeeder>();
   ```
3. If the seeder needs modules not already registered (e.g. Billing), wire the
   module's DbContext registration in `Program.cs` too.

## EF Core migrations — owner of `Migrations/` per module

Each module owns its own migrations folder; the migrator owns the apply pipeline:

```
src/modules/<Module>/Hybrid.Modules.<Module>.Infrastructure/Migrations/
├── <timestamp>_<Module>_InitialSchema.cs
├── <timestamp>_<Module>_InitialSchema.Designer.cs
└── <Module>DbContextModelSnapshot.cs
```

- **Adding a new migration**: `dotnet ef migrations add <Name> -c <DbContext> -p <Infrastructure.csproj> -s src/host/Hybrid.Migrator`
- **Order is fixed** by FK dependencies: `Outbox → Tenants → Identity → Campaigns → AdLibrary → Tasks`. Do NOT change the order in `MigratorPipeline.ApplyMigrationsAsync` without reviewing every cross-module FK reference.
- **Hand-edits** are allowed for the `Up()` body when EF Core cannot express the schema (PostgreSQL views, partial indexes, custom types). The `.editorconfig` already disables IDE0058 / IDE0161 / MA0197 / CA1861 / IDE1006 for `[**/Migrations/*.cs]` — auto-format drift on these files is expected and accepted.

## Identity admin seed

- `Auth:Seed:Enabled=true` (in `appsettings.Development.json` or via
  `HYBRID_AUTH_SEED__ENABLED=true` env var) — required for the admin user to be
  created.
- The seeder **does not delete** — re-running is a logged no-op when the user
  already exists.
- Failure modes:
  - **Configuration error** (missing email, password < 8 chars, empty AgencyId)
    → fail the migrator non-zero.
  - **Transient infra error** (DB down, not migrated) → log warning, exit 0;
    deployer retries on the next pass.