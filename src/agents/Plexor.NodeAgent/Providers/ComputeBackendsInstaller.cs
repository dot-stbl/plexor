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
    /// <param name="services"></param>
    /// <param name="configuration"></param>
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
    /// <param name="services"></param>
    /// <param name="configuration"></param>
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
    /// <param name="services"></param>
    public static IServiceCollection AddLinuxBridgeNetwork(this IServiceCollection services)
    {
        services.AddSingleton<INetworkBackend, LinuxBridgeBackend>();
        return services;
    }

    /// <summary>
    ///     Register <see cref="HttpImageRegistry" /> against the
    ///     configured catalogue (<c>NodeAgent:Images:Http</c>)
    ///     and a named <see cref="IHttpClientFactory" /> client for
    ///     image downloads. Default cache directory when unset:
    ///     <c>/var/lib/plexor/images-cache</c>.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <remarks>
    ///     The named HttpClient uses a 10-minute total request
    ///     timeout — cloud images are 500MB-2GB so a 30-second
    ///     default is too tight. Redirects are followed; on 4xx /
    ///     5xx the registry throws <see cref="HttpRequestException" />
    ///     so the caller (the workload provider) surfaces it as a
    ///     domain error rather than silently swallowing the
    ///     failure.
    /// </remarks>
    public static IServiceCollection AddHttpImageRegistry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<HttpImageRegistryOptions>(configuration.GetSection("NodeAgent:Images:Http"));

        services.AddHttpClient(HttpImageRegistry.HttpClientName, static client =>
        {
            // 10 minutes — Ubuntu cloud image is ~600MB, on a
            // slow mirror a 30-second default would time out.
            client.Timeout = TimeSpan.FromMinutes(10);
            // We're fetching operator-curated catalogue entries —
            // follow redirects (CDN storage may redirect to a
            // region-specific URL).
            client.DefaultRequestHeaders.Add("User-Agent", "plexor-nodeagent/0.1");
        });

        services.AddSingleton<IImageRegistry, HttpImageRegistry>();
        return services;
    }
}
