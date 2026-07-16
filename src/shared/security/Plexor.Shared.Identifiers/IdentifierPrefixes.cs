// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IdentifierPrefixes — file-scoped constants for the entity ID prefixes
// Plexor uses. Each strongly-typed value object in this assembly
// references the matching prefix here; no ID can ever be constructed
// without the correct prefix attached.
//
// Keep this file adjacent to the value objects so a future rename of a
// prefix has a single edit point. Constructor parameter names on the
// value objects echo these strings — grep `cluster_` here if you want
// to find every ID type that starts with that prefix.
// ============================================================================

namespace Plexor.Shared.Identifiers;

/// <summary>
///     String prefixes for every Plexor entity ID type. Each prefix is
///     a globally unique, type-specific token that lets log lines,
///     URLs, and grep queries disambiguate which kind of ID they are
///     dealing with without context.
/// </summary>
file static class IdentifierPrefixes
{
    /// <summary>Cluster aggregate — forge.clusters row.</summary>
    public const string Cluster = "cluster_";

    /// <summary>Node within a cluster — forge.nodes row.</summary>
    public const string Node = "node_";

    /// <summary>Join token issued by host (one-time, hashed on disk).</summary>
    public const string Token = "tok_";

    /// <summary>Workload managed by Plexor (currently a Docker container).</summary>
    public const string Workload = "wl_";
}
