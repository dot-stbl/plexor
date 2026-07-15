// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NullableNodeIdConverter — EF Core ValueConverter for the
// nullable <see cref="NodeId" /> column on forge.join_tokens
// (RedeemedByNodeId). EF Core's expression-tree builder rejects
// inline lambdas with `is null` patterns (CS8122), so a dedicated
// converter class is required for this single use.
// ============================================================================

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Plexor.Shared.Identifiers;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence;

/// <summary>
///     Converts <see cref="NodeId" />? to/from <c>string</c> for the
///     <c>redeemed_by_node_id</c> column. Null passes through unchanged
///     so unscoped join tokens (freshly issued, never redeemed) keep
///     a SQL NULL column.
/// </summary>
public sealed class NullableNodeIdConverter : ValueConverter<NodeId?, string?>
{
    /// <summary>
    ///     Construct the converter. The two delegates here are plain
    ///     methods (not lambdas) because EF Core's expression-tree
    ///     builder cannot compile inline <c>is null</c> patterns.
    ///     Construction-time conversion is what we want for a
    ///     <see cref="ValueConverter" /> anyway — the delegates run
    ///     at materialisation, not inside the SQL query.
    /// </summary>
    public NullableNodeIdConverter()
        : base(
            convertToProviderExpression: id => ToProvider(id),
            convertFromProviderExpression: raw => FromProvider(raw))
    {
    }

    private static string? ToProvider(NodeId? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.GetValueOrDefault().Value.ToString();
    }

    private static NodeId? FromProvider(string? raw)
    {
        return raw is null
            ? (NodeId?)null
            : new NodeId(Guid.ParseExact(raw, "N"));
    }
}
