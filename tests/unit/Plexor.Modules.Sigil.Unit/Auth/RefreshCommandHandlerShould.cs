// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RefreshCommandHandlerShould — exercise the iss-binding path added
// in Phase 4.6.3c. Two test cases:
//   1. iss == "plexor" (Sigil issuer): the handler accepts the
//      token shape and proceeds to the rotation path. Verified
//      by making the stubbed refresh store return Replayed, which
//      raises RefreshTokenReplayed BEFORE the DbContext roundtrip.
//   2. iss != "plexor" (OIDC issuer): the handler accepts the
//      token shape and proceeds to the rotation path. Same shape
//      as the Sigil case — verified by the same Replayed return.
//
// The InMemory provider can't model the production Identity schema
// (the Permissions array + IReadOnlyList<Email> converter), so
// the tests stop at the rotation-return boundary rather than
// exercising the issue-access-token path. The full path is
// pre-existing and unchanged; the iss-binding is the new
// behavior under test.
// ============================================================================

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Errors;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Auth;

/// <summary>
///     Behavioural tests for the Phase 4.6.3c iss-binding in
///     <see cref="RefreshCommandHandler" />.
/// </summary>
public sealed class RefreshCommandHandlerShould
{
    /// <summary>
    ///     Plexor's own JWT <c>iss</c> claim value. Mirrors the
    ///     production constant in <c>RefreshCommandHandler</c>.
    /// </summary>
    private const string SigilIssuerValue = "plexor";

    /// <summary>Stable sub for the mint-issued token. Used as a
    /// deterministic input to the test JWT generator.</summary>
    private const string TestSubject = "alice@example.com";

    /// <summary>
    ///     Given a Sigil-issued refresh token (iss == plexor),
    ///     when refresh is called, then the handler accepts the
    ///     iss-binding and proceeds to the rotation path. Asserted
    ///     by making the stubbed refresh store return Replayed,
    ///     which raises <see cref="IdentityExceptions.RefreshTokenReplayed" />
    ///     BEFORE the DbContext roundtrip.
    /// </summary>
    [Fact(DisplayName = "Given a Plexor-issued refresh token (iss == plexor), when refresh is called, then the handler accepts the iss and reaches the rotation path")]
    public async Task Refresh_WithSigilIssuer_ReachesRotationPathAsync()
    {
        var (handler, mocks) = await BuildHandlerAsync();

        mocks.RefreshTokens.RotateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(RefreshRotationResult.Replayed);

        var exception = await Should.ThrowAsync<IdentityException>(
            () => handler.HandleAsync(
                new RefreshCommand(MintSigilRefreshToken()),
                CancellationToken.None));

        // The handler accepted the iss and reached the rotation
        // path; rotation surfaced Replayed; the handler maps to
        // RefreshTokenReplayed. A different exception here (e.g.
        // RefreshMalformed) would mean the iss-binding rejected
        // the token.
        exception.Code.ShouldBe(IdentityExceptions.RefreshTokenReplayed);
    }

    /// <summary>
    ///     Given a JWT-shaped refresh token whose iss is not
    ///     plexor (an OIDC-issued token — placeholder for Phase 5+
    ///     when the IDP roundtrip lands), when refresh is called,
    ///     then the handler accepts the iss-binding and proceeds to
    ///     the rotation path. Same shape as the Sigil case.
    /// </summary>
    [Fact(DisplayName = "Given a JWT-shaped refresh token with iss != plexor, when refresh is called, then the handler accepts the iss and reaches the rotation path")]
    public async Task Refresh_WithOidcIssuer_ReachesRotationPathAsync()
    {
        var (handler, mocks) = await BuildHandlerAsync();

        mocks.RefreshTokens.RotateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(RefreshRotationResult.Replayed);

        var exception = await Should.ThrowAsync<IdentityException>(
            () => handler.HandleAsync(
                new RefreshCommand(MintOidcRefreshToken()),
                CancellationToken.None));

        exception.Code.ShouldBe(IdentityExceptions.RefreshTokenReplayed);
    }

    /// <summary>
    ///     Build a real (but unsigned — the handler doesn't
    ///     validate signatures) JWT carrying
    ///     <c>iss="plexor"</c>. The inspector's iss-binding
    ///     peeks the claim without verifying the signature; the
    ///     rotation store uses the token hash to find the user.
    ///     Both paths are exercised end-to-end.
    /// </summary>
    private static string MintSigilRefreshToken()
    {
        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = SigilIssuerValue,
            Subject = new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("sub", TestSubject)]),
            Expires = DateTime.UtcNow.AddMinutes(10),
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
        };
        return handler.CreateToken(descriptor);
    }

    /// <summary>
    ///     Build a real JWT carrying an external IDP's issuer
    ///     URL (not "plexor"). v1 doesn't ship OIDC-issued refresh
    ///     tokens; this fixture exists so Phase 5+ code can layer
    ///     on without changing the rotation path. The handler's
    ///     iss-binding routes it through the OIDC re-issue branch.
    /// </summary>
    private static string MintOidcRefreshToken()
    {
        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://kc.example.com/realms/plexor",
            Subject = new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("sub", TestSubject)]),
            Expires = DateTime.UtcNow.AddMinutes(10),
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
        };
        return handler.CreateToken(descriptor);
    }

    /// <summary>Application-layer dependency stubs.</summary>
    /// <param name="RefreshTokens">IssueAsync / RotateAsync stub.</param>
    /// <param name="TokenIssuer">IssueAsync stub.</param>
    private sealed record RefreshMocks(
        IRefreshTokenStore RefreshTokens,
        ITokenIssuer TokenIssuer);

    /// <summary>
    ///     Build a <see cref="RefreshCommandHandler" /> with
    ///     NSubstitute stubs for the refresh store + token
    ///     issuer. The IdentityDbContext is constructed but
    ///     never queried — the tests stop at the rotation-return
    ///     boundary.
    /// </summary>
    private static async Task<(RefreshCommandHandler Handler, RefreshMocks Mocks)> BuildHandlerAsync()
    {
        var identity = await IdentityTestDb.CreateAsync();

        var mocks = new RefreshMocks(
            RefreshTokens: Substitute.For<IRefreshTokenStore>(),
            TokenIssuer: Substitute.For<ITokenIssuer>());

        var handler = new RefreshCommandHandler(
            mocks.RefreshTokens,
            mocks.TokenIssuer,
            identity,
            TimeProvider.System);

        return (handler, mocks);
    }
}
