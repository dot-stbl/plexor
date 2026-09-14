// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuthProviderId — stable identifier for an authentication provider that
// issued the current principal. Maps to OrgAuthProvider in the realm
// module (Plexor.Modules.Realm.Domain.Entities.OrgAuthProvider) for
// routing purposes. Two implementations in v0.1:
//
//   - AuthProviderId.Sigil — local email+password against sigil.users
//   - AuthProviderId.Oidc  — external IDP via OIDC (Phase 4.6.2b)
//
// Stringly-typed discriminator — same shape as IdentityClaims /
// IdentityExceptions / PlexorPermissions: the wire is a string, the type
// is a typed wrapper. Stable across renames; clients branch on the
// string, not the C# identity.
// ============================================================================

namespace Plexor.Modules.Sigil.Application.AuthProviders;

/// <summary>
///     Stable wire string identifying which authentication provider
///     issued the current bearer token. Maps to
///     <c>Plexor.Modules.Realm.Domain.Entities.OrgAuthProvider</c>
///     — the Sigil provider and the OIDC provider share the same wire
///     values; the realm layer decides which provider serves which
///     tenant.
/// </summary>
/// <param name="Value">Lowercase wire string — <c>"sigil"</c> or
///     <c>"oidc"</c> in v0.1.</param>
public sealed record AuthProviderId(string Value)
{
    /// <summary>Local email + password backend. Default for fresh
    /// tenants (<c>OrgAuthProviderSeeder</c>); every existing Plexor
    /// org starts here.</summary>
    public static readonly AuthProviderId Sigil = new("sigil");

    /// <summary>External OIDC backend. Lands in 4.6.2b
    /// (<c>ExternalOidcAuthProvider</c>); the constant is defined here
    /// so the dispatcher (4.6.2c) and the bearer handler can route on
    /// it from day one.</summary>
    public static readonly AuthProviderId Oidc = new("oidc");

    /// <inheritdoc />
    public override string ToString()
    {
        return Value;
    }
}
