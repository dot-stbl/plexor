// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeId — strongly-typed Plexor node identifier.
//
// Wire format: "node_" + UUIDv7 in lowercase, no dashes.
//             e.g. node_0190f4d6c8e7b2a9f8c1d4e5a7b3c6d
//
// In Phase B (mTLS) this ID appears as the CN of the X.509 client
// certificate Plexor issues for each NodeAgent at join. The CN is
// the canonical identity — no separate "nodeId" claim is required.
// ============================================================================

namespace Plexor.Shared.Identifiers;

/// <summary>
///     Identifies a <c>Node</c> row in <c>forge.nodes</c>.
///     The string form is <c>node_</c> + UUIDv7 lowercase no-dashes.
/// </summary>
/// <param name="Value">Raw UUIDv7 bytes.</param>
public readonly partial record struct NodeId(Guid Value) : IParsable<NodeId>
{
    /// <inheritdoc />
    public override string ToString()
    {
        return Prefix + IdParse.FormattedUuid(Value);
    }

    /// <summary>Canonical literal prefix.</summary>
    public const string Prefix = "node_";

    /// <inheritdoc />
    /// <remarks>See <see cref="ClusterId.Parse" /> for ASP.NET Core
    /// model-binder contract.</remarks>
    public static NodeId Parse(string s, IFormatProvider? provider)
    {
        return IdParse.ParseNodeId(s);
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out NodeId result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }

        try
        {
            result = IdParse.ParseNodeId(s);
            return true;
        }
        catch (FormatException)
        {
            result = default;
            return false;
        }
    }
}
