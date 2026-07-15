// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// ClusterId — strongly-typed Plexor cluster identifier.
//
// Wire format:  "cluster_" + UUIDv7 in lowercase, no dashes.
//              e.g. cluster_0190f4d6c8e7b2a9f8c1d4e5a7b3c6d
//
// Storage:      varchar(64) in forge.clusters.id
// Ordering:     monotonic by creation time (UUIDv7); ORDER BY id is
//               a stable cursor for keyset pagination.
// Parsing:      use ClusterId.TryParse / ClusterId.Parse (in IdParse.cs).
//               Bare UUIDv7 (without the prefix) is rejected.
// ============================================================================

namespace Plexor.Shared.Identifiers;

/// <summary>
///     Identifies a <c>Cluster</c> aggregate in <c>forge.clusters</c>.
///     The string form is <c>cluster_</c> + UUIDv7 lowercase no-dashes.
/// </summary>
/// <param name="Value">Raw UUIDv7 bytes (16 bytes, time-ordered).</param>
public readonly partial record struct ClusterId(Guid Value)
{
    /// <summary>The canonical string form of this ID.</summary>
    public override string ToString()
    {
        return Prefix + IdParse.FormattedUuid(Value);
    }

    /// <summary>
    ///     The literal string prefix for this ID type. Used by the
    ///     parser in <see cref="IdParse" /> to dispatch on type.
    /// </summary>
    public const string Prefix = "cluster_";
}
