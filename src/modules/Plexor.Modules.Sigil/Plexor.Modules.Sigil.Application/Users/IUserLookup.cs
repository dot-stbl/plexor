// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IUserLookup — read-side queries for users. Follows the project rule
// "Application service — DbContext directly": no IRepository<TUser>
// wrapper; the lookup holds DbContext via DI and runs the queries.
// ============================================================================

using Plexor.Modules.Sigil.Domain.Entities;

namespace Plexor.Modules.Sigil.Application.Users;

/// <summary>
///     Read-only user lookups. Scoped because the underlying DbContext
///     is scoped (per-request).
/// </summary>
public interface IUserLookup
{
    /// <summary>
    ///     Find a user by exact (case-insensitive) email within an
    ///     organization. Returns <c>null</c> when no match.
    /// </summary>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="email">Email address to look up. Compared case-
    ///     insensitively against the stored <see cref="User.Email" />.</param>
    /// <param name="cancellationToken">Forwarded to the DB.</param>
    public Task<User?> FindByEmailAsync(
        Guid orgId,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Find a user by username (which is the local-part of their
    ///     email address). Returns <c>null</c> when no match.
    /// </summary>
    /// <param name="orgId">Tenant scope.</param>
    /// <param name="username">Username (email local-part, no domain).</param>
    /// <param name="cancellationToken">Forwarded to the DB.</param>
    public Task<User?> FindByUsernameAsync(
        Guid orgId,
        string username,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Find a user by id. Returns <c>null</c> when no match.
    /// </summary>
    /// <param name="userId">UUID v7 user id.</param>
    /// <param name="cancellationToken">Forwarded to the DB.</param>
    public Task<User?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
