// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PermissionAuthorizationHandler — short-circuits every pending
// PermissionRequirement by checking the caller's claims. AND
// semantics: all PermissionRequirements in the current policy must
// resolve to Succeed, otherwise no Succeed is called on the missing
// ones and authorization fails.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Plexor.Shared.Authorization;

/// <summary>
///     Authorization handler that evaluates every
/// <see cref="PermissionRequirement" /> in the current policy
/// against the caller's <c>permission</c> claims. All requirements
/// must succeed (AND); partial coverage causes auth to fail with
/// no <c>Succeed</c> call on the missing ones — the framework then
/// emits a 403.
/// </summary>
/// <param name="logger"></param>
/// <remarks>
///     <para><b>Single handler for all permission requirements.</b>
/// Registered once as scoped (one instance per request) so it can
/// pick up <see cref="ILogger{TCategoryName}" /> via DI. The handler
/// iterates <c>context.PendingRequirements</c> filtering for
/// <see cref="PermissionRequirement" /> and only attempts to satisfy
/// those — other requirements in mixed policies pass through
/// untouched.</para>
///     <para><b>Wildcard <c>*</c>.</b> A claim value of <c>"*"</c> is
/// treated as "all permissions" — the built-in admin role gets one
/// such claim and the handler short-circuits any required permission
/// against it. This keeps the on-the-wire token small while still
/// letting endpoints use arbitrary future permissions.</para>
/// </remarks>
public sealed class PermissionAuthorizationHandler(
    ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {

        var hasClaim = context.User.Claims.Any(claim =>
            string.Equals(
                claim.Type,
                AuthorizationClaimNames.PermissionClaim,
                StringComparison.Ordinal)
            && PermissionMatches(claim.Value, requirement.Permission));

        if (hasClaim)
        {
            context.Succeed(requirement);
        }
        else
        {
            // Log only on failure — success is the common path and
            // spammy auth-trace logs make brute-force detection
            // (per the X-Forwarded-For / IP throttling work, Phase 5+)
            // harder.
            logger.LogDebug(
                "Permission {Permission} missing for caller; required-claim {Claim} not present on principal.",
                requirement.Permission,
                AuthorizationClaimNames.PermissionClaim);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Match a required permission against an issued permission
    /// claim. Returns <c>true</c> on exact match (case-insensitive)
    /// OR when the issued claim is the wildcard <c>"*"</c> (built-in
    /// admin role — every permission matches).
    /// </summary>
    /// <param name="issuedPermission"></param>
    /// <param name="requiredPermission"></param>
    private static bool PermissionMatches(string issuedPermission, string requiredPermission)
    {
        if (string.Equals(issuedPermission, requiredPermission, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(issuedPermission, "*", StringComparison.Ordinal);
    }
}
