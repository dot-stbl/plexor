// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// User CRUD commands + queries. Application layer carries the wire
// shapes; handlers in Infrastructure run the EF queries + role /
// permission calls.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Users;

/// <summary>
///     Create a new user inside an organization. Admin-only operation
///     (caller must have <c>iam.users.create</c> permission).
/// </summary>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Email">Email address (validated + lowercased on write).</param>
/// <param name="DisplayName">Human-readable name shown in audit + UI.</param>
/// <param name="Password">Plain-text initial password. Hashed via
/// <see cref="Auth.IPasswordHasher" /> before storage.</param>
public sealed record CreateUserCommand(
    Guid OrgId,
    string Email,
    string DisplayName,
    string Password);

/// <summary>
///     Update an existing user's display metadata + status. Email
///     changes are forbidden in v0.1 (would require re-verification).
/// </summary>
/// <param name="UserId">Target user.</param>
/// <param name="DisplayName">New display name (null = leave unchanged).</param>
/// <param name="Status">New status (null = leave unchanged). Recognised
/// values: <c>"active"</c>, <c>"suspended"</c>.</param>
public sealed record UpdateUserCommand(
    Guid UserId,
    string? DisplayName,
    string? Status);

/// <summary>
///     Disable a user — sets <c>status = "suspended"</c> and revokes
///     every refresh token in every family. Soft-delete only; the
///     row stays in <c>sigil.users</c> for audit / FK integrity.
/// </summary>
/// <param name="UserId">Target user.</param>
public sealed record DisableUserCommand(Guid UserId);

/// <summary>
///     Fetch one user by id within the caller's org.
/// </summary>
/// <param name="UserId">Target user.</param>
public sealed record GetUserQuery(Guid UserId);

/// <summary>
///     List users in the caller's org, paged.
/// </summary>
/// <param name="OrgId">Tenant scope.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Items per page (default 50).</param>
public sealed record ListUsersQuery(
    Guid OrgId,
    int Page = 1,
    int PageSize = 50);

/// <summary>Public projection of <see cref="Domain.Entities.User" />
/// returned by GET endpoints. Never includes password hash.</summary>
/// <summary>Public projection of <see cref="Domain.Entities.User" />.
/// <c>sealed partial class</c> with init-only properties for
/// Mapperly source-generation compatibility.</summary>
public sealed partial class UserSummary
{
    /// <summary>UUID v7 user id.</summary>
    public Guid Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public Guid OrgId { get; init; }

    /// <summary>Email address.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Human-readable name shown in audit + UI.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>User status.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Account creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Last successful login (UTC), null if never.</summary>
    public DateTimeOffset? LastLoginAt { get; init; }
}

/// <summary>Paged response shape for ListUsersQuery.</summary>
/// <param name="Items">Page items.</param>
/// <param name="Total">Total matching users in the org.</param>
/// <param name="Page">Echo of the requested page.</param>
/// <param name="PageSize">Echo of the requested page size.</param>
public sealed record UserPage(
    IReadOnlyCollection<UserSummary> Items,
    int Total,
    int Page,
    int PageSize);

/// <summary>Result of a successful CreateUserCommand.</summary>
/// <param name="UserId">UUID v7 of the newly created user.</param>
public sealed record CreateUserResult(Guid UserId);
