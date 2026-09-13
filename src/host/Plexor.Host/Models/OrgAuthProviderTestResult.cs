// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgAuthProviderTestResult — wire shape for
// POST /api/v1/iam/orgs/{orgId}/auth-provider/test (4.6.1).
//
// The test endpoint decrypts the stored OIDC client secret locally
// and runs an OIDC discovery-document fetch against the configured
// authority. The response carries the discovery document URL, the
// available scopes the authority advertised, and a connection
// error if the fetch failed. Plaintext secrets are NEVER included
// in the response.
// ============================================================================

namespace Plexor.Host.Models;

/// <summary>
///     Response body for the OIDC connection-test endpoint.
///     Indicates whether the configured authority is reachable
///     and what scopes it advertises.
/// </summary>
public sealed class OrgAuthProviderTestResult
{
    /// <summary><c>true</c> when the discovery document was
    /// fetched + parsed successfully.</summary>
    public bool Connected { get; init; }

    /// <summary>The URL the controller actually fetched.
    /// <c>null</c> on failure.</summary>
    public string? DiscoveryDocumentUrl { get; init; }

    /// <summary>Scopes the authority advertised. Empty on
    /// failure.</summary>
    public IReadOnlyList<string> AvailableScopes { get; init; } = [];

    /// <summary>Human-readable failure reason. <c>null</c> on
    /// success.</summary>
    public string? Error { get; init; }
}
