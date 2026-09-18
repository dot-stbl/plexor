// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgAuthProviderConfigResponse — wire shape for
// GET /api/v1/iam/orgs/{orgId}/auth-provider (4.6.1).
//
// The OIDC client secret is NEVER returned in the GET response.
// <see cref="OidcClientSecretMasked" /> carries the literal string
// <c>"***"</c> when the persisted row has a non-null
// <c>oidc_client_secret_protected</c> column; <c>null</c> when the
// provider is Sigil or the secret has not been set yet.
// ============================================================================

namespace Plexor.Host.Models;

/// <summary>
///     Response body for the per-org authentication provider
///     configuration. The OIDC client secret is redacted to
///     <c>"***"</c>; the plaintext value is never returned through
///     the REST surface (the <c>/test</c> endpoint decrypts it
///     locally for an outbound OIDC call but does not include it
///     in the response body).
/// </summary>
public sealed class OrgAuthProviderConfigResponse
{
    /// <summary>Tenant scope (echoed from the URL).</summary>
    public Guid OrgId { get; init; }

    /// <summary>Lowercase wire form of the provider discriminator
    /// (<c>"sigil"</c> or <c>"oidc"</c>).</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>OIDC issuer URL. <c>null</c> when the provider is
    /// Sigil.</summary>
    public string? OidcAuthority { get; init; }

    /// <summary>OIDC confidential client id. <c>null</c> when the
    /// provider is Sigil.</summary>
    public string? OidcClientId { get; init; }

    /// <summary>Always <c>"***"</c> when an OIDC secret is set;
    /// <c>null</c> otherwise. The plaintext secret is never
    /// returned through the REST surface.</summary>
    public string? OidcClientSecretMasked { get; init; }

    /// <summary>Effective OIDC scopes.</summary>
    public IReadOnlyList<string> OidcScopes { get; init; } = [];

    /// <summary>UTC.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>UTC.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
