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
public readonly partial record struct ClusterId(Guid Value) : IParsable<ClusterId>
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

    /// <inheritdoc />
    /// <remarks>
    ///     Implemented explicitly so ASP.NET Core's model binder (which
    ///     looks for <c>static T Parse(string, IFormatProvider?)</c>
    ///     since .NET 7) can map <c>[FromRoute] ClusterId</c> parameters
    ///     from the wire-format string directly. Throws
    ///     <see cref="FormatException" /> on missing prefix or
    ///     malformed body — the binder surfaces this as a 400 with
    ///     the inner exception message.
    /// </remarks>
    public static ClusterId Parse(string s, IFormatProvider? provider)
    {
        return IdParse.ParseClusterId(s);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     Returns false on <c>null</c>, missing prefix, wrong body
    ///     length, or non-hex characters. Never throws — the binder
    ///     translates false into a 400 with a deterministic error
    ///     path.
    /// </remarks>
    public static bool TryParse(string? s, IFormatProvider? provider, out ClusterId result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        try
        {
            result = IdParse.ParseClusterId(s);
            return true;
        }
        catch (FormatException)
        {
            result = default;
            return false;
        }
    }
}
