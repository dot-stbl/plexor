// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeAgentWorker — placeholder BackgroundService. Real implementation in
// follow-up phase streams tasks from Plexor.Host via gRPC, dispatches to
// local providers (libvirt, ovs-vsctl, rbd, etc.).
// ============================================================================

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Plexor.NodeAgent;

internal sealed class NodeAgentWorker : BackgroundService
{
    private readonly ILogger<NodeAgentWorker> logger;

    public NodeAgentWorker(ILogger<NodeAgentWorker> logger)
    {
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("plexor-nodeagent 0.1.0-dev (skeleton) starting");
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("plexor-nodeagent stopping");
        }
    }
}