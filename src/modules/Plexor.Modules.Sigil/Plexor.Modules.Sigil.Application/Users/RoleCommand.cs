// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Role CRUD + RoleBinding CRUD wire shapes. Phase 4 IAM surface.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Users;

/// <summary>
///     Create a custom (non-built-in) role inside an organization.
///     The Migrator seeds the built-in <c>admin</c> + <c>viewer</c>
///     roles — those can't be recreated via this command.
/// </summary>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Name">Role name (unique per org).</param>
/// <param name="Description">Optional human-readable description.</param>
/// <param name="Permissions">Permission strings granted to bound users.</param>
public sealed record CreateRoleCommand(
    Guid OrgId,
    string Name,
    string? Description,
    IReadOnlyCollection<string> Permissions);

/// <summary>Update an existing role. <c>BuiltIn = true</c> roles
/// are immutable — the handler refuses updates against them.</summary>
/// <param name="RoleId">Target role id.</param>
/// <param name="Description">New description (null = leave unchanged).</param>
/// <param name="Permissions">New permissions list (null = leave unchanged).</param>
public sealed record UpdateRoleCommand(
    Guid RoleId,
    string? Description,
    IReadOnlyCollection<string>? Permissions);

/// <summary>Delete a custom role. Built-in roles are protected —
/// the handler returns 409 Conflict via
/// <see cref="Domain.Errors.IdentityExceptions" />.</summary>
/// <param name="RoleId">Target role id.</param>
public sealed record DeleteRoleCommand(Guid RoleId);

/// <summary>Fetch one role by id.</summary>
/// <param name="RoleId">Target role id.</param>
public sealed record GetRoleQuery(Guid RoleId);

/// <summary>List roles in an org.</summary>
/// <param name="OrgId">Tenant scope.</param>
public sealed record ListRolesQuery(Guid OrgId);

/// <summary>Public projection of <see cref="Domain.Entities.Role" />.
/// Built-in flag is exposed so admin UIs can warn before changes.</summary>
/// <param name="Id">UUID v7 role id.</param>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Name">Role name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Permissions">Permission strings.</param>
/// <param name="BuiltIn">True when seeded by Migrator; immutable.</param>
/// <param name="CreatedAt">Creation time (UTC).</param>
/// <param name="UpdatedAt">Last modification time (UTC).</param>
public sealed record RoleSummary(
    Guid Id,
    Guid OrgId,
    string Name,
    string? Description,
    IReadOnlyCollection<string> Permissions,
    bool BuiltIn,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Result of CreateRoleCommand.</summary>
/// <param name="RoleId">UUID v7 of the newly created role.</param>
public sealed record CreateRoleResult(Guid RoleId);
