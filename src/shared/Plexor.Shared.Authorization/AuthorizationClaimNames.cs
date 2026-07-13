// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthorizationClaimNames — claim type names used by the authorization
// pipeline. Standalone constants so this module has no dependency on
// Plexor.Modules.Sigil (which owns its own IdentityClaims for token-
// shaping purposes).
// ============================================================================

namespace Plexor.Shared.Authorization;

/// <summary>
///     Wire-format claim type names read by the authorization handlers
///     when evaluating <see cref="PermissionRequirement" />s.
/// </summary>
public static class AuthorizationClaimNames
{
    /// <summary>
    ///     Permission claim type. The handler looks up
    ///     <c>Claim { Type = <see cref="PermissionClaim" />, Value = "..." }</c>
    ///     pairs on the caller's <see cref="System.Security.Claims.ClaimsPrincipal" />.
    /// </summary>
    /// <remarks>
    ///     <b>MUST stay in sync with</b>
    ///     <c>Plexor.Modules.Sigil.Application.Abstractions.IdentityClaims.Permission</c>.
    ///     The value is identical (<c>"permission"</c>) but lives in two
    ///     assemblies on purpose: this assembly has no business depending
    ///     on a module project, and the module's JWT signer can iterate
    ///     freely without impedance-mismatching its own constant layout.
    ///     A pre-commit check or unit test should catch a drift
    ///     (every signer roundtrip would otherwise silently drop
    ///     authorization claims).
    /// </remarks>
    public const string PermissionClaim = "permission";
}
