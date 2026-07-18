// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// LinuxBridgeBackend — INetworkBackend reference impl backed by
// Linux kernel bridges managed by libvirt's `virsh net-*`
// commands. Each network in the catalog is a libvirt-managed
// virtual network; attaching a VM creates an `virsh
// attach-interface` invocation that wires the VM's NIC to the
// bridge.
//
// Volume lifecycle (network side):
//   - AttachAsync: virsh net-define + net-start (idempotent —
//     `define` errors on existing network, ignored), then
//     return NetworkInterfaceHandle with the bridge name (e.g.
//     "br-prod-vpc"). The VM provider references that name in
//     <interface type="bridge"><source bridge="..."/></interface>.
//   - DetachAsync: virsh net-destroy + net-undefine. Idempotent
//     — missing network is OK.
//
// NetworkSpec.Name doubles as the libvirt network name (must
// be DNS-safe + libvirt-compliant).
// ==========================================================================

using Plexor.NodeAgent.Providers.Common;
using Plexor.Shared.Compute;

namespace Plexor.NodeAgent.Providers.Network;

/// <summary>
///     Linux bridge backed by libvirt's <c>virsh net-*</c>. v0.1's
///     only network backend.
/// </summary>
/// <param name="logger"></param>
public sealed class LinuxBridgeBackend(ILogger<LinuxBridgeBackend> logger) : INetworkBackend
{
    /// <summary>Stable backend name — used in <see cref="NetworkInterfaceHandle.BackendName" />.</summary>
    public const string BackendName = "linux-bridge";

    /// <inheritdoc />
    public async Task<NetworkInterfaceHandle> AttachAsync(NetworkSpec networkSpec, CancellationToken cancellationToken)
    {
        // Idempotent: `virsh net-define` errors if the network
        // is already defined; we swallow that specific case. The
        // second call (net-start) is also idempotent.
        try
        {
            await LibvirtRunner.RunAsync(
                LibvirtKvmProvider.LibvirtUri,
                $"net-define {LinuxBridgeBackendXml.BuildNetworkXml(networkSpec.Name)}",
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
        {
            // Network already defined — fine. Continue to net-start
            // in case it's not running.
        }

        await LibvirtRunner.RunAsync(
            LibvirtKvmProvider.LibvirtUri,
            $"net-start {networkSpec.Name}",
            cancellationToken);

        logger.LogInformation(
            "LinuxBridgeBackend: attached network {Name} (kind={Kind})",
            networkSpec.Name,
            networkSpec.Kind);

        return new NetworkInterfaceHandle(BackendName, networkSpec.Name);
    }

    /// <inheritdoc />
    public async Task DetachAsync(NetworkInterfaceHandle handle, CancellationToken cancellationToken)
    {
        if (handle.BackendName != BackendName)
        {
            throw new ArgumentException(
                $"NetworkInterfaceHandle was issued by backend '{handle.BackendName}', "
                + $"not '{BackendName}'.",
                nameof(handle));
        }

        // Idempotent — destroy + undefine both no-op on missing.
        // Catch the union of "virsh not installed" (Win32Exception)
        // and "virsh exited non-zero because the resource is gone"
        // (InvalidOperationException from LibvirtRunner). Both
        // mean the detach is already done from our perspective.
        try
        {
            await LibvirtRunner.RunAsync(
                LibvirtKvmProvider.LibvirtUri,
                $"net-destroy {handle.Reference}",
                cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Network wasn't running, or virsh missing. Continue to undefine.
        }

        try
        {
            await LibvirtRunner.RunAsync(
                LibvirtKvmProvider.LibvirtUri,
                $"net-undefine {handle.Reference}",
                cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Network didn't exist, or virsh missing. Treat as success — idempotent.
        }

        logger.LogInformation(
            "LinuxBridgeBackend: detached network {Name}",
            handle.Reference);
    }
}
