namespace Plexor.Modules.Clusters.Domain;

/// <summary>
///     Aggregated counts of nodes by lifecycle status — used by
/// cluster-detail / cluster-list pages and by the Plexor.Host
/// dashboard.
/// </summary>
/// <remarks>
///     <para><b>Why a record.</b> Pure data — value equality (two
///     clusters with the same breakdown are equivalent for the
///     dashboard) and immutability (no setter means no partial-write
///     race when the UI renders).</para>
///     <para><b>Default construction.</b> Positional ctor with five
///     ints. <c>ClusterSummary.NodeCounts</c> defaults to all-zeros
///     until <see cref="NodeSpec.Aggregate" /> fills it in.</para>
/// </remarks>
public sealed record NodeCounts(
    int Total,
    int Ready,
    int Pending,
    int Offline,
    int Draining);
