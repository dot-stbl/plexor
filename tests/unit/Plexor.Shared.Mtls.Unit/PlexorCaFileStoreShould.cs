// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCaFileStore unit tests — filesystem persistence layer for the
// Plexor CA root. Every host startup reads through here, so a write
// that fails to round-trip or a missing-file path that throws the
// wrong exception type blocks every boot.
//
// We use per-test temp directories so the production /var/lib/plexor
// files are never touched, even by accident.
// ============================================================================

using System.Security.Cryptography.X509Certificates;
using Shouldly;

namespace Plexor.Shared.Mtls.Unit;

/// <summary>
///     Unit tests for <see cref="PlexorCaFileStore" /> — the PEM
///     cert + key read/write layer PlexorCaRoot depends on. Locks
///     down roundtrip fidelity, missing-file errors, and concurrent-
///     write robustness.
/// </summary>
public sealed class PlexorCaFileStoreShould
{
    /// <summary>
    ///     A WriteRoot then ReadRoot roundtrip must preserve the
    ///     cert subject and keep the private key attached — a
    ///     stripped key means no leaf can be issued against the
    ///     loaded root.
    /// </summary>
    [Fact(DisplayName = "Given a root cert, when WriteRoot then ReadRoot, then the loaded cert matches the original")]
    public void RoundtripWriteThenReadPreservesCert()
    {
        var tempDir = FileStoreTestHelpers.NewTempDir("plexor-filestore-roundtrip");
        var options = FileStoreTestHelpers.CreateOptionsInTempDir(tempDir);
        var store = new PlexorCaFileStore(options);

        try
        {
            using var original = X509Authority.CreateRoot(
                X509Authority.BuildDn("Plexor Roundtrip CA"),
                TimeSpan.FromDays(3650));

            store.WriteRoot(original);

            using var loaded = store.ReadRoot();

            X509Authority.ExtractCommonName(loaded.Subject)
                .ShouldBe("Plexor Roundtrip CA");

            loaded.GetRSAPrivateKey().ShouldNotBeNull(
                "loaded cert must carry its private key — leaves can't be " +
                "issued against a keyless CA root.");

            loaded.GetRSAPrivateKey()!.KeySize.ShouldBe(original.GetRSAPrivateKey()!.KeySize);
        }
        finally
        {
            FileStoreTestHelpers.CleanupTempDir(tempDir);
        }
    }

    /// <summary>
    ///     Reading from a directory with no cert/key files must
    ///     throw an IOException-shaped exception
    ///     (<see cref="FileNotFoundException" /> on Windows when the
    ///     parent exists, or <see cref="DirectoryNotFoundException" />
    ///     before that). PlexorCaRoot's RootExists() guards against
    ///     this in production, but direct callers (or a race) surface
    ///     the exception verbatim.
    /// </summary>
    [Fact(DisplayName = "Given a store pointing at missing files, when ReadRoot, then throws IOException")]
    public void ReadRootThrowsWhenFilesAreMissing()
    {
        var tempDir = FileStoreTestHelpers.NewTempDir("plexor-filestore-missing");
        var options = FileStoreTestHelpers.CreateOptionsInTempDir(tempDir);
        var store = new PlexorCaFileStore(options);

        try
        {
            // Create the directory so the missing-file path is the
            // only thing ReadAllBytes has to surface — without this,
            // Windows throws DirectoryNotFoundException first.
            Directory.CreateDirectory(tempDir);

            File.Exists(options.CertPath).ShouldBeFalse();
            File.Exists(options.KeyPath).ShouldBeFalse();

            Should.Throw<IOException>(store.ReadRoot);
        }
        finally
        {
            FileStoreTestHelpers.CleanupTempDir(tempDir);
        }
    }

    /// <summary>
    ///     Five concurrent WriteRoot calls must leave the on-disk
    ///     file as a single parseable PEM block — partial writes
    ///     or interleaved bytes would corrupt every host startup
    ///     that reads the file.
    /// </summary>
    [Fact(DisplayName = "Given N concurrent WriteRoot calls, when all complete, then the file on disk is a parseable, valid cert")]
    public async Task ConcurrentWritesDoNotCorruptFinalFile()
    {
        var tempDir = FileStoreTestHelpers.NewTempDir("plexor-filestore-concurrent");
        var options = FileStoreTestHelpers.CreateOptionsInTempDir(tempDir);
        var store = new PlexorCaFileStore(options);

        try
        {
            using (var seed = X509Authority.CreateRoot(
                       X509Authority.BuildDn("Plexor Seed"),
                       TimeSpan.FromDays(3650)))
            {
                store.WriteRoot(seed);
            }

            var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
            {
                using var fresh = X509Authority.CreateRoot(
                    X509Authority.BuildDn("Plexor Concurrent"),
                    TimeSpan.FromDays(3650));

                try
                {
                    store.WriteRoot(fresh);
                    return true;
                }
                catch (IOException)
                {
                    return false;
                }
            })).ToList();

            await Task.WhenAll(tasks);

            File.Exists(options.CertPath).ShouldBeTrue(
                "CA cert file must exist after concurrent writes — " +
                "missing means the file got deleted, which is impossible.");
            File.Exists(options.KeyPath).ShouldBeTrue(
                "CA key file must exist after concurrent writes.");

            var pem = await File.ReadAllTextAsync(options.CertPath);
            pem.ShouldStartWith("-----BEGIN CERTIFICATE-----");
            pem.ShouldEndWith("-----END CERTIFICATE-----");

            using var loaded = X509CertificateLoader.LoadCertificate(
                await File.ReadAllBytesAsync(options.CertPath));

            var cn = X509Authority.ExtractCommonName(loaded.Subject);
            cn.ShouldBeOneOf("Plexor Seed", "Plexor Concurrent");
        }
        finally
        {
            FileStoreTestHelpers.CleanupTempDir(tempDir);
        }
    }
}

/// <summary>
///     File-local setup helpers for the PlexorCaFileStore tests —
///     temp dir creation, options wiring, and best-effort cleanup.
///     File-scoped so it cannot leak to other test files via a
///     shared <c>Helpers/</c> folder.
/// </summary>
file static class FileStoreTestHelpers
{
    /// <summary>
    ///     Returns a unique per-test temp dir path under
    ///     <see cref="Path.GetTempPath" />. The directory is NOT
    ///     created — callers must create it themselves if needed.
    /// </summary>
    /// <param name="prefix"></param>
    public static string NewTempDir(string prefix)
    {
        return Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    ///     Builds a <see cref="CertAuthorityOptions" /> whose paths
    ///     point at <paramref name="tempDir" />. Used by both the
    ///     parent test class and the bootstrap tests (which copy
    ///     this same layout).
    /// </summary>
    /// <param name="tempDir"></param>
    public static CertAuthorityOptions CreateOptionsInTempDir(string tempDir)
    {
        return new CertAuthorityOptions
        {
            CertPath = Path.Combine(tempDir, "ca.crt"),
            KeyPath = Path.Combine(tempDir, "ca.key"),
            HostCertPath = Path.Combine(tempDir, "host.pem"),
            HostKeyPath = Path.Combine(tempDir, "host.key"),
        };
    }

    /// <summary>
    ///     Best-effort recursive delete of the per-test temp dir.
    ///     Swallows lock-conflict errors — the OS sweeps the temp
    ///     dir eventually, and failing the test on a transient
    ///     antivirus lock would be flaky.
    /// </summary>
    /// <param name="tempDir"></param>
    public static void CleanupTempDir(string tempDir)
    {
        if (Directory.Exists(tempDir))
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
