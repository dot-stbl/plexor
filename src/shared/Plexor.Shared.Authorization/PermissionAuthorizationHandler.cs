// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PermissionAuthorizationHandler — short-circuits every pending
// PermissionRequirement by checking the caller's claims. AND
// semantics: all PermissionRequirements in the current policy must
// resolve to Succeed, otherwise no Succeed is called for the missing
// ones and authorization fails.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace Plexor.Shared.Authorization;

/// <summary>
///     Authorization handler that evaluates every
///     <see cref="PermissionRequirement" /> in the current policy
///     against the caller's <c>permission</c> claims. All requirements
///     must succeed (AND); partial coverage causes auth to fail with
///     no <c>Succeed</c> call on the missing ones — the framework then
///     emits a 403.
/// </summary>
/// <remarks>
///     <para><b>Single handler for all permission requirements.</b>
///     Registered once as scoped (one instance per request) so it can
///     pick up <see cref="ILogger{TCategoryName}" /> via DI. The handler
///     iterates <c>context.PendingRequirements</c> filtering for
///     <see cref="PermissionRequirement" /> and only attempts to satisfy
///     those — other requirements in mixed policies pass through
///     untouched.</para>
///     <para><b>Why case-insensitive comparison.</b> HTTP claim values
///     are case-sensitive strings but permission conventions are
///     lowercase. A case-sensitive match would force callers to write
///     <c>VMS.Read</c> in policy strings but <c>vms.read</c> on the token,
///     producing silent authorization failures. Using
///     <see cref="StringComparison.OrdinalIgnoreCase" /> matches
///     resource-name conventions typical of cloud IAM (AWS / GCP /
///     YC permissions are case-insensitive).</para>
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
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        var hasClaim = context.User.Claims.Any(claim =>
            string.Equals(
                claim.Type,
                AuthorizationClaimNames.PermissionClaim,
                StringComparison.Ordinal)
            && string.Equals(
                claim.Value,
                requirement.Permission,
                StringComparison.OrdinalIgnoreCase));

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
}
