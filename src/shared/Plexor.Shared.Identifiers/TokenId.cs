// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// TokenId — strongly-typed Plexor join-token identifier.
//
// This is the row ID of the join_tokens table. The actual credential
// the node proves itself with (the "secret") is the separate
// JoinTokenSecret type — see JoinTokenSecret.cs. Confusing the two
// is impossible at compile time because neither will accidentally
// fit the other's properties.
// ============================================================================

namespace Plexor.Shared.Identifiers;

/// <summary>
///     Identifies a <c>JoinToken</c> row in <c>forge.join_tokens</c>.
///     The string form is <c>tok_</c> + UUIDv7 lowercase no-dashes.
/// </summary>
/// <param name="Value">Raw UUIDv7 bytes.</param>
public readonly partial record struct TokenId(Guid Value)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return Prefix + IdParse.FormattedUuid(Value);
    }

    /// <summary>Canonical literal prefix.</summary>
    public const string Prefix = "tok_";
}
