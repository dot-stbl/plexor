// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// UpsertOrgAuthProviderRequestValidatorShould — exercise the 4.6.1
// FluentValidation chain for the
// PUT /api/v1/iam/orgs/{orgId}/auth-provider body in isolation. No
// DB, no controller — the validator is a pure function over the
// request DTO. Lives in Plexor.Host.UnitTests because the DTO +
// validator types live in Plexor.Host alongside the controller.
// ============================================================================

using FluentValidation.TestHelper;
using Plexor.Host.Models;
using Plexor.Host.Validation;
using Shouldly;
using Xunit;

namespace Plexor.Host.UnitTests;

/// <summary>
///     Behavioural tests for
///     <see cref="UpsertOrgAuthProviderRequestValidator" />. Covers
///     the rule chain documented on the validator's class remarks:
///     non-empty provider in {sigil, oidc}, HTTPS-only
///     <c>OidcAuthority</c> when Oidc, non-empty client id,
///     optional client secret with a minimum length when supplied,
///     and non-empty scope entries when supplied.
/// </summary>
public sealed class UpsertOrgAuthProviderRequestValidatorShould
{
    private static UpsertOrgAuthProviderRequest BuildRequest(
        string provider = "sigil",
        string? oidcAuthority = null,
        string? oidcClientId = null,
        string? oidcClientSecret = null,
        IReadOnlyList<string>? oidcScopes = null)
    {
        return new UpsertOrgAuthProviderRequest
        {
            Provider = provider,
            OidcAuthority = oidcAuthority,
            OidcClientId = oidcClientId,
            OidcClientSecret = oidcClientSecret,
            OidcScopes = oidcScopes,
        };
    }

    /// <summary>Given a Sigil request with no OIDC fields, when the
    /// validator runs, then no errors are reported.</summary>
    [Fact(DisplayName = "Given a Sigil request, when ValidateAsync runs, then no errors are reported")]
    public async Task Validate_WithSigilProvider_PassesAsync()
    {
        var validator = new UpsertOrgAuthProviderRequestValidator();

        var result = await validator.TestValidateAsync(BuildRequest());

        result.IsValid.ShouldBeTrue();
    }

    /// <summary>Given an OIDC request with all required fields
    /// populated and an HTTPS authority, when the validator runs,
    /// then no errors are reported.</summary>
    [Fact(DisplayName = "Given a fully-populated OIDC request, when ValidateAsync runs, then no errors are reported")]
    public async Task Validate_WithOidcProvider_AllFieldsSet_PassesAsync()
    {
        var validator = new UpsertOrgAuthProviderRequestValidator();

        var result = await validator.TestValidateAsync(BuildRequest(
            provider: "oidc",
            oidcAuthority: "https://kc.example.com/realms/plexor",
            oidcClientId: "plexor-console",
            oidcClientSecret: "secret-of-sufficient-length",
            oidcScopes: ["openid", "profile", "email", "groups"]));

        result.IsValid.ShouldBeTrue();
    }

