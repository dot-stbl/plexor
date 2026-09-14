// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// JwtSigningServiceShould — exercises the ES256 signer/verifier
// against NSubstitute-mocked key + revocation repos. The signer reads
// the active keypair from the repository, builds a compact JWT,
// and exposes it through IssuedAccessToken; the verifier checks
// signature, lifetime, kid header, and post-verify revocation state.
// ============================================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using NSubstitute;
using Plexor.Modules.Sigil.Application.Abstractions;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;
using Plexor.Modules.Sigil.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.Infrastructure;

/// <summary>
///     Behavioural tests for <see cref="JwtSigningService" />.
///     Each test generates an ECDSA P-256 keypair in-memory so the
///     signer/verifier round-trip works without a real Postgres —
///     the only stubbed dependencies are
///     <see cref="ISigningKeyRepository" /> and
///     <see cref="IUserRevocationChecker" />.
/// </summary>
public sealed class JwtSigningServiceShould
{
    /// <summary>Verifies that <see cref="JwtSigningService.IssueAsync" />
    /// produces a compact JWT whose header carries the active
    /// keypair's <c>kid</c> and whose payload carries the supplied
    /// claims + Plexor's issuer value.</summary>
    [Fact(DisplayName = "Given active keypair, when IssueAsync, then compact JWT carries kid + iss + supplied claims")]
    public async Task IssueAsyncProducesJwtWithKidAndIssuerAsync()
    {
        var (kid, publicPem, privatePem) = GenerateEcdsaKeypair();
        var keys = Substitute.For<ISigningKeyRepository>();
        keys.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new SigningKey
            {
                Kid = kid,
                Algorithm = "ES256",
                PublicKeyPem = publicPem,
                PrivateKeyPem = privatePem,
                CreatedAt = DateTimeOffset.UtcNow,
                NotAfter = null,
            });
        var revocation = Substitute.For<IUserRevocationChecker>();
        var service = new JwtSigningService(keys, revocation, TimeProvider.System);
        var identity = new ClaimsIdentity(
            [
                new Claim(IdentityClaims.UserId, Guid.NewGuid().ToString()),
                new Claim(IdentityClaims.TenantId, Guid.NewGuid().ToString()),
                new Claim(IdentityClaims.Permission, "compute.vms.read"),
            ],
            authenticationType: "test");
        var principal = new ClaimsPrincipal(identity);

