// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeAgentOptions — agent-side configuration that affects runtime
// behaviour beyond the control-plane URL. The cert triple is the
// key piece: the join flow writes the cert + key returned by
// the host into a known directory; the mTLS HTTP client then
// loads that file on every call to prove the agent's identity.
// ============================================================================

using System.IO;

namespace Plexor.NodeAgent.Composition;

/// <summary>
///     mTLS options for the NodeAgent. Read from configuration
///     section <c>NodeAgent:Mtls</c> (env-var override via
///     <c>NODEAGENT__MTLS__*</c>).
/// </summary>
public sealed class NodeAgentOptions
{
    /// <summary>Config section name (matches appsettings).</summary>
    public const string SectionName = "NodeAgent:Mtls";

    /// <summary>
    ///     Directory where the agent persists the mTLS cert + key
    ///     returned by the host on join. Created on first
    ///     startup if absent. Mode 0700 on Unix (cert/key files
    ///     inside are mode 0600). Defaults to
    ///     <c>~/.plexor/agent/</c> — same dot-dir convention as
    ///     the host's data root, but agent-scoped.
    /// </summary>
    public string CertDirectory { get; init; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".plexor", "agent");

    /// <summary>File name for the client cert inside <see cref="CertDirectory" />.</summary>
    public string CertFileName { get; init; } = "node.crt";

    /// <summary>File name for the client cert private key.</summary>
    public string KeyFileName { get; init; } = "node.key";

    /// <summary>
    ///     File name for the Plexor CA root (downloaded from the
    ///     host on join). Pinned by the HttpClient when validating
    ///     the host's server cert.
    /// </summary>
    public string CaFileName { get; init; } = "ca.crt";

    /// <summary>Path to the Plexor CA root used to pin the host's server cert.</summary>
    public string CaPath =>
        Path.Combine(CertDirectory, CaFileName);

    /// <summary>Path to the agent's client cert.</summary>
    public string CertPath =>
        Path.Combine(CertDirectory, CertFileName);

    /// <summary>Path to the agent's client cert private key.</summary>
    public string KeyPath =>
        Path.Combine(CertDirectory, KeyFileName);

    /// <summary>
    ///     Set to <c>true</c> once the join flow has written the
    ///     mTLS triple. The HttpClient factory is gated on this
    ///     so a fresh agent that hasn't joined yet doesn't try to
    ///     load a non-existent cert file.
    /// </summary>
    public bool Enrolled { get; set; }
}
