using Plexor.Shared.Filtering.Registry;

using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Sigil.Domain.Entities;

/// <summary>
///     Attaches one user to one role, optionally scoped to a project.
///     A user holds multiple bindings; their effective permissions are
///     the union of all bound roles' permissions.
/// </summary>
/// <remarks>
///     <para><b>Folder scoping.</b> When <see cref="FolderId" /> is
///     <c>null</c>, the binding applies org-wide. When set, the
///     binding only applies to operations scoped to that project.
///     Cross-project operations require a org-wide binding.</para>
///     <para><b>Uniqueness.</b> The DB enforces UNIQUE
///     (<c>user_id</c>, <c>role_id</c>, <c>project_id</c>) — same role
///     cannot be bound to the same scope twice.</para>
///     <para><b>Phase 2.</b> When the <c>Plexor.Modules.Realm</c> module
///     grows the <c>Project</c> aggregate, this entity gains the
///     project FK. v0.1 keeps <see cref="FolderId" /> nullable; the
///     column type is <c>UUID NULL</c>.</para>
/// </remarks>
public sealed class RoleBinding : IFilterableEntity, ICreatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Organization this binding belongs to (denormalized for
    /// org-scoped queries).</summary>
    public Guid OrgId { get; init; }

    /// <summary>User receiving the role.</summary>
    public Guid UserId { get; init; }

    /// <summary>Role being bound.</summary>
    public Guid RoleId { get; init; }

    /// <summary>
    ///     Optional team scope. <c>null</c> = the binding applies
    ///     organization-wide (or organization-wide excluding teams in
    ///     Phase 2 when team-scoped bindings exist). When set, the
    ///     binding only applies to operations scoped to that team.
    /// </summary>
    public Guid? TeamId { get; init; }

    /// <summary>
    ///     Optional folder scope. <c>null</c> = the binding applies to
    ///     the org (or team if <see cref="TeamId" /> is set). When set,
    ///     the binding only applies to operations scoped to that
    ///     folder. Cross-folder operations require an org-wide binding.
    /// </summary>
    public Guid? FolderId { get; init; }

    /// <summary>When the binding was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }
}