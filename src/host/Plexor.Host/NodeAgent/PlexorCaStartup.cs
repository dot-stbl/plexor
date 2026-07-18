// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PlexorCaStartup — IHostedService that triggers an eager load of the
// Plexor CA root on host boot. Pure side-effect-free wrapper around
// PlexorCaRoot — the actual root generation lives in the root service.
// ============================================================================

using Plexor.Shared.Mtls;

namespace Plexor.Host.NodeAgent;

/// <summary>
///     Warms the <see cref="PlexorCaRoot" /> cache on host startup.
///     If the filesystem is misconfigured (no write access to the
///     CA directory, bad permissions, etc.) the call fails here at
///     boot — the operator sees the error in the systemd log instead
///     of the first NodeJoin attempt.
/// </summary>
/// <param name="caRoot"></param>
public sealed class PlexorCaStartup(PlexorCaRoot caRoot) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Discard-less: returning the result isn't needed, the side
        // effect (loading + caching the root cert) is the entire point.
        caRoot.GetCertificate();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
