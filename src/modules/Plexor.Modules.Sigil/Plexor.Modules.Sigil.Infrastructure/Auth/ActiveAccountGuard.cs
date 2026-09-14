// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ActiveAccountGuard — verifies the user is in a state that can
// authenticate via password: status == Active and a PasswordHash
// exists (OAuth-only accounts with no local password get the generic
// "InvalidCredentials" surface so we don't leak the auth mode).
// ============================================================================

using Plexor.Modules.Sigil.Domain;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Domain.Errors;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Stateless guards applied to a user record before any password
///     check runs. Pulled out of LoginCommandHandler so the handler
///     doesn't carry the "is this account usable" policy in private
///     methods.
/// </summary>
public static class ActiveAccountGuard
{
    /// <summary>
    ///     Throws <see cref="IdentityException" />
    ///     (<see cref="IdentityExceptions.AccountSuspended" />) when
    ///     the user's status is anything other than
    ///     <see cref="UserStatusValues.Active" />.
    /// </summary>
    /// <param name="user">The resolved user row.</param>
    public static void EnsureActive(User user)
    {
        if (!string.Equals(user.Status, UserStatusValues.Active, StringComparison.Ordinal))
        {
            throw new IdentityException(
                IdentityExceptions.AccountSuspended,
                "Account is not active.");
        }
    }

    /// <summary>
    ///     Throws <see cref="IdentityException" />
    ///     (<see cref="IdentityExceptions.InvalidCredentials" />)
    ///     when the user has no password hash. OAuth-only accounts
    ///     get the generic invalid-credentials surface (no
    ///     auth-mode leakage).
    /// </summary>
    /// <param name="user">The resolved user row.</param>
    public static void EnsurePasswordExists(User user)
    {
        if (user.PasswordHash is null)
        {
            throw new IdentityException(
                IdentityExceptions.InvalidCredentials,
                "Password login not available for this account.");
        }
    }
}