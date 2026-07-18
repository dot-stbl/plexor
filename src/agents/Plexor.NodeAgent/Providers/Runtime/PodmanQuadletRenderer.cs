// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PodmanQuadletRenderer — pure-function spec → systemd quadlet INI
// file. File-static (no DI) per class-decomposition.md.
//
// Quadlet format reference:
//   https://docs.podman.io/en/latest/markdown/podman-systemd.unit.5.html
//
// Pinned structure (4 sections, fixed order):
//   [Unit]       — Description only (auto-restart driven by Restart
//                   in [Service], not the [Unit] key).
//   [Container]  — Image, PublishPort (one per port), Environment
//                   (one per kv, alphabetic-sorted for stability).
//   [Service]    — Restart= (driven by config.Restart).
//   [Install]    — WantedBy=multi-user.target (omitted when
//                   AutoStart=false so the unit doesn't auto-start
//                   at boot).
//
// Each line is `<Key>=<Value>`. Podman quadlets don't allow
// quoting or escaping in value (systemd does, but podman's
// parser is stricter for some keys). Environment values are
// restricted to alphanumeric + dash/underscore/equals/dot/colons
// (the systemd-friendly subset); if the app-provider needs more,
// it should use EnvironmentFile= instead (Phase 7+).
// ============================================================================

using System.Text;

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Pure-function renderer that turns a
///     <see cref="PodmanQuadletConfig" /> into a Podman Quadlet
///     INI manifest. The caller writes the rendered string to a
///     per-workload <c>&lt;name&gt;.container</c> file on disk and
///     invokes <c>systemctl daemon-reload &amp;&amp; systemctl start
///     &lt;name&gt;.service</c>.
/// </summary>
internal static class PodmanQuadletRenderer
{
    /// <summary>
    ///     Render the workload's quadlet definition. Caller writes
    ///     the returned string to
    ///     <c>/etc/containers/systemd/&lt;name&gt;.container</c> on
    ///     disk and reloads systemd.
    /// </summary>
    /// <param name="serviceName">
    ///     Service name (driven by <c>WorkloadSpec.Name</c>). Used
    ///     in the Description and becomes the systemd unit's
    ///     service identifier (<c>&lt;name&gt;.service</c>).
    /// </param>
    /// <param name="config">Parsed config (image, ports, env, etc.).</param>
    /// <exception cref="ArgumentException"></exception>
    public static string Render(string serviceName, PodmanQuadletConfig config)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException(
                "Podman quadlet service name cannot be null or whitespace.",
                nameof(serviceName));
        }

        // config is non-nullable; trust the type system per
        // .agents/rules/coding/code-shape.md §11. TryParse in the
        // provider's CreateAsync returns a non-null config or
        // throws InvalidOperationException — the null state is
        // unreachable here.

        var sb = new StringBuilder();

        // [Unit] section.
        sb.Append("[Unit]\n")
            .Append("Description=Plexor workload ")
            .Append(serviceName)
            .Append('\n')
            .Append("\n[Container]\n")
            .Append("Image=")
            .Append(config.Image)
            .Append('\n');




        // [Container] section.





        foreach (var port in config.Ports)
        {
            sb.Append("PublishPort=").Append(port).Append(':').Append(port).Append('\n');
        }

        // Environment sorted by key for byte-stable output across
        // replays (matches DockerComposeRenderer's contract).
        foreach (var kv in config.Environment.OrderBy(
                     static kv => kv.Key, StringComparer.Ordinal))
        {
            sb.Append("Environment=")
                .Append(kv.Key)
                .Append('=')
                .Append(kv.Value)
                .Append('\n');
        }

        // [Service] section.
        sb.Append("\n[Service]\n")
            .Append("Restart=").Append(config.Restart).Append('\n');

        // [Install] section — only when AutoStart. Production
        // workloads want this; one-shot / dev workloads skip it.
        if (config.AutoStart)
        {
            sb.Append("\n[Install]\n")
                .Append("WantedBy=multi-user.target\n");
        }

        return sb.ToString();
    }
}
