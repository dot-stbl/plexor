// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadAssignedNodeIdConverter — EF Core ValueConverter for the
// nullable <see cref="NodeId" /> column on forge.workloads
// (assigned_node_id). EF Core's expression-tree builder rejects
// inline lambdas with `is null` patterns (CS8122), so a dedicated
// converter class is required — same pattern as
// NullableNodeIdConverter on join_tokens.redeemed_by_node_id.
// ============================================================================

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence;

/// <summary>
///     Converts <see cref="NodeId" />? to/from <c>string</c> for the
///     <c>assigned_node_id</c> column. Null passes through unchanged
///     so a workload that's still in <c>Pending</c> (placement
///     pending) keeps a SQL NULL column.
/// </summary>
public sealed class WorkloadAssignedNodeIdConverter : ValueConverter<NodeId?, string?>
{
    /// <summary>
    ///     Construct the converter. The two delegates are plain
    ///     methods (not lambdas) because EF Core's expression-tree
    ///     builder cannot compile inline <c>is null</c> patterns.
    /// </summary>
    public WorkloadAssignedNodeIdConverter()
        : base(
            convertToProviderExpression: static id => ToProvider(id),
            convertFromProviderExpression: static raw => FromProvider(raw))
    {
    }

    private static string? ToProvider(NodeId? value)
    {
        return value?.ToString();
    }

    private static NodeId? FromProvider(string? raw)
    {
        return raw is null ? null : IdParse.ParseNodeId(raw);
    }
}
