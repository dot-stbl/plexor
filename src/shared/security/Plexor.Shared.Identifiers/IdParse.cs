// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IdParse — static helpers for parsing wire-format IDs back into
// strongly-typed values, and the shared FormattedUuid helper used
// by every ID type's ToString() implementation.
//
// Wire format: "<prefix>_<UUIDv7-lowercase-no-dashes>" — 26 chars
// after the prefix (UUIDv7 is 128 bits = 32 hex chars with dashes
// or 32 hex chars without dashes; we use the dashes-stripped 32-char
// representation, then lowercase it).
// ============================================================================

using System.Runtime.CompilerServices;

namespace Plexor.Shared.Identifiers;

/// <summary>
///     String-based entry points for parsing prefixed IDs and for
///     converting raw <see cref="Guid" /> values to the canonical
///     wire-format UUIDv7 string.
/// </summary>
public static class IdParse
{
    /// <summary>
    ///     Length of a UUIDv7 hex string with dashes stripped
    ///     (32 chars, lowercase). Matches the canonical form
    ///     <see cref="FormattedUuid" /> emits.
    /// </summary>
    internal const int UuidHexLength = 32;

    /// <summary>
    ///     Convert a <see cref="Guid" /> to its ULID-style canonical
    ///     form — lowercase, no dashes, 32 chars. Used by every
    ///     ID type's <c>ToString</c> implementation. UUIDNext's
    ///     <c>Guid.ToString("N")</c> already gives this format.
    /// </summary>
    /// <param name="value">The Guid to format. Must not be <see cref="Guid.Empty" />.</param>
    /// <returns>32-char lowercase hex string without dashes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormattedUuid(Guid value)
    {
        return value.ToString("N");
    }

    /// <summary>
    ///     Parse a wire-format ID into a <see cref="ClusterId" />.
    ///     Throws <see cref="FormatException" /> on malformed input
    ///     (missing prefix, wrong body length, non-hex chars).
    /// </summary>
    /// <param name="raw"></param>
    public static ClusterId ParseClusterId(string raw)
    {
        return new ClusterId(ParseUuidAfterPrefix(raw, ClusterId.Prefix));
    }

    /// <summary>Parse a wire-format ID into a <see cref="NodeId" />.</summary>
    /// <param name="raw"></param>
    public static NodeId ParseNodeId(string raw)
    {
        return new NodeId(ParseUuidAfterPrefix(raw, NodeId.Prefix));
    }

    /// <summary>Parse a wire-format ID into a <see cref="TokenId" />.</summary>
    /// <param name="raw"></param>
    public static TokenId ParseTokenId(string raw)
    {
        return new TokenId(ParseUuidAfterPrefix(raw, TokenId.Prefix));
    }

    /// <summary>Parse a wire-format ID into a <see cref="WorkloadId" />.</summary>
    /// <param name="raw"></param>
    public static WorkloadId ParseWorkloadId(string raw)
    {
        return new WorkloadId(ParseUuidAfterPrefix(raw, WorkloadId.Prefix));
    }

    /// <summary>
    ///     Common parser: verify <paramref name="raw" /> begins with
    ///     <paramref name="prefix" />, then convert the trailing
    ///     32 hex chars into a <see cref="Guid" />.
    /// </summary>
    /// <param name="raw"></param>
    /// <param name="prefix"></param>
    /// <exception cref="FormatException"></exception>
    private static Guid ParseUuidAfterPrefix(string raw, string prefix)
    {
        if (!raw.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new FormatException($"ID must start with '{prefix}' (got '{raw}').");
        }

        if (raw.Length != prefix.Length + UuidHexLength)
        {
            throw new FormatException(
                $"ID body must be exactly {UuidHexLength} hex chars after the prefix (got {raw.Length - prefix.Length}).");
        }

        return Guid.ParseExact(
            raw[prefix.Length..],
            "N");
    }
}
