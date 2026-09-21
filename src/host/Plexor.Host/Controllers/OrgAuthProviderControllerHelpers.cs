// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// OrgAuthProviderControllerHelpers — file-static helpers shared by
// OrgAuthProviderController (GET / PUT) and OrgAuthProviderTestController
// (POST .../test). Pulled out to satisfy the no-private-methods
// convention (class-layout-and-tooling.md §1a / §9.4 — Controller /
// minimal API endpoint). The controllers are thin orchestration
// layers; entity → response projection, ProblemDetails construction,
// the OIDC discovery-document fetch + parse, and the Phase 5.2
// audit-emit payload composition live here.
// ============================================================================

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Plexor.Host.Models;
using Plexor.Modules.Realm.Domain.Entities;
using Plexor.Shared.Kernel.Audit;

namespace Plexor.Host.Controllers;

/// <summary>
///     Helpers for <see cref="OrgAuthProviderController" /> and
///     <see cref="OrgAuthProviderTestController" />. Each
///     public method on the controllers is a one-line orchestration
///     call into this file; the per-method logic lives here.
/// </summary>
internal static class OrgAuthProviderControllerHelpers
{
    /// <summary>
    ///     400 <see cref="ValidationProblemDetails" /> for the PUT
    ///     endpoint when the FluentValidation chain rejects the
    ///     body. The dictionary comes from
    ///     <c>ValidationResult.ToDictionary()</c> (property-name →
    ///     error messages); ASP.NET Core binds it into the standard
    ///     <c>errors</c> shape per RFC 9457.
    /// </summary>
    /// <param name="errors">Dictionary keyed by property name,
    /// value = the error message(s) from the validator (one per
    /// failure on that property).</param>
    /// <returns>A typed <see cref="BadRequestObjectResult" />
    /// wrapping the <see cref="ValidationProblemDetails" />.</returns>
    public static BadRequestObjectResult InvalidRequestResponse(
        IDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more request fields failed validation.",
        };
        return new BadRequestObjectResult(problem);
    }

    /// <summary>
    ///     404 ProblemDetails for GET / PUT / POST .../test on an
    ///     org that doesn't have a config row yet. The seeder
    ///     guarantees a row exists for every org; the only way a
    ///     request can miss is a torn write or a manual DB delete —
    ///     either way, 404 is the right shape.
    /// </summary>
    /// <param name="orgId">Org id from the URL.</param>
    /// <param name="path">Request path (for the RFC 9457
    /// <c>instance</c> field).</param>
    /// <returns>A typed <see cref="NotFoundObjectResult" />.</returns>
    public static NotFoundObjectResult ConfigNotFound(Guid orgId, string path)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Auth-provider config not found",
            Detail = $"No auth-provider config for org '{orgId}'.",
            Instance = path,
        };
        return new NotFoundObjectResult(problem);
    }

    /// <summary>
    ///     Map a persisted <see cref="OrgAuthProviderConfig" />
    ///     row to the public response DTO. The OIDC client
    ///     secret column is masked to the literal string
    ///     <c>"***"</c> when set; <c>null</c> when the provider is
    ///     Sigil or the secret has not been configured yet.
    /// </summary>
    /// <param name="row">EF-tracked config row.</param>
    /// <returns>The wire DTO with the secret masked.</returns>
    public static OrgAuthProviderConfigResponse MapToResponse(
        OrgAuthProviderConfig row)
    {
        var hasSecret = !string.IsNullOrEmpty(row.OidcClientSecretProtected);

        return new OrgAuthProviderConfigResponse
        {
            OrgId = row.OrgId,
            Provider = row.Provider switch
            {
                OrgAuthProvider.Sigil => "sigil",
                OrgAuthProvider.Oidc => "oidc",
                _ => row.Provider.ToString().ToLowerInvariant(),
            },
            OidcAuthority = row.Provider == OrgAuthProvider.Oidc ? row.OidcAuthority : null,
            OidcClientId = row.Provider == OrgAuthProvider.Oidc ? row.OidcClientId : null,
            OidcClientSecretMasked = row.Provider == OrgAuthProvider.Oidc && hasSecret
                ? "***"
                : null,
            OidcScopes = row.OidcScopes,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };
    }

    /// <summary>
    ///     Build the OIDC discovery-document URL for the given
    ///     authority. Trims trailing slashes so
    ///     <c>https://kc.example.com/realms/plexor/</c> and
    ///     <c>https://kc.example.com/realms/plexor</c> produce
    ///     the same fetch target.
    /// </summary>
    /// <param name="authority">The OIDC issuer URL (must be
    /// absolute + HTTPS, validated at the boundary).</param>
    /// <returns>The discovery document URL.</returns>
    public static string BuildDiscoveryDocumentUrl(string authority)
    {
        var trimmed = authority.TrimEnd('/');
        return $"{trimmed}/.well-known/openid-configuration";
    }

    /// <summary>
    ///     Fetch the OIDC discovery document from the configured
    ///     authority. Returns a structured success / failure
    ///     shape so the controller can wrap it in an HTTP
    ///     response without leaking transport-level details.
    ///     Expected failure modes (timeout, transport error, malformed
    ///     JSON body) all narrow-catch into a <see cref="DiscoveryFetchResult.Failed" />;
    ///     anything outside those (programming bugs, reflection
    ///     errors, mis-configured DI) rethrows so the host surfaces
    ///     a 500 via the ProblemDetails handler instead of a misleading
    ///     "Authority discovery failed" string.
    /// </summary>
    /// <param name="discoveryUrl">The discovery document URL
    /// (<see cref="BuildDiscoveryDocumentUrl" />).</param>
    /// <param name="httpClientFactory">ASP.NET Core HTTP client
    /// factory; the controller injects
    /// <c>IHttpClientFactory</c>.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The fetch outcome.</returns>
    public static async Task<DiscoveryFetchResult> FetchDiscoveryAsync(
        string discoveryUrl,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Plexor-OidcDiscovery");
            var response = await client.GetAsync(discoveryUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return DiscoveryFetchResult.Failed(
                    $"Authority returned HTTP {(int)response.StatusCode}.");
            }

            var document = await response.Content
                .ReadFromJsonAsync<OidcDiscoveryDocument>(cancellationToken: cancellationToken);
            if (document is null)
            {
                return DiscoveryFetchResult.Failed(
                    "Authority returned an empty discovery document.");
            }

            return DiscoveryFetchResult.Succeeded(document.ScopesSupported ?? []);
        }
        catch (TaskCanceledException)
        {
            return DiscoveryFetchResult.Failed("Authority did not respond within the timeout.");
        }
        catch (HttpRequestException exception)
        {
            return DiscoveryFetchResult.Failed(
                $"Authority is unreachable: {exception.Message}");
        }
        catch (JsonException exception)
        {
            // ReadFromJsonAsync<T> throws JsonException on a malformed
            // discovery-document body. That's an authority-side config
            // problem the operator needs to know about, not a bug in
            // Plexor — surface it as a structured failure.
            return DiscoveryFetchResult.Failed(
                $"Authority returned a malformed discovery document: {exception.Message}");
        }
    }

    /// <summary>
    ///     Result of an OIDC discovery-document fetch — either a
    ///     parsed scope list (success) or a human-readable error
    ///     string (failure). Carries enough context for the
    ///     controller to wrap it into an
    ///     <see cref="OrgAuthProviderTestResult" /> without a
    ///     second round-trip.
    /// </summary>
    /// <param name="Scopes">Authority-advertised scopes (success).</param>
    /// <param name="Error">Human-readable failure (failure).</param>
    public sealed record DiscoveryFetchResult(
        IReadOnlyList<string>? Scopes,
        string? Error)
    {
        /// <summary>True when the fetch succeeded AND a
        /// discovery document was parsed.</summary>
        public bool Connected => Scopes is not null && Error is null;

        /// <summary>Construct a successful result.</summary>
        /// <param name="scopes">Authority-advertised scopes.</param>
        public static DiscoveryFetchResult Succeeded(IReadOnlyList<string> scopes)
        {
            return new DiscoveryFetchResult(scopes, null);
        }

        /// <summary>Construct a failed result.</summary>
        /// <param name="error">Human-readable failure.</param>
        public static DiscoveryFetchResult Failed(string error)
        {
            return new DiscoveryFetchResult(null, error);
        }
    }

    /// <summary>
    ///     Minimal projection of the OIDC discovery document
    ///     (RFC 8414 / OpenID Connect Discovery 1.0 §4). Plexor
    ///     only needs <c>scopes_supported</c> today; the response
    ///     carries other fields that future phases (token issuer
    ///     dispatcher, JWKS caching) will read.
    /// </summary>
    internal sealed class OidcDiscoveryDocument
    {
        /// <summary>Issuer URL advertised by the authority.</summary>
        [JsonPropertyName("issuer")]
        public string? Issuer { get; init; }

        /// <summary>Authorization endpoint URL.</summary>
        [JsonPropertyName("authorization_endpoint")]
        public string? AuthorizationEndpoint { get; init; }

        /// <summary>Token endpoint URL.</summary>
        [JsonPropertyName("token_endpoint")]
        public string? TokenEndpoint { get; init; }

        /// <summary>JWKS endpoint URL.</summary>
        [JsonPropertyName("jwks_uri")]
        public string? JwksUri { get; init; }

        /// <summary>Scopes the authority advertises.</summary>
        [JsonPropertyName("scopes_supported")]
        public IReadOnlyList<string>? ScopesSupported { get; init; }
    }

    /// <summary>
    ///     Compose the <c>org.auth_provider.changed</c> audit
    ///     payload from a before/after snapshot of the
    ///     <see cref="OrgAuthProviderConfig" /> row and emit it
    ///     through <paramref name="auditEmitter" />.
    ///     <see cref="AuditActions.OrgAuthProviderChanged" />'s
    ///     documented payload keys (<c>old_provider</c>,
    ///     <c>new_provider</c>, <c>old_oidc_authority</c>,
    ///     <c>new_oidc_authority</c>, <c>old_oidc_client_id</c>,
    ///     <c>new_oidc_client_id</c>) are the union of fields an
    ///     admin would want to see when looking back at a
    ///     provider switch.
    /// </summary>
    /// <param name="auditEmitter">
    /// Scoped <see cref="IAuditEmitter" /> resolved from the
    /// request scope. Fire-and-forget — emits never throw.
    /// </param>
    /// <param name="orgId">Tenant the row belongs to
    /// (= <see cref="OrgAuthProviderConfig.OrgId" />; passed
    /// explicitly so a null <paramref name="oldConfig" /> still
    /// produces a correctly-scoped row).</param>
    /// <param name="actorUserId">
    /// Id of the user that triggered the PUT
    /// (<c>ICurrentUser.UserId</c>).
    /// </param>
    /// <param name="oldConfig">
    /// Snapshot BEFORE the upsert. <c>null</c> when the
    /// controller takes the first-time-setup branch (a manual DB
    /// delete left the row absent); the payload then carries
    /// <c>null</c> on every <c>old_*</c> key — a visible
    /// "first provisioning" signal for the admin UI timeline.
    /// </param>
    /// <param name="newConfig">
    /// Snapshot AFTER the upsert. Always populated.
    /// </param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>
    /// A completed <see cref="Task" />. The IAuditEmitter
    /// contract swallows emit failures.
    /// </returns>
    public static Task EmitAuthProviderChangedAsync(
        IAuditEmitter auditEmitter,
        Guid orgId,
        Guid actorUserId,
        OrgAuthProviderConfig? oldConfig,
        OrgAuthProviderConfig newConfig,
        CancellationToken cancellationToken)
    {

        // Phase 5.2 wire name — stable dot.case per AuditActions. The
        // payload key set is documented on the constant and consumed
        // by the future admin UI (5.3) to render the diff column.
        // The OIDC client secret is intentionally NOT carried in
        // the payload — a leaked audit log would expose the
        // credential.
        var payload = new Dictionary<string, object?>
        {
            ["old_provider"] = oldConfig?.Provider.ToString(),
            ["new_provider"] = newConfig.Provider.ToString(),
            ["old_oidc_authority"] = oldConfig?.OidcAuthority,
            ["new_oidc_authority"] = newConfig.OidcAuthority,
            ["old_oidc_client_id"] = oldConfig?.OidcClientId,
            ["new_oidc_client_id"] = newConfig.OidcClientId,
        };

        return auditEmitter.EmitAsync(
            AuditActions.OrgAuthProviderChanged,
            new AuditContext(
                OrgId: orgId,
                ActorUserId: actorUserId,
                TargetKind: "org_auth_provider_config",
                TargetId: newConfig.OrgId,
                Payload: payload),
            cancellationToken);
    }
}
