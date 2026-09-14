// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// MeQueryHandler — return the authenticated caller's identity, roles,
// and permissions as resolved by the bearer handler. Reads through
// <see cref="ICurrentUser" />; never touches the DB on the hot path
// (all values come from the JWT claims). Extracted from
// AuthCommandHandlers.cs (Sprint 3, item 2).
// ============================================================================

using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Errors;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Me — return the authenticated caller's identity, roles, and
///     permissions as resolved by the bearer handler. Reads through
///     <see cref="ICurrentUser" />; never touches the DB on the hot
///     path (all values come from the JWT claims).
/// </summary>
/// <param name="currentUser">Caller identity, populated by the bearer handler.</param>
public sealed class MeQueryHandler(
    ICurrentUser currentUser) : ICommandHandler<MeQuery, MeResult>
{
    /// <inheritdoc />
    public Task<MeResult> HandleAsync(
        MeQuery command,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId == Guid.Empty)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Caller is not authenticated.");
        }

        return Task.FromResult(new MeResult(
            UserId: currentUser.UserId,
            OrgId: currentUser.TenantId,
            Roles: currentUser.Roles,
            Permissions: currentUser.Permissions));
    }
}
