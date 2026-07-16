// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ComputeBackendsInstaller — DI registration for the storage /
// image / network backends that the workload providers consume.
//
// v0.1 ships one of each (LocalDirStorage, LocalDirImageRegistry,
// LinuxBridgeBackend). Multi-node deployments register additional
// backends alongside (e.g. CephRbdStorage on the same NodeAgent
// as LocalDirStorage) — the workload provider picks the right one
// per volume via the VolumeSpec / NetworkSpec.
//
// Registration is opt-in per backend: comment out the line for a
// backend the operator doesn't have configured. Without a volume
// backend the workload provider fails loudly at first Create call,
// which is preferable to silently picking a default.
// ==========================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plexor.NodeAgent.Providers.Image;
using Plexor.NodeAgent.Providers.Network;
using Plexor.NodeAgent.Providers.Storage;
using Plexor.Shared.Compute;

namespace Plexor.NodeAgent.Providers;

/// <summary>
///     DI registration for compute backends. One method per
///     backend family (storage, image, network) so operators
///     can mix-and-match per node.
/// </summary>
public static class ComputeBackendsInstaller
{
    /// <summary>
    ///     Register LocalDirStorage against the configured root
    ///     directory (<c>NodeAgent:Storage:LocalDir:Root</c>).
    ///     Default root when unset: <c>/var/lib/plexor/volumes</c>.
    /// </summary>
    public static IServiceCollection AddLocalDirStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var root = configuration["NodeAgent:Storage:LocalDir:Root"]
                   ?? "/var/lib/plexor/volumes";

        services.AddSingleton<IVolumeBackend>(_ => new LocalDirStorage(
            root,
            _.GetRequiredService<IImageRegistry>(),
            _.GetRequiredService<ILogger<LocalDirStorage>>()));

        return services;
    }

    /// <summary>
    ///     Register LocalDirImageRegistry against the configured
    ///     catalog (<c>NodeAgent:Images</c>). Empty catalog
    ///     when unset — every ref lookup throws
    ///     <see cref="UnknownImageException" />.
    /// </summary>
    public static IServiceCollection AddLocalDirImageRegistry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ImageRegistryOptions>(configuration.GetSection("NodeAgent:Images"));
        services.AddSingleton<IImageRegistry, LocalDirImageRegistry>();
        return services;
    }

    /// <summary>
    ///     Register LinuxBridgeBackend as the default network
    ///     backend. v0.1 ships only this; future OVS / OVN
    ///     backends register alongside.
    /// </summary>
    public static IServiceCollection AddLinuxBridgeNetwork(this IServiceCollection services)
    {
        services.AddSingleton<INetworkBackend, LinuxBridgeBackend>();
        return services;
    }
}
