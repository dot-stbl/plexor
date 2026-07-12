---
description: "When 3+ entities share a property (CreatedAt, UpdatedAt, TenantId, IsDeleted, etc.), extract the property to a shared interface in Plexor.Shared.Kernel.Common. Entity declares the interface; queries and DI bind to the interface."
globs: ["**/*.cs"]
priority: medium
---

# Common fields on shared interfaces

> **Properties that appear on 3+ entities belong on a shared interface, not duplicated per entity.** Interface composition over copy-paste gives generic queries (`where T : ICreatedAt`), test doubles (`Mock<ICreatedAt>`), and a single place to add a cross-cutting field (e.g. `ITenantScoped` for tenant isolation).

## The rule

Audit entity classes for repeated properties. When 3+ entities share a property:

1. **Extract to a shared interface** in `src/shared/Plexor.Shared.Kernel/Common/`.
2. **Entity declares the interface** alongside `IFilterableEntity`: `public sealed class User : IFilterableEntity, ICreatedAt, IUpdatedAt`.
3. **No shared base class** — interfaces compose, base classes don't. A future entity that needs only `ICreatedAt` shouldn't pull in `IUpdatedAt` for free.
4. **Split the interface** if the shared field is conditional. `ICreatedAt` and `IUpdatedAt` are separate; append-only entities (RefreshToken, SigningKey, AuditEntry, RoleBinding) implement only `ICreatedAt`, not both.

## Existing shared interfaces

| Interface | Property | Used by |
|---|---|---|
| `ICreatedAt` | `DateTimeOffset CreatedAt { get; }` | User, Role, ApiKey, RoleBinding, RefreshToken, SshKey, SigningKey, AuditEntry, Tenant |
| `IUpdatedAt` | `DateTimeOffset UpdatedAt { get; }` | User, Role (mutable entities only) |
| `IFilterableEntity` | (marker) | every entity exposed to the FE filter DSL |

## Examples

```csharp
// ✓ Correct — composition
public interface ICreatedAt
{
    DateTimeOffset CreatedAt { get; }
}

public sealed class User : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class RefreshToken : IFilterableEntity, ICreatedAt
{
    public DateTimeOffset CreatedAt { get; init; }
    // No UpdatedAt — refresh tokens are append-only, no mutation concept.
}
```

```csharp
// ✗ Wrong — base class with optional fields
public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }  // null for append-only
}

public sealed class User : AuditableEntity { ... }  // UpdatedAt? always null?
```

## Self-audit grep

```bash
rg -n "public DateTimeOffset CreatedAt" src/ --type cs
rg -n "public DateTimeOffset UpdatedAt" src/ --type cs
rg -n "public Guid TenantId" src/ --type cs
# → If 3+ entities declare the same property, extract to interface.
```