// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterMappers — Mapperly source-generated implementation of
// IClusterMapper. Entity → DTO translations for read-handlers.
// The .g.cs sibling is emitted at build time by the Riok.Mapperly
// source generator; the body is just property copies plus
// user-implemented helpers for non-trivial cases.
//
// DTOs are sealed record (positional) so EF Core's
// Select(... new X(...)) translates cleanly to SQL; Mapperly uses
// the positional constructor when generating the mapping body,
// so records + source-generated mapping compose natively.
//
// Pattern:
// [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
// public partial class ClusterMapper : IClusterMapper { ... }
//
// - Target strategy: every target DTO property must be mapped.
// - [MapperIgnoreTarget]: target properties with no source
//   counterpart (computed aggregates, lookup-merged values) are
//   flagged for caller-completion.
// - [MapProperty]: connects an additional method parameter to a
//   target member by name (case-insensitive).
//
// Node tracking moved to Plexor.Modules.Outpost. The
// <c>ToNodeSummary</c> mapping lives in Outpost's mapper now.
// ==========================================================================

using Plexor.Modules.Clusters.Application.Clusters;
using Plexor.Modules.Clusters.Domain;
using Riok.Mapperly.Abstractions;

namespace Plexor.Modules.Clusters.Infrastructure.Mappers;

/// <summary>
///     Mapperly-generated mapper. Implements <see cref="IClusterMapper" />.
///     Registered in DI as a singleton via
///     <c>AddSingleton&lt;IClusterMapper, ClusterMappers&gt;()</c> — the
///     generator emits stateless bodies, so a single instance per host
///     is allocation-free.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ClusterMapper : IClusterMapper
{
    /// <summary>
    ///     Map <see cref="Cluster" /> to <see cref="ClusterSummary" />.
    ///     All positional fields are mapped 1:1 by name from the
    ///     source. Node counts and node summaries moved to
    ///     Plexor.Modules.Outpost; the list view populates them
    ///     client-side from the Outpost read API.
    /// </summary>
    public partial ClusterSummary ToSummary(Cluster source);

    /// <summary>
    ///     Map <see cref="Cluster" /> to <see cref="ClusterDetail" />,
    ///     using the supplied <paramref name="nodes" /> for the
    ///     embedded collection. <c>nodes</c> is populated by the
    ///     Outpost-side read handler; this mapper passes through
    ///     whatever the caller supplies (empty for v0.1).
    /// </summary>
    public partial ClusterDetail ToDetail(Cluster source, IReadOnlyList<ClusterNodeSummary> nodes);
}