// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RequirePermissionAttribute — gate controllers / actions on the
// caller having one or more permission claims. Bridges into the ASP.NET
// Core authorization pipeline via IAuthorizeData; the dynamic
// PermissionPolicyProvider resolves the encoded policy name at
// request time and supplies N PermissionRequirement instances, one
// per listed permission (AND).
// ============================================================================

using Microsoft.AspNetCore.Authorization;

namespace Plexor.Shared.Authorization;

/// <summary>
///     Restrict an endpoint to callers whose claims include every
///     listed permission. Multiple <c>[RequirePermission]</c>
///     attributes (or a single attribute carrying multiple names)
///     combine with AND semantics: missing any one of the listed
///     permissions produces 403.
/// </summary>
/// <remarks>
///     <para><b>Why this lives in shared/ rather than the Identity
///     module.</b> Authorization is a cross-cutting concern — every
///     module (audit / billing / compute / etc.) will want to gate
///     its endpoints on permissions. Keeping it in
///     <c>Plexor.Shared.Authorization</c> lets controllers in any
///     module depend on the attribute without taking on a reference
///     to the Identity implementation.</para>
///     <para><b>Why <c>params string[]</c>.</b> The natural pattern for
///     a developer reading the controller is
///     <c>[RequirePermission("vms.read", "vms.write")]</c> — single
///     attribute, comma-separated in the source, AND semantics. An
///     alternative API (<c>[RequirePermission("vms.read")]</c> +
///     <c>[RequirePermission("vms.write")]</c>) was rejected because
///     it scatters related permissions across multiple lines and
///     makes the permission set harder to audit at a glance.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute, IAuthorizeData
{
    /// <summary>
    ///     The permission strings the caller must possess. Stored
    ///     trimmed and non-empty after validation; reflection-friendly
    ///     for tests and for documentation generators.
    /// </summary>
    public IReadOnlyList<string> Permissions { get; }

    /// <inheritdoc />
    public string? Policy { get; set; }

    /// <inheritdoc />
    public string? Roles { get; set; }

    /// <inheritdoc />
    public string? AuthenticationSchemes { get; set; }

    /// <summary>
    ///     Construct the attribute with one or more required
    ///     permissions. All whitespace-trimmed, non-empty tokens are
    ///     encoded into a single permission policy name via
    ///     <see cref="AuthorizationPolicyNames.For(string[])" />.
    /// </summary>
    /// <param name="permissions">
    ///     Permission strings. Empty or whitespace-only entries are
    ///     filtered out; at least one non-empty token must remain
    ///     (otherwise the constructor throws).
    /// </param>
    public RequirePermissionAttribute(params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var tokens = permissions
            .Where(static p => p is not null)
            .Select(static p => p.Trim())
            .Where(static p => p.Length > 0)
            .ToArray();

        if (tokens.Length == 0)
        {
            throw new ArgumentException(
                "RequirePermission needs at least one non-empty permission.",
                nameof(permissions));
        }

        Permissions = tokens;
        Policy = AuthorizationPolicyNames.For(tokens);
    }
}
