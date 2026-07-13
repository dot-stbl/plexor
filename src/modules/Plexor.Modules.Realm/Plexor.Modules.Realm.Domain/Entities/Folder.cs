using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Realm.Domain.Entities;

/// <summary>
///     One folder within a team (or, when <see cref="TeamId" /> is
///     <c>null</c>, within an organization as an org-level folder).
///     The default resource scope — VMs, app instances, and most
///     other resources live in a folder.
/// </summary>
/// <remarks>
///     <para><b>3-tier nullable scope.</b> A folder can be:
///     <list type="bullet">
///       <item>Org-level (TeamId + null) — shared across all teams in
///         the organization.</item>
///       <item>Team-level (TeamId set) — shared across members of that
///         team, no folder further down.</item>
///       <item>Folder-level (TeamId + FolderId) — private to the
///         folder's members.</item>
///     </list></para>
///     <para><b>Common use.</b> Per-team folders for separation of
///     concerns: <c>dev</c>, <c>staging</c>, <c>prod</c>,
///     <c>project-alpha</c>, <c>project-beta</c>. Resources default
///     to the most specific scope (folder > team > org).</para>
/// </remarks>
public sealed class Folder : ICreatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Organization this folder belongs to (denormalized for
    /// org-scoped queries — every folder has an org, even org-level
    /// ones with no team).</summary>
    public Guid OrgId { get; init; }

    /// <summary>Team this folder belongs to. <c>null</c> = folder is
    /// org-level (shared across the org's teams).</summary>
    public Guid? TeamId { get; init; }

    /// <summary>Display name shown in UI (<c>"project-alpha"</c>,
    /// <c>"prod"</c>, <c>"dev"</c>, ...).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>URL-safe lowercase kebab-case identifier. Unique per
    /// (org, team) — two folders in the same team cannot share a slug.
    /// Org-level folders (TeamId = null) must have a slug unique
    /// within the org.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Folder status. <c>"active"</c> by default.</summary>
    public string Status { get; init; } = "active";

    /// <summary>Creation time (UTC). See
    /// <see cref="Plexor.Shared.Kernel.Common.ICreatedAt" />.</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
