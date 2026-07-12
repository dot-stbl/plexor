using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Realm.Domain.Entities;

/// <summary>
///     One team within an organization. IAM aggregation level — a
///     team has 5-15 people and a single set of role bindings. Role
///     bindings can be team-scoped (all members see the same
///     permissions) in addition to user-scoped.
/// </summary>
/// <remarks>
///     <para><b>Resource scope.</b> Resources can be created at the
///     team level (no Folder); they're then visible to all members
///     of the team. Useful for team-shared services that don't need
///     folder-level isolation.</para>
///     <para><b>Phase 1.</b> The entity exists; team-scoped role
///     bindings and team-scoped endpoints land in Phase 2.</para>
/// </remarks>
public sealed class Team : ICreatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Organization this team belongs to.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Display name shown in UI (<c>"team-zero"</c>,
    /// <c>"platform-core"</c>, ...).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>URL-safe lowercase kebab-case identifier. Unique per
    /// organization.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Team status. <c>"active"</c> by default.</summary>
    public string Status { get; init; } = "active";

    /// <summary>Creation time (UTC). See
    /// <see cref="Plexor.Shared.Kernel.Common.ICreatedAt" />.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}