    /// <summary>Given an empty <c>Provider</c>, when the validator
    /// runs, then the <c>Provider</c> rule fails.</summary>
    [Fact(DisplayName = "Given empty Provider, when ValidateAsync runs, then Provider rule fails")]
    public async Task Validate_WithEmptyProvider_ReturnsErrorAsync()
    {
        var validator = new UpsertOrgAuthProviderRequestValidator();

        var result = await validator.TestValidateAsync(BuildRequest(provider: string.Empty));

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static request => request.Provider);
    }

    /// <summary>Given an OIDC request with a non-HTTPS
    /// <c>OidcAuthority</c>, when the validator runs, then the
    /// <c>OidcAuthority</c> rule fails.</summary>
    [Fact(DisplayName = "Given OIDC + Http OidcAuthority, when ValidateAsync runs, then OidcAuthority rule fails")]
    public async Task Validate_WithOidcProvider_HttpUrl_ReturnsErrorAsync()
    {
        var validator = new UpsertOrgAuthProviderRequestValidator();

        var result = await validator.TestValidateAsync(BuildRequest(
            provider: "oidc",
            oidcAuthority: "http://kc.example.com/realms/plexor",
            oidcClientId: "plexor-console",
            oidcClientSecret: "secret-of-sufficient-length"));

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static request => request.OidcAuthority);
    }

    /// <summary>Given an OIDC request with a missing
    /// <c>OidcAuthority</c>, when the validator runs, then the
    /// rule fails.</summary>
    [Fact(DisplayName = "Given OIDC + null OidcAuthority, when ValidateAsync runs, then OidcAuthority rule fails")]
    public async Task Validate_WithOidcProvider_NoAuthority_ReturnsErrorAsync()
    {
        var validator = new UpsertOrgAuthProviderRequestValidator();

        var result = await validator.TestValidateAsync(BuildRequest(
            provider: "oidc",
            oidcClientId: "plexor-console",
            oidcClientSecret: "secret-of-sufficient-length"));

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static request => request.OidcAuthority);
    }

    /// <summary>Given an OIDC request with an empty
    /// <c>OidcClientId</c>, when the validator runs, then the
    /// rule fails.</summary>
    [Fact(DisplayName = "Given OIDC + empty OidcClientId, when ValidateAsync runs, then OidcClientId rule fails")]
    public async Task Validate_WithOidcProvider_EmptyClientId_ReturnsErrorAsync()
    {
        var validator = new UpsertOrgAuthProviderRequestValidator();

        var result = await validator.TestValidateAsync(BuildRequest(
            provider: "oidc",
            oidcAuthority: "https://kc.example.com/realms/plexor",
            oidcClientId: string.Empty,
            oidcClientSecret: "secret-of-sufficient-length"));

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static request => request.OidcClientId);
    }

    /// <summary>Given an OIDC request with a short
    /// <c>OidcClientSecret</c>, when the validator runs, then
    /// the rule fails.</summary>
    [Fact(DisplayName = "Given OIDC + short OidcClientSecret, when ValidateAsync runs, then OidcClientSecret rule fails")]
    public async Task Validate_WithOidcProvider_ShortClientSecret_ReturnsErrorAsync()
    {
        var validator = new UpsertOrgAuthProviderRequestValidator();

        var result = await validator.TestValidateAsync(BuildRequest(
            provider: "oidc",
            oidcAuthority: "https://kc.example.com/realms/plexor",
            oidcClientId: "plexor-console",
            oidcClientSecret: "short"));

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static request => request.OidcClientSecret);
    }

    /// <summary>Given an OIDC request with an empty scope entry,
    /// when the validator runs, then the scopes rule fails.</summary>
    [Fact(DisplayName = "Given OIDC + empty scope entry, when ValidateAsync runs, then OidcScopes rule fails")]
    public async Task Validate_WithOidcProvider_EmptyScopeEntry_ReturnsErrorAsync()
    {
        var validator = new UpsertOrgAuthProviderRequestValidator();

        var result = await validator.TestValidateAsync(BuildRequest(
            provider: "oidc",
            oidcAuthority: "https://kc.example.com/realms/plexor",
            oidcClientId: "plexor-console",
            oidcClientSecret: "secret-of-sufficient-length",
            oidcScopes: ["openid", "", "email"]));

        result.IsValid.ShouldBeFalse();
        result.ShouldHaveValidationErrorFor(static request => request.OidcScopes);
    }

    /// <summary>Given a Sigil request with mixed-case
    /// <c>Provider</c> (<c>"Sigil"</c>), when the validator
    /// runs, then the rule accepts it (case-insensitive).</summary>
    [Fact(DisplayName = "Given a Sigil request with mixed-case Provider, when ValidateAsync runs, then no errors are reported")]
    public async Task Validate_WithSigilProvider_MixedCase_PassesAsync()
    {
        var validator = new UpsertOrgAuthProviderRequestValidator();

        var result = await validator.TestValidateAsync(BuildRequest(provider: "Sigil"));

        result.IsValid.ShouldBeTrue();
    }
}
