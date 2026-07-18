// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// MtlsCertWriter — persists the cert triple returned by the host
// on join to disk so the SocketsHttpHandler can load it on every
// subsequent call. Idempotent: overwrites any existing cert files
// in the configured directory (operator runs the agent against a
// fresh host — old certs would just fail revocation checks).
//
// Mode 0600 on the cert + key files on Unix (no world-readable /
// group-readable). The directory itself gets mode 0700. On Windows
// the FileSystemAcl layer isn't engaged — the dev-certs/ on a
// Windows dev box is process-isolated by the OS already.
// ============================================================================

namespace Plexor.NodeAgent.Composition;

/// <summary>
///     Writes the mTLS cert triple (client cert + key + CA root)
///     from a join response to disk. After this runs the
///     <see cref="NodeAgentOptions.Enrolled" /> flag flips to
///     <c>true</c> so the HttpClient factory starts loading the
///     cert on every call.
/// </summary>
public static class MtlsCertWriter
{
    /// <summary>
    ///     Write the cert + key + CA root to disk under
    ///     <see cref="NodeAgentOptions.CertDirectory" />. Sets
    ///     <see cref="NodeAgentOptions.Enrolled" /> = true on
    ///     success.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="response"></param>
    public static void Persist(NodeAgentOptions options, Shared.NodeApi.JoinResponse response)
    {
        Directory.CreateDirectory(options.CertDirectory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                options.CertDirectory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        File.WriteAllText(options.CertPath, response.NodeCertificatePem);
        File.WriteAllText(options.KeyPath, response.NodePrivateKeyPem);
        File.WriteAllText(options.CaPath, response.CaCertificatePem);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                options.CertPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetUnixFileMode(
                options.KeyPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        options.Enrolled = true;
    }

    /// <summary>
    ///     True iff the three cert files exist on disk and are
    ///     non-empty. The agent uses this on startup to decide
    ///     whether to re-run the join flow (no cert → join) or
    ///     skip it (cert present → assume already enrolled).
    /// </summary>
    /// <param name="options"></param>
    public static bool AlreadyEnrolled(NodeAgentOptions options)
    {
        return File.Exists(options.CertPath) &&
               File.Exists(options.KeyPath) &&
               File.Exists(options.CaPath) &&
               new FileInfo(options.CertPath).Length > 0;
    }

    /// <summary>
    ///     Wipe the on-disk cert triple. Used on operator-requested
    ///     re-enrollment (e.g. CA rotation, node replacement).
    /// </summary>
    /// <param name="options"></param>
    public static void Forget(NodeAgentOptions options)
    {
        if (File.Exists(options.CertPath))
        {
            File.Delete(options.CertPath);
        }
        if (File.Exists(options.KeyPath))
        {
            File.Delete(options.KeyPath);
        }
        if (File.Exists(options.CaPath))
        {
            File.Delete(options.CaPath);
        }
        options.Enrolled = false;
    }
}
