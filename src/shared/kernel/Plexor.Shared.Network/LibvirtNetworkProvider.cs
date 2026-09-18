// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LibvirtNetworkProvider — INetworkProvider reference impl backed
// by libvirt's `virsh` CLI on a Linux node. Pure orchestration:
// every line is a thin wrapper around the static LibvirtRunner +
// LibvirtNetListParser. Pure functions live in their own files;
// this class only stitches them together with logging + OS guard.
//
// OS guard: libvirt is Linux-only. Calling code on a non-Linux
// host (Windows CI, macOS dev box) gets a clear PlatformNotSupported
// exception instead of a confusing Process.Start failure.
// ============================================================================

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Plexor.Shared.Network;

/// <summary>
///     <see cref="INetworkProvider" /> backed by the libvirt
///     <c>virsh</c> CLI. v0.1 reference impl; v0.2+ may swap in a
///     libvirt-client library without changing the seam.
/// </summary>
/// <param name="libvirtUri">
///     The libvirt URI the runner connects to. Defaults to
///     <c>qemu:///system</c> (the standard hypervisor instance);
///     tests inject a custom URI to point at a fixture.
/// </param>
/// <param name="logger"></param>
public sealed class LibvirtNetworkProvider(
    Uri? libvirtUri = null,
    ILogger<LibvirtNetworkProvider>? logger = null) : INetworkProvider
{
    /// <summary>
    ///     Default libvirt URI — the system-wide QEMU/KVM
    ///     instance. The host's default-bridge lives here.
    /// </summary>
    public static readonly Uri DefaultLibvirtUri = new("qemu:///system", UriKind.Absolute);

    private readonly Uri libvirtUri = libvirtUri ?? DefaultLibvirtUri;
    private readonly ILogger<LibvirtNetworkProvider> logger = logger ?? NullLogger<LibvirtNetworkProvider>.Instance;

    /// <inheritdoc />
    [ExcludeFromCodeCoverage] // requires a Linux host with libvirt; coverage via integration suite
    public async Task<IReadOnlyList<string>> ListNetworksAsync(CancellationToken cancellationToken = default)
    {
        EnsureLinuxHost();

        var stdout = await LibvirtRunner.RunAsync(libvirtUri, "net-list --all", cancellationToken);
        var names = LibvirtNetListParser.ParseNames(stdout);

        logger.LogDebug(
            "LibvirtNetworkProvider: listed {Count} network(s) on {Uri}",
            names.Count,
            libvirtUri);

        return names;
    }

    /// <inheritdoc />
    [ExcludeFromCodeCoverage] // requires a Linux host with libvirt; coverage via integration suite
    public async Task<string?> ResolveBridgeNameAsync(CancellationToken cancellationToken = default)
    {
        EnsureLinuxHost();

        var names = await ListNetworksAsync(cancellationToken);
        if (names.Count == 0)
        {
            logger.LogWarning(
                "LibvirtNetworkProvider: no networks defined on {Uri}; returning null",
                libvirtUri);
            return null;
        }

        // Prefer the libvirt "default" network when present — that's
        // the canonical NAT'd bridge every fresh libvirtd ships
        // with. Fall back to the first user-defined bridge.
        foreach (var name in names)
        {
            if (string.Equals(name, "default", StringComparison.Ordinal))
            {
                return name;
            }
        }

        return names[0];
    }

    /// <summary>
    ///     Throws <see cref="PlatformNotSupportedException" /> on
    ///     non-Linux hosts. Called at the entry point of every
    ///     method that shells out so callers fail fast on Windows
    ///     CI before the OS-specific Process.Start blows up.
    /// </summary>
    private static void EnsureLinuxHost()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException(
                "LibvirtNetworkProvider requires a Linux host (libvirt is not available on Windows or macOS).");
        }
    }
}