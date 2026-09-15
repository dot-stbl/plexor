// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCertificateIssuer — ICertificateAuthority implementation.
// Composes the pieces: sign leaves against the cached CA root, verify
// presented certs against the same root + the revoked-serial cache.
//
// Single point of contact for handlers and the mTLS middleware. No
// filesystem, no BouncyCastle, no cache plumbing — all delegated.
// ============================================================================

using System.Security.Cryptography.X509Certificates;

namespace Plexor.Shared.Mtls;

/// <summary>
///     Issues and verifies Plexor client certs. The hot path
///     (<see cref="VerifyClientCert" />) does a single X509Chain
///     build + a single dictionary lookup on the revoked-cache; the
///     rest is composition.
/// </summary>
/// <param name="caRoot"></param>
/// <param name="revokedCache"></param>
public sealed class PlexorCertificateIssuer(
    PlexorCaRoot caRoot,
    RevokedCertCache revokedCache) : ICertificateAuthority
{

    /// <inheritdoc />
    public X509Certificate2 IssueClientCert(
        X500DistinguishedName subject,
        TimeSpan ttl)
    {
        var caCert = caRoot.GetCertificate();
        return X509Authority.IssueLeaf(subject, caCert, ttl);
    }

    /// <inheritdoc />
    public bool VerifyClientCert(X509Certificate2 candidate)
    {
        var caCert = caRoot.GetCertificate();

        // 1. Chain build under our CA.
        var chain = new X509Chain();
        chain.ChainPolicy.ExtraStore.Add(caCert);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

        if (!chain.Build(candidate))
        {
            return false;
        }

        // 2. SECURITY: with AllowUnknownCertificateAuthority, chain.Build
        //    returns true for any foreign-CA-signed chain. Verify the
        //    chain actually terminates at OUR CA root — not just any CA —
        //    by comparing RawData of the final chain element.
        //    Without this check, an attacker with any CA could register a
        //    node with Plexor if they produced a cert with CN starting
        //    "node_" (Guid format) and not in the revocation list.
        if (chain.ChainElements.Count == 0)
        {
            return false;
        }

        var chainRoot = chain.ChainElements[^1].Certificate;
        if (!chainRoot.RawData.SequenceEqual(caCert.RawData))
        {
            return false;
        }

        // 3. Revocation check (cached).
        if (revokedCache.IsRevoked(candidate.SerialNumber))
        {
            return false;
        }

        // 4. CN must be a node_ prefixed Plexor NodeId — defence in
        // depth: the middleware also dispatches by CN prefix, but
        // refuse anything that isn't a Plexor node cert here too.
        var cn = ExtractCn(candidate.SubjectName.Name);
        return cn.StartsWith("node_", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public byte[] GetRootCertificatePem()
    {
        return caRoot.GetPem();
    }

    /// <inheritdoc />
    public X509Certificate2 GetRootCertificate()
    {
        return caRoot.GetCertificate();
    }

    private static string ExtractCn(string distinguishedName)
    {
        foreach (var part in distinguishedName.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.Ordinal))
            {
                return trimmed[3..];
            }
        }
        return string.Empty;
    }
}
