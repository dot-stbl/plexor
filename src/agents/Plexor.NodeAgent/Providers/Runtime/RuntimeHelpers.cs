// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RuntimeHelpers — file-static helpers shared across the runtime
// providers (Docker Compose, Podman Quadlet, k3s). Pure functions
// only — no DI, no instance state. Lives in the same folder as
// the providers per class-decomposition.md:
//   "Pure function without DI -> file static class in *Helpers.cs".
//
// Path convention: Plexor is Linux-only; all paths use forward-
// slash strings, not Path.Combine (which produces '\\' on Windows
// dev boxes).
// ============================================================================

namespace Plexor.NodeAgent.Providers.Runtime;

/// <summary>
///     Pure-function helpers shared by every runtime provider
///     (Docker Compose, Podman Quadlet, k3s). Each provider has
///     its own manifest directory constant; this helper formats
///     paths under that directory.
/// </summary>
internal static class RuntimeHelpers
{
    /// <summary>
    ///     Compose-style manifest path:
    ///     <c>{manifestsDirectory}/{workloadName}/compose.yaml</c>.
    ///     Used by Docker Compose (single file per workload).
    /// </summary>
    /// <param name="manifestsDirectory"></param>
    /// <param name="workloadName"></param>
    public static string ComposeManifestPath(string manifestsDirectory, string workloadName)
    {
        return $"{manifestsDirectory}/{workloadName}/compose.yaml";
    }

    /// <summary>
    ///     Kustomize-style manifest directory:
    ///     <c>{manifestsDirectory}/{workloadName}/</c>. Caller
    ///     writes <c>kustomization.yaml</c>, <c>deployment.yaml</c>,
    ///     and (optionally) <c>service.yaml</c> inside. Used by k3s.
    /// </summary>
    /// <param name="manifestsDirectory"></param>
    /// <param name="workloadName"></param>
    public static string KustomizeManifestDirectory(string manifestsDirectory, string workloadName)
    {
        return $"{manifestsDirectory}/{workloadName}";
    }

    /// <summary>
    ///     systemd quadlet file path:
    ///     <c>{quadletsDirectory}/{serviceName}.container</c>.
    ///     systemd resolves the corresponding <c>.service</c>
    ///     unit on <c>daemon-reload</c>. Used by Podman Quadlet.
    /// </summary>
    /// <param name="quadletsDirectory"></param>
    /// <param name="serviceName"></param>
    public static string QuadletPath(string quadletsDirectory, string serviceName)
    {
        return $"{quadletsDirectory}/{serviceName}.container";
    }

    /// <summary>
    ///     systemd unit name: <c>{serviceName}.service</c>.
    ///     Used by Podman Quadlet when invoking
    ///     <c>systemctl &lt;verb&gt; &lt;name&gt;.service</c>.
    /// </summary>
    /// <param name="serviceName"></param>
    public static string SystemdUnitName(string serviceName)
    {
        return $"{serviceName}.service";
    }

    /// <summary>
    ///     Idempotent mkdir equivalent for v0.1 (no recursive
    ///     flag needed since we're a single-host single-tenant
    ///     deployment).
    /// </summary>
    /// <param name="directory"></param>
    public static void EnsureDirectoryExists(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    ///     RFC 1123 DNS-label validation: lowercase alphanumeric
    ///     or '-', max 63 chars. K8s rejects anything else on
    ///     namespace / service / deployment create. Used by
    ///     <see cref="K3sWorkloadConfig.TryParse" /> on the
    ///     namespace field.
    /// </summary>
    /// <param name="value"></param>
    public static bool IsValidK8sName(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 63)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '-')
            {
                return false;
            }
            // Lowercase only — uppercase letters return false.
            if (char.IsAsciiLetter(c) && char.IsUpper(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Whitelist of systemd-valid <c>Restart=</c> values.
    ///     Anything else is a typo / misunderstanding of the
    ///     quadlet contract and <see cref="PodmanQuadletConfig.TryParse" />
    ///     surfaces it as a config error at parse time.
    /// </summary>
    /// <param name="value"></param>
    public static bool IsValidRestartPolicy(string value)
    {
        return value is "no" or "on-success" or "on-failure" or "on-abnormal"
            or "on-watchdog" or "on-abort" or "always";
    }
}
