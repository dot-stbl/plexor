// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PermissionPolicyProvider — dynamic IAuthorizationPolicyProvider that
// turns "permission:vms.read,vms.write" policy names into an
// AuthorizationPolicy with one PermissionRequirement per permission.
// Non-permission policy names fall through (return null) so other
// policy sources can still resolve.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Plexor.Shared.Authorization;

/// <summary>
///     Resolves permission policy names (those starting with
///     <see cref="AuthorizationPolicyNames.Prefix" />) on demand. Each
///     non-empty token in the CSV becomes its own
///     <see cref="PermissionRequirement" />; combined with the handler
///     this gives AND semantics across all permissions on a single
///     <c>[RequirePermission]</c> attribute.
/// </summary>
/// <param name="logger"></param>
/// <remarks>
///     <para><b>Fallback behaviour.</b> The framework's default policy
///     resolution still runs after this provider returns null, so
///     <c>[Authorize(Policy = "...")]</c> with non-permission names
///     keeps working as long as their target policies are registered
///     via <c>services.AddAuthorization(o => o.AddPolicy(...))</c>.</para>
///     <para><b>Whitespace handling.</b> Each token is trimmed before
///     being handed to <see cref="PermissionRequirement" />. Tokens
///     that become empty after trimming are silently dropped — they
///     almost always indicate an authoring typo, but throwing on a
///     stray double-comma is more disruptive than helpful at request
///     time (the attribute ctor is the right place to enforce
///     cleanliness).</para>
/// </remarks>
public sealed class PermissionPolicyProvider(
    ILogger<PermissionPolicyProvider> logger)
    : IAuthorizationPolicyProvider
{
    /// <summary>
    ///     Default policy when neither an endpoint metadata nor an
    ///     explicit policy name requests anything — requires an
    ///     authenticated caller. Mirrors the framework default so
    ///     adding this provider doesn't silently relax
    ///     <c>[AllowAnonymous]</c>-style behaviour.
    /// </summary>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return Task.FromResult(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());
    }

    /// <summary>
    ///     Fallback policy when authorization is invoked but no
    ///     <c>[Authorize]</c> attribute applies. Same semantics as
    ///     <see cref="GetDefaultPolicyAsync" /> for this project —
    ///     anonymous endpoints pass; everything else requires
    ///     authentication.
    /// </summary>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());
    }

    /// <summary>
    ///     Returns a one-policy-per-permission
    ///     <see cref="AuthorizationPolicy" /> for permission policy
    ///     names, or <c>null</c> for any other name so the framework
    ///     can try the next provider in its chain.
    /// </summary>
    /// <param name="policyName">
    ///     The policy name requested by an <see cref="IAuthorizeData" />
    ///     or a direct <c>IAuthorizationService.AuthorizeAsync</c> call.
    /// </param>
    /// <returns>
    ///     A task whose result is a built <see cref="AuthorizationPolicy" />
    ///     for permission-prefixed names, or <c>null</c> for everything
    ///     else.
    /// </returns>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {

        if (!policyName.StartsWith(AuthorizationPolicyNames.Prefix, StringComparison.Ordinal))
        {
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        var permissionsCsv = policyName[AuthorizationPolicyNames.Prefix.Length..];
        var tokens = permissionsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
        {
            logger.LogDebug(
                "Permission policy {PolicyName} had no permissions after parsing.",
                policyName);
            return Task.FromResult<AuthorizationPolicy?>(null);
        }

        var builder = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser();

        foreach (var token in tokens)
        {
            builder.AddRequirements(new PermissionRequirement(token));
        }

        return Task.FromResult<AuthorizationPolicy?>(builder.Build());
    }
}
