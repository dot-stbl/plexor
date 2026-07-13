// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BearerOptions — AuthenticationSchemeOptions for the custom Plexor
// Bearer scheme (JWT). Currently a marker; future tunables (audience
// pinning, max clock skew, allowed signing algorithms) will live here.
// ============================================================================

using Microsoft.AspNetCore.Authentication;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Configuration for the <see cref="BearerAuthenticationHandler" />.
/// </summary>
/// <remarks>
///     <para><b>Why we don't reuse <c>BearerOptions</c> from
///     <c>Microsoft.AspNetCore.Authentication.Bearer</c>.</b> That type is
///     tied to <c>JwtBearerHandler</c> which assumes an
///     <see cref="Microsoft.IdentityModel.Tokens.TokenValidationParameters" />
///     pipeline. We talk to <see cref="Application.Auth.IJwtSigningService" />
///     directly so the only option we actually need right now is a
///     stable schema name and a realm for the <c>WWW-Authenticate</c>
///     challenge.</para>
/// </remarks>
public sealed class BearerOptions : AuthenticationSchemeOptions
{
    /// <summary>
    ///     Stable scheme name used both in <c>AddAuthentication(...)</c> and
    ///     on the <c>Authorization: Bearer</c> request header. Hard-coded
    ///     to match the RFC 7235 scheme token.
    /// </summary>
    public const string SchemeName = "Bearer";

    /// <summary>
    ///     Realm surfaced in the <c>WWW-Authenticate: Bearer realm="…"</c>
    ///     challenge header (RFC 7235 §2.2). Defaults to <c>"plexor"</c>;
    ///     can be overridden per-host in <c>appsettings.json</c> via
    ///     <c>Authentication:Bearer:Realm</c> if multi-tenant deployments
    ///     need to distinguish.
    /// </summary>
    public string Realm { get; set; } = "plexor";
}
