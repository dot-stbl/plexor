// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PkceGeneratorShould — exercise the PKCE generator (RFC 7636).
// Phase 4.6.3a. Three tests cover the spec invariants:
//   1. code_verifier length + URL-safe base64 alphabet.
//   2. code_challenge = base64url(SHA-256(code_verifier)).
//   3. Each call produces a fresh pair (no caching).
// ============================================================================

using System.Security.Cryptography;
using System.Text;
using Plexor.Modules.Sigil.Infrastructure.AuthProviders.Oidc;
using Shouldly;
using Xunit;

namespace Plexor.Modules.Sigil.Unit.AuthProviders;

/// <summary>
///     Behavioural tests for <see cref="PkceGenerator" />.
/// </summary>
public sealed class PkceGeneratorShould
{
    /// <summary>The PKCE alphabet per RFC 7636 §4.1: URL-safe
    /// base64 with no padding. Allowed characters are
    /// <c>A-Z</c>, <c>a-z</c>, <c>0-9</c>, <c>-</c>, <c>_</c>.</summary>
    private const string PkceAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    /// <summary>Given Generate is called, when it returns, then the
    /// code_verifier is 43 chars long and uses only the URL-safe
    /// base64 alphabet (no padding, no <c>+</c>, no <c>/</c>).</summary>
    [Fact(DisplayName = "Given Generate, when called, then the code_verifier is 43 chars of URL-safe base64 alphabet")]
    public void Generate_ProducesValidCodeVerifier()
    {
        var generator = new PkceGenerator();

        var pair = generator.Generate();

        pair.CodeVerifier.Length.ShouldBe(43);
        pair.CodeVerifier.All(IsPkceAlphabetChar).ShouldBeTrue();
    }

    /// <summary>Given Generate is called, when it returns, then the
    /// code_challenge is base64url(SHA-256(code_verifier)).</summary>
    [Fact(DisplayName = "Given Generate, when called, then the code_challenge is base64url(SHA-256(code_verifier))")]
    public void Generate_ProducesMatchingCodeChallenge()
    {
        var generator = new PkceGenerator();

        var pair = generator.Generate();

        var expectedChallengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(pair.CodeVerifier));
        var expectedChallenge = Convert.ToBase64String(expectedChallengeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        pair.CodeChallenge.ShouldBe(expectedChallenge);
    }

    /// <summary>Given Generate is called multiple times, when each
    /// call returns, then the pairs are distinct (no caching /
    /// reuse).</summary>
    [Fact(DisplayName = "Given Generate called multiple times, when each call returns, then each pair is unique")]
    public void Generate_ProducesDifferentPairsEachCall()
    {
        var generator = new PkceGenerator();

        var first = generator.Generate();
        var second = generator.Generate();
        var third = generator.Generate();

        first.CodeVerifier.ShouldNotBe(second.CodeVerifier);
        first.CodeVerifier.ShouldNotBe(third.CodeVerifier);
        second.CodeVerifier.ShouldNotBe(third.CodeVerifier);

        first.CodeChallenge.ShouldNotBe(second.CodeChallenge);
        first.CodeChallenge.ShouldNotBe(third.CodeChallenge);
        second.CodeChallenge.ShouldNotBe(third.CodeChallenge);
    }

    /// <summary>Given Generate, when called, then method is always
    /// <c>"S256"</c> (RFC 7636 §4.2). <c>"plain"</c> is forbidden
    /// for confidential clients (OpenID Connect Core 1.0 §15.5.2).</summary>
    [Fact(DisplayName = "Given Generate, when called, then method is always S256")]
    public void Generate_MethodIsAlwaysS256()
    {
        var generator = new PkceGenerator();

        var pair = generator.Generate();

        pair.Method.ShouldBe("S256");
    }

    private static bool IsPkceAlphabetChar(char c)
    {
        return PkceAlphabet.Contains(c);
    }
}
