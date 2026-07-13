// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorPermissions — single source of truth for permission strings.
// Hard-coded so the change-own-password permission works at login
// time, BEFORE the user has any role bindings resolved (so we
// can't look it up in the role_bindings table).
// ============================================================================

namespace Plexor.Modules.Sigil.Application.Authorization;

/// <summary>
///     Hard-coded permission strings the Sigil (Identity) module
///     recognises. Most permissions are stored as <see cref="Domain.Entities.Role.Permissions" />
///     rows in <c>sigil.roles</c>; this class only carries the strings
///     that must work even when no role-binding has been resolved yet
///     (i.e. during the first-login password rotation).
/// </summary>
public static class PlexorPermissions
{
    /// <summary>
    ///     <c>iam.users.change-own-password</c> — the only permission
    ///     baked into the short-lived access token issued to a user
    ///     whose <see cref="Domain.Entities.User.MustChangePassword" />
    ///     flag is true. The user can hit
    ///     <c>POST /iam/users/{userId}/password</c> (and that's it)
    ///     until the flag is cleared by a successful password change.
    /// </summary>
    public const string UsersChangeOwnPassword = "iam.users.change-own-password";
}
