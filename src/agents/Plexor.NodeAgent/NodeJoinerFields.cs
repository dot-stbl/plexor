// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeJoinerFields — small static helpers shared by NodeJoiner.
// §1a forbids private methods on production classes; the IpAddress
// + IsoVersion resolvers live here so NodeJoiner stays a flat
// orchestrator.
// ============================================================================

using System.Reflection;

namespace Plexor.NodeAgent;

/// <summary>
///     Static field-resolution helpers used by
///     <see cref="NodeJoiner" />. Extracted to a separate file so the
///     joiner class stays free of private helpers (per
///     <c>class-layout-and-tooling.md</c> §1a).
/// </summary>
internal static class NodeJoinerFields
{
    /// <summary>
    ///     Best-effort IP address for v0.1. We don't have a real
    ///     detection mechanism yet; falling back to the host portion
    ///     of the configured <c>ControlPlaneUrl</c> gets the agent
    ///     past the host's structural validation (non-empty
    ///     <c>ipAddress</c>). A future iteration will probe the
    ///     local interface + VPN.
    /// </summary>
    /// <param name="controlPlaneUrl"></param>
    public static string ResolveIpAddress(string controlPlaneUrl)
    {
        return Uri.TryCreate(controlPlaneUrl, UriKind.Absolute, out var uri)
            ? uri.Host
            : controlPlaneUrl;
    }

    /// <summary>
    ///     ISO image version the agent booted from. v0.1 falls back to
    ///     the executing assembly's informational version (set via
    ///     &lt;Version&gt; in the csproj); a real build pipeline will
    ///     inject the actual ISO version at install time.
    /// </summary>
    public static string ResolveIsoVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0.0" : version.ToString();
    }
}