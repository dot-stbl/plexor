// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PermissionRequirement — single-permission authorization requirement.
// Multiple requirements on one policy combine with AND semantics
// (handler fails if ANY permission is absent on the caller's claims).
// ============================================================================

using Microsoft.AspNetCore.Authorization;

namespace Plexor.Shared.Authorization;

/// <summary>
///     Authorization requirement that a single permission string must
///     be present on the caller's claim set. Comparison against
///     <see cref="AuthorizationClaimNames.PermissionClaim" /> claims is
///     exact and case-insensitive on the value.
/// </summary>
public sealed record PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    ///     The permission string. Always trimmed; never null or
    ///     whitespace after construction (validated in the primary
    ///     ctor, throws if violated).
    /// </summary>
    public string Permission { get; }

    /// <summary>
    ///     Construct a permission requirement. Empty or whitespace
    ///     strings are rejected because they would silently authorize
    ///     every caller — almost certainly a policy-author bug.
    /// </summary>
    /// <param name="permission">
    ///     Non-null, non-whitespace permission string. Trimmed
    ///     before storage so equality comparisons are well-defined.
    /// </param>
    public PermissionRequirement(string permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        var trimmed = permission.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException(
                "Permission cannot be empty or whitespace.",
                nameof(permission));
        }

        Permission = trimmed;
    }
}
