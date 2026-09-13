// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertOrgAuthProviderRequest — wire shape for
// PUT /api/v1/iam/orgs/{orgId}/auth-provider (4.6.1).
//
// The body carries the provider discriminator plus the OIDC fields.
// When <see cref="Provider" /> = <c>"sigil"</c> the OIDC fields are
// ignored at the controller layer (the row is reset to a Sigil
// default). When <see cref="Provider" /> = <c>"oidc"</c> the OIDC
// fields are required (validated by FluentValidation).
// ============================================================================

namespace Plexor.Host.Models;

/// <summary>
///     Request body for the per-org authentication provider
///     upsert endpoint.
/// </summary>
public sealed class UpsertOrgAuthProviderRequest
{
    /// <summary>Lowercase wire form (<c>"sigil"</c> or <c>"oidc"</c>).</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>OIDC issuer URL. Required when
    /// <see cref="Provider" /> = <c>"oidc"</c>.</summary>
    public string? OidcAuthority { get; init; }

    /// <summary>OIDC confidential client id. Required when
    /// <see cref="Provider" /> = <c>"oidc"</c>.</summary>
    public string? OidcClientId { get; init; }

    /// <summary>Plaintext OIDC confidential client secret.
    /// Encrypted before persist. Null on a no-op PUT that doesn't
    /// rotate the secret.</summary>
    public string? OidcClientSecret { get; init; }

    /// <summary>OIDC scopes; <c>null</c> = leave the stored list
    /// alone.</summary>
    public IReadOnlyList<string>? OidcScopes { get; init; }
}
