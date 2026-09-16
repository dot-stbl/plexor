// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeReadHandlers — read-only handlers backed by Repository<NodeRecord>.
// List + Get are pure reads; both delegate to the Repository<T>
// .ListAsync / .FirstOrDefaultAsync surface.
// ============================================================================

using Plexor.Modules.Outpost.Application;
using Plexor.Modules.Outpost.Application.Abstractions;
using Plexor.Modules.Outpost.Application.NodeCommands;
using Plexor.Modules.Outpost.Infrastructure.Persistence;
using Plexor.Modules.Outpost.Infrastructure.Persistence.Specifications;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Outpost.Infrastructure.Nodes;

/// <summary>
///     List nodes in one cluster — read via
///     <see cref="NodesByClusterSpec" />. Identity projection (entity
///     rows); the caller can wrap into a <see cref="NodeSummary" /> if
///     a wire-shape projection is needed.
/// </summary>
/// <param name="nodeRepo">Node read surface.</param>
public sealed class ListNodesQueryHandler(
    Repository<NodeRecord> nodeRepo)
    : ICommandHandler<ListNodesQuery, IReadOnlyList<NodeRecord>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<NodeRecord>> HandleAsync(
        ListNodesQuery command,
        CancellationToken cancellationToken = default)
    {
        return await nodeRepo.ListAsync(
            new NodesByClusterSpec(command.ClusterId),
            cancellationToken);
    }
}

/// <summary>
///     Get one node by id — read via <see cref="NodeByIdSpec" />.
/// </summary>
/// <param name="nodeRepo">Node read surface.</param>
public sealed class GetNodeQueryHandler(
    Repository<NodeRecord> nodeRepo)
    : ICommandHandler<GetNodeQuery, NodeRecord?>
{
    /// <inheritdoc />
    public async Task<NodeRecord?> HandleAsync(
        GetNodeQuery command,
        CancellationToken cancellationToken = default)
    {
        return await nodeRepo.FirstOrDefaultAsync(
            new NodeByIdSpec(command.NodeId),
            cancellationToken);
    }
}