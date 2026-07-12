using Plexor.Modules.Identity.Domain.ValueObjects;
using Plexor.Shared.Filtering;

namespace Plexor.Modules.Identity.Domain.Entities;

/// <summary>
///     Named permission set within a tenant. Bindings (see
///     <see cref="RoleBinding" />) attach users to roles; permissions
///     authorize actions on endpoints via <c>[RequirePermission]</c>.
/// </summary>
/// <remarks>
///     <para><b>Built-in roles.</b> The Migrator seeds two built-in
///     roles per tenant on first deploy:
///     <c>"admin"</c> with <c>*</c> permission, and <c>"viewer"</c>
///     with <c>*.read</c> permissions (Phase 2 refinement). Built-in
///     roles have <see cref="BuiltIn" /> = <c>true</c> and cannot be
///     deleted (DELETE returns 409).</para>
///     <para><b>Permissions list.</b> Each permission is a flat string
///     (no wildcards, no inheritance). The denormalized list is stored
///     in <see cref="Permissions" /> and copied verbatim into JWT claims
///     at sign time.</para>
/// </remarks>
public sealed class Role : IFilterableEntity
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant this role belongs to. Roles are tenant-scoped —
    /// two tenants cannot share a role.</summary>
    public Guid TenantId { get; init; }

    /// <summary>Human-readable role name, unique per tenant
    /// (<c>"admin"</c>, <c>"viewer"</c>, <c>"compute.editor"</c>, ...).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional description for admin UI.</summary>
    public string? Description { get; init; }

    /// <summary>Permission strings granted to users who bind this role.
    /// Stored as a Postgres TEXT[] column.</summary>
    public IReadOnlyList<PermissionScope> Permissions { get; init; } = [];

    /// <summary>True when the role was seeded by the Migrator and cannot
    /// be deleted by tenant admins.</summary>
    public bool BuiltIn { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}