        var issued = await service.IssueAsync(principal);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.CompactJwt);
        jwt.Header["kid"].ShouldBe(kid);
        jwt.Issuer.ShouldBe(IdentityClaims.IssuerValue);
        jwt.Claims.ShouldContain(static claim => claim.Type == IdentityClaims.UserId);
        jwt.Claims.ShouldContain(static claim => claim.Type == IdentityClaims.TenantId);
        jwt.Claims.ShouldContain(static claim =>
            claim.Type == IdentityClaims.Permission && claim.Value == "compute.vms.read");
        issued.ExpiresAtUtc.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    /// <summary>Verifies that <see cref="JwtSigningService.IssueAsync" />
    /// throws <see cref="InvalidOperationException" /> when no active
    /// signing key exists — the bootstrapper should have ensured
    /// one, but a misconfigured deployment surfaces this loudly.</summary>
    [Fact(DisplayName = "Given no active signing key, when IssueAsync, then throws InvalidOperationException")]
    public async Task IssueAsyncThrowsWhenNoActiveKeyAsync()
    {
        var keys = Substitute.For<ISigningKeyRepository>();
        keys.GetActiveAsync(Arg.Any<CancellationToken>()).Returns((SigningKey?)null);
        var revocation = Substitute.For<IUserRevocationChecker>();
        var service = new JwtSigningService(keys, revocation, TimeProvider.System);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.IssueAsync(new ClaimsPrincipal()));
    }

    /// <summary>Verifies that <see cref="JwtSigningService.VerifyAsync" />
    /// accepts a freshly-issued token and returns the original
    /// principal via <see cref="VerifyResult.Success" />.</summary>
    [Fact(DisplayName = "Given valid token, when VerifyAsync, then returns Success with the original principal")]
    public async Task VerifyAsyncAcceptsFreshTokenAsync()
    {
        var keys = Substitute.For<ISigningKeyRepository>();
        var revocation = Substitute.For<IUserRevocationChecker>();
        revocation.IsStillValidAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(new RevocationCheckResult.Active());
        var (kid, publicPem, privatePem) = GenerateEcdsaKeypair();
        keys.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new SigningKey
            {
                Kid = kid,
                Algorithm = "ES256",
                PublicKeyPem = publicPem,
                PrivateKeyPem = privatePem,
                CreatedAt = DateTimeOffset.UtcNow,
                NotAfter = null,
            });
        keys.GetByKidAsync(kid, Arg.Any<CancellationToken>())
            .Returns(new SigningKey
            {
                Kid = kid,
                Algorithm = "ES256",
                PublicKeyPem = publicPem,
                PrivateKeyPem = privatePem,
                CreatedAt = DateTimeOffset.UtcNow,
                NotAfter = null,
            });
        var service = new JwtSigningService(keys, revocation, TimeProvider.System);

        var identity = new ClaimsIdentity(
            [new Claim(IdentityClaims.UserId, Guid.NewGuid().ToString())],
            authenticationType: "test");
        var issued = await service.IssueAsync(new ClaimsPrincipal(identity));

        var result = await service.VerifyAsync(issued.CompactJwt);

        var success = result.ShouldBeOfType<VerifyResult.Success>();
        success.Principal.FindFirst(IdentityClaims.UserId).ShouldNotBeNull();
    }

    /// <summary>Verifies that <see cref="JwtSigningService.VerifyAsync" />
    /// surfaces a tampered signature as
    /// <see cref="VerifyResult.Invalid" /> — the bearer handler
    /// maps this to a 401 with an <c>error_description</c>.</summary>
    [Fact(DisplayName = "Given tampered token, when VerifyAsync, then returns Invalid")]
    public async Task VerifyAsyncRejectsTamperedTokenAsync()
    {
        var (kid, publicPem, privatePem) = GenerateEcdsaKeypair();
        var keys = Substitute.For<ISigningKeyRepository>();
        keys.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new SigningKey
            {
                Kid = kid,
                Algorithm = "ES256",
                PublicKeyPem = publicPem,
                PrivateKeyPem = privatePem,
                CreatedAt = DateTimeOffset.UtcNow,
                NotAfter = null,
            });
        keys.GetByKidAsync(kid, Arg.Any<CancellationToken>())
            .Returns(new SigningKey
            {
                Kid = kid,
                Algorithm = "ES256",
                PublicKeyPem = publicPem,
                PrivateKeyPem = privatePem,
                CreatedAt = DateTimeOffset.UtcNow,
                NotAfter = null,
            });
        var revocation = Substitute.For<IUserRevocationChecker>();
        var service = new JwtSigningService(keys, revocation, TimeProvider.System);

        var issued = await service.IssueAsync(new ClaimsPrincipal(new ClaimsIdentity()));
        var tampered = issued.CompactJwt[..^2] + "AA";

        var result = await service.VerifyAsync(tampered);

        result.ShouldBeAssignableTo<VerifyResult.Invalid>();
    }

    /// <summary>Verifies that <see cref="JwtSigningService.VerifyAsync" />
    /// surfaces a non-JWT string (missing dots, invalid base64) as
    /// a failure outcome — either
    /// <see cref="VerifyResult.Malformed" /> or
    /// <see cref="VerifyResult.Invalid" />. The bearer handler maps
    /// both to a 401; the distinction is informational for the
    /// <c>error_description</c>.</summary>
    [Fact(DisplayName = "Given non-JWT garbage, when VerifyAsync, then returns a failure outcome (Malformed or Invalid)")]
    public async Task VerifyAsyncRejectsNonJwtGarbageAsync()
    {
        var (kid, publicPem, privatePem) = GenerateEcdsaKeypair();
        var keys = Substitute.For<ISigningKeyRepository>();
        keys.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(new SigningKey
            {
                Kid = kid,
                Algorithm = "ES256",
                PublicKeyPem = publicPem,
                PrivateKeyPem = privatePem,
                CreatedAt = DateTimeOffset.UtcNow,
                NotAfter = null,
            });
        var revocation = Substitute.For<IUserRevocationChecker>();
        var service = new JwtSigningService(keys, revocation, TimeProvider.System);

        var result = await service.VerifyAsync("not-a-jwt");

        result.ShouldNotBeOfType<VerifyResult.Success>();
    }

    private static (string Kid, string PublicPem, string PrivatePem) GenerateEcdsaKeypair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicPem = ecdsa.ExportSubjectPublicKeyInfoPem();
        var privatePem = ecdsa.ExportPkcs8PrivateKeyPem();
        return ($"key_{Guid.NewGuid():N}", publicPem, privatePem);
    }
}
