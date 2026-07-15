// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RoleBinding wire shapes — attach a user to a role, optionally scoped
// to a team or folder. Phase 4 ships only the org-wide path; team /
// folder scopes land when Plexor.Modules.Realm grows a Team aggregate.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Users;

/// <summary>
///     Bind a user to a role. The (user, role, project) triple must be
///     unique — duplicates throw
///     <see cref="Domain.Errors.IdentityExceptions.InvalidPermission" />.
/// </summary>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="UserId">Target user.</param>
/// <param name="RoleId">Target role.</param>
public sealed record CreateRoleBindingCommand(
    Guid OrgId,
    Guid UserId,
    Guid RoleId);

/// <summary>Remove a role binding by id.</summary>
/// <param name="BindingId">Target role_binding row id.</param>
public sealed record DeleteRoleBindingCommand(Guid BindingId);

/// <summary>List role bindings for a user.</summary>
/// <param name="UserId">User whose bindings to enumerate.</param>
public sealed record ListRoleBindingsQuery(Guid UserId);

/// <summary>Public projection of <see cref="Domain.Entities.RoleBinding" />.</summary>
/// <summary>Public projection of <see cref="Domain.Entities.RoleBinding" />.
/// <c>sealed partial class</c> with init-only properties for
/// Mapperly source-generation compatibility.</summary>
public sealed partial class RoleBindingSummary
{
    /// <summary>UUID v7 binding id.</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Bound user.</summary>
    public Guid UserId { get; init; }

    /// <summary>Bound role.</summary>
    public Guid RoleId { get; init; }

    /// <summary>Optional team scope (null = org-wide).</summary>
    public Guid? TeamId { get; init; }

    /// <summary>Optional folder scope (null = org-wide).</summary>
    public Guid? FolderId { get; init; }

    /// <summary>Binding creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Result of CreateRoleBindingCommand.</summary>
/// <param name="BindingId">UUID v7 of the newly created binding.</param>
public sealed record CreateRoleBindingResult(Guid BindingId);
