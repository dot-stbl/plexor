// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertOrgAuthProviderRequestValidator — FluentValidation rule chain
// for the PUT /api/v1/iam/orgs/{orgId}/auth-provider body (4.6.1).
// Runs in the controller via [FromServices] before any DB lookup
// or IDataProtector work; failures surface as 400
// ValidationProblemDetails via OrgAuthProviderControllerHelpers.
// ============================================================================

using FluentValidation;
using Plexor.Host.Models;

namespace Plexor.Host.Validation;

/// <summary>
///     Validates the <see cref="UpsertOrgAuthProviderRequest" /> body
///     before the controller dispatches the upsert. Rules:
///     <list type="bullet">
///       <item><see cref="UpsertOrgAuthProviderRequest.Provider" />
///       is one of <c>"sigil"</c> or <c>"oidc"</c> (case-insensitive).</item>
///       <item>When <c>provider = "oidc"</c>:
///         <see cref="UpsertOrgAuthProviderRequest.OidcAuthority" />
///         is non-empty AND an absolute HTTPS URL;
///         <see cref="UpsertOrgAuthProviderRequest.OidcClientId" />
///         is non-empty and ≤ 256 chars;
///         <see cref="UpsertOrgAuthProviderRequest.OidcClientSecret" />
///         (when supplied) is ≥ 8 chars — a defensible lower bound
///         on the encrypted payload length.</item>
///       <item>When <see cref="UpsertOrgAuthProviderRequest.OidcScopes" />
///       is supplied, every entry is non-empty.</item>
///     </list>
/// </summary>
/// <remarks>
///     <para><b>Why case-insensitive provider match.</b> The wire
///     format is lowercase (<c>"sigil"</c> / <c>"oidc"</c>), but
///     callers occasionally send uppercase. The validator normalises
///     via <see cref="string.ToLowerInvariant" />; the controller
///     parses the same string with the same convention (no
///     drift).</para>
///     <para><b>Why HTTPS-only for the authority.</b> The
///     <c>oidc_authority</c> column is plaintext; a plain HTTP
///     issuer would leak the discovery URL + every issued token in
///     transit. Production deployments run over HTTPS; the test
///     environment uses self-signed certificates handled by the
///     host trust store.</para>
/// </remarks>
public sealed class UpsertOrgAuthProviderRequestValidator : AbstractValidator<UpsertOrgAuthProviderRequest>
{
    /// <summary>
    ///     The set of allowed lowercase provider discriminators. Kept
    ///     here so the test project + controller parser stay
    ///     aligned.
    /// </summary>
    public static readonly IReadOnlyCollection<string> AllowedProviders = ["sigil", "oidc"];

    /// <summary>Construct the validator with the standard rule chain.</summary>
    public UpsertOrgAuthProviderRequestValidator()
    {
        RuleFor(static request => request.Provider)
            .NotEmpty()
                .WithMessage("Provider is required.")
            .Must(static provider => AllowedProviders.Contains(provider.ToLowerInvariant()))
                .WithMessage("Provider must be 'sigil' or 'oidc'.");

        When(
            static request => request.Provider.Equals("oidc", StringComparison.OrdinalIgnoreCase),
            () =>
            {
                RuleFor(static request => request.OidcAuthority)
                    .NotEmpty()
                        .WithMessage("OidcAuthority is required when Provider is 'oidc'.")
                    .Must(static url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                                            && uri.Scheme == Uri.UriSchemeHttps)
                        .WithMessage("OidcAuthority must be an absolute HTTPS URL.");

                RuleFor(static request => request.OidcClientId)
                    .NotEmpty()
                        .WithMessage("OidcClientId is required when Provider is 'oidc'.")
                    .MaximumLength(256)
                        .WithMessage("OidcClientId must be 256 characters or fewer.");

                RuleFor(static request => request.OidcClientSecret!)
                    .MinimumLength(8)
                        .WithMessage("OidcClientSecret must be at least 8 characters.")
                    .When(static request => request.OidcClientSecret is not null);
            });

        RuleFor(static request => request.OidcScopes!)
            .Must(static scopes => scopes.All(static s => !string.IsNullOrWhiteSpace(s)))
                .WithMessage("Every OidcScopes entry must be non-empty.")
            .When(static request => request.OidcScopes is not null);
    }
}
