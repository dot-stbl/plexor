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
/// <param name="Id">UUID v7 binding id.</param>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="UserId">Bound user.</param>
/// <param name="RoleId">Bound role.</param>
/// <param name="TeamId">Optional team scope (null = org-wide).</param>
/// <param name="FolderId">Optional folder scope (null = org-wide).</param>
/// <param name="CreatedAt">Binding creation time (UTC).</param>
public sealed record RoleBindingSummary(
    Guid Id,
    Guid OrgId,
    Guid UserId,
    Guid RoleId,
    Guid? TeamId,
    Guid? FolderId,
    DateTimeOffset CreatedAt);

/// <summary>Result of CreateRoleBindingCommand.</summary>
/// <param name="BindingId">UUID v7 of the newly created binding.</param>
public sealed record CreateRoleBindingResult(Guid BindingId);
