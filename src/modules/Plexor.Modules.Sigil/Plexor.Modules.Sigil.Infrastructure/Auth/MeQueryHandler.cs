// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// MeQueryHandler — return the authenticated caller's identity, roles,
// and permissions as resolved by the bearer handler. Reads through
// ICurrentUser; never touches the DB on the hot path (all values come
// from the JWT claims).
//
// Extracted from AuthCommandHandlers.cs (issue #81 / M2) so each CQRS
// command/query handler lives in its own file per
// folder-organization.md §1.
// ============================================================================

using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Shared.Kernel.Identity;
using Plexor.Modules.Sigil.Domain.Errors;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Me — return the authenticated caller's identity, roles, and
///     permissions as resolved by the bearer handler. Reads through
///     <see cref="ICurrentUser" />; never touches the DB on the hot
///     path (all values come from the JWT claims).
/// </summary>
/// <param name="currentUser"></param>
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
