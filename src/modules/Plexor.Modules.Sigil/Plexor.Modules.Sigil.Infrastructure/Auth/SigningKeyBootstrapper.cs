// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SigningKeyBootstrapper — IHostedService that ensures at least one
// active signing key exists on startup. Generates an ECDSA P-256
// keypair if the signing_keys table is empty.
// ============================================================================

using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Sigil.Application.Auth;
using Plexor.Modules.Sigil.Domain.Entities;

namespace Plexor.Modules.Sigil.Infrastructure.Auth;

/// <summary>
///     Ensures the <c>signing_keys</c> table has an active keypair
///     on application startup. v0.1: "first writer wins" — if no
///     active key exists, generate an ECDSA P-256 keypair with a
///     kid derived from the current year + quarter, export to
///     PKCS#8 PEM, insert it.
/// </summary>
/// <remarks>
///     <para><b>Why IHostedService (startup), not BackgroundService.</b>
///     This runs once on host start, then never again. No need for
///     a long-running loop.</para>
///     <para><b>Why "first writer wins".</b> v0.1 runs a single
///     host per deployment. If two hosts start simultaneously and
///     both find the table empty, both try to insert the same
///     <c>kid = key_YYYY_Qn</c> — the unique key constraint
///     guarantees only one succeeds. The losing host logs a
///     conflict, retries, and finds the winner's row.</para>
///     <para><b>Future.</b> Phase 2 introduces a distributed lock
///     (Postgres advisory or Redis) to serialize the race and
///     skip the retry-on-conflict path.</para>
/// </remarks>
public sealed class SigningKeyBootstrapper(
    ISigningKeyRepository keys,
    ILogger<SigningKeyBootstrapper> logger) : IHostedService
{
    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (await keys.GetActiveAsync(cancellationToken) is { } existing)
        {
            logger.LogInformation(
                "Signing key already active (kid={Kid}); skipping bootstrap.",
                existing.Kid);
            return;
        }

        var (kid, publicPem, privatePem) = GenerateKeypair();
        var key = new SigningKey
        {
            Kid = kid,
            Algorithm = "ES256",
            PublicKeyPem = publicPem,
            PrivateKeyPem = privatePem,
            CreatedAt = DateTimeOffset.UtcNow,
            NotAfter = null,
        };

        try
        {
            await keys.AddAsync(key, cancellationToken);
            logger.LogInformation(
                "Generated and stored active signing key (kid={Kid}, alg={Algorithm}).",
                key.Kid, key.Algorithm);
        }
        catch (Exception ex)
        {
            // Likely a race with another host that inserted the
            // same kid. Log and retry once — the second call to
            // GetActiveAsync should find the winner.
            logger.LogWarning(ex,
                "Failed to insert signing key (kid={Kid}); retrying with the existing key.",
                kid);

            if (await keys.GetActiveAsync(cancellationToken) is not { } afterRace)
            {
                throw;
            }

            logger.LogInformation(
                "Adopting signing key written by a peer host (kid={Kid}).",
                afterRace.Kid);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static (string Kid, string PublicPem, string PrivatePem) GenerateKeypair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var publicPem = ecdsa.ExportSubjectPublicKeyInfoPem();
        var privatePem = ecdsa.ExportPkcs8PrivateKeyPem();
        var kid = $"key_{DateTime.UtcNow:yyyy}_q{DateTime.UtcNow.Month / 4 + 1}";
        return (kid, publicPem, privatePem);
    }
}