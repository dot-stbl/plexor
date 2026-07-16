// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthorizationPolicyNames — single source of truth for permission
// policy naming. The [RequirePermission] attribute emits policies
// here; PermissionPolicyProvider parses them.
// ============================================================================

namespace Plexor.Shared.Authorization;

/// <summary>
///     String constants for permission policy names. Routing happens
///     on <see cref="Prefix" /> — anything that doesn't start with it
///     falls through to the default ASP.NET Core authorization policy
///     resolution.
/// </summary>
public static class AuthorizationPolicyNames
{
    /// <summary>
    ///     Prefix every permission policy. Format:
    ///     <c>permission:&lt;csv-of-required-permissions&gt;</c>.
    /// </summary>
    public const string Prefix = "permission:";

    /// <summary>
    ///     Encodes a list of required permissions into a policy name
    ///     suitable for <see cref="RequirePermissionAttribute.Policy" />.
    /// </summary>
    /// <param name="permissions">
    ///     One or more permission strings; empty entries are dropped,
    ///     whitespace around each token is trimmed.
    /// </param>
    /// <returns>
    ///     <c>permission:</c> followed by the trimmed-and-joined token
    ///     sequence, e.g. <c>permission:vms.read,vms.write</c>.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when no non-empty tokens remain after filtering.
    /// </exception>
    public static string For(params string[] permissions)
    {

        var tokens = permissions
            .Where(static p => p is not null)
            .Select(static p => p.Trim())
            .Where(static p => p.Length > 0)
            .ToArray();

        if (tokens.Length == 0)
        {
            throw new ArgumentException(
                "At least one non-empty permission is required.",
                nameof(permissions));
        }

        return Prefix + string.Join(',', tokens);
    }
}
