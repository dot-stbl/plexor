// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IdGenerator — single seam for minting new Plexor IDs. Wraps the
// UUIDNext UUIDv7 generator so the rest of the codebase does not
// know about UUIDNext directly; if we ever swap implementations
// (UUIDNext, custom RFC 9562, NanoID) only this file changes.
//
// UUIDv7 is time-ordered: the 48 most significant bits are a
// Unix-ms timestamp. Two IDs minted within the same millisecond on
// different machines can still collide on the random tail, but
// ORDER BY id is monotonic in creation time and EF Core can use
// the index as a substitute for ORDER BY created_at when paginating
// by ID.
// ============================================================================

using UUIDNext;

namespace Plexor.Shared.Identifiers;

/// <summary>
///     One-stop factory for the four prefixed Plexor ID types plus
///     <see cref="JoinTokenSecret" /> (which is not prefixed — see
///     <see cref="JoinTokenSecret.New" />).
/// </summary>
public static class IdGenerator
{
    /// <summary>Mint a new <see cref="ClusterId" /> with the canonical <c>cluster_</c> prefix.</summary>
    public static ClusterId NewClusterId()
    {
        return new ClusterId(Uuid.NewDatabaseFriendly(Database.PostgreSql));
    }

    /// <summary>Mint a new <see cref="NodeId" /> with the canonical <c>node_</c> prefix.</summary>
    public static NodeId NewNodeId()
    {
        return new NodeId(Uuid.NewDatabaseFriendly(Database.PostgreSql));
    }

    /// <summary>Mint a new <see cref="TokenId" /> with the canonical <c>tok_</c> prefix.</summary>
    public static TokenId NewTokenId()
    {
        return new TokenId(Uuid.NewDatabaseFriendly(Database.PostgreSql));
    }

    /// <summary>Mint a new <see cref="WorkloadId" /> with the canonical <c>wl_</c> prefix.</summary>
    public static WorkloadId NewWorkloadId()
    {
        return new WorkloadId(Uuid.NewDatabaseFriendly(Database.PostgreSql));
    }
}
