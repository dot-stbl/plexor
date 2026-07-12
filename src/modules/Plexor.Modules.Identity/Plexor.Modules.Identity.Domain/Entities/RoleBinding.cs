using Plexor.Shared.Filtering;

using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Identity.Domain.Entities;

/// <summary>
///     Attaches one user to one role, optionally scoped to a project.
///     A user holds multiple bindings; their effective permissions are
///     the union of all bound roles' permissions.
/// </summary>
/// <remarks>
///     <para><b>Project scoping.</b> When <see cref="ProjectId" /> is
///     <c>null</c>, the binding applies tenant-wide. When set, the
///     binding only applies to operations scoped to that project.
///     Cross-project operations require a tenant-wide binding.</para>
///     <para><b>Uniqueness.</b> The DB enforces UNIQUE
///     (<c>user_id</c>, <c>role_id</c>, <c>project_id</c>) — same role
///     cannot be bound to the same scope twice.</para>
///     <para><b>Phase 2.</b> When the <c>Plexor.Modules.Tenants</c> module
///     grows the <c>Project</c> aggregate, this entity gains the
///     project FK. v0.1 keeps <see cref="ProjectId" /> nullable; the
///     column type is <c>UUID NULL</c>.</para>
/// </remarks>
public sealed class RoleBinding : IFilterableEntity, ICreatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant this binding belongs to (denormalized for
    /// tenant-scoped queries).</summary>
    public Guid TenantId { get; init; }

    /// <summary>User receiving the role.</summary>
    public Guid UserId { get; init; }

    /// <summary>Role being bound.</summary>
    public Guid RoleId { get; init; }

    /// <summary>Optional project scope. <c>null</c> = tenant-wide.</summary>
    public Guid? ProjectId { get; init; }

    /// <summary>When the binding was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }
}