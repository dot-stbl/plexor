// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// JoinToken entity. Plan: .agents/docs/plans/plan-clusters.md.
//
// JoinToken is a one-time credential a node uses to authenticate its
// first heartbeat / mTLS handshake. Issued via
// POST /api/v1/compute/clusters/{id}/tokens; redeemed (one-time) on
// POST /api/v1/compute/clusters/join; rotated / revoked explicitly.
// ============================================================================

using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Clusters.Domain.Entities;

/// <summary>
///     One-time bearer credential for joining a node to a cluster.
/// Issuance sets <see cref="Status" /> = <see cref="TokenStatus.Active" />;
/// join consumption flips Status to Revoked, recording the node id via
/// <see cref="RedeemedByNodeId" />.
/// </summary>
public sealed class JoinToken : IFilterableEntity, ICreatedAt
{
    /// <summary>TokenId — strongly-typed <c>tok_&lt;UUIDv7&gt;</c> wire format.</summary>
    public TokenId Id { get; init; }

    /// <summary>FK to <see cref="Cluster.Id" />.</summary>
    public ClusterId ClusterId { get; init; }

    /// <summary>Tenant scope (denormalized).</summary>
    public Guid OrgId { get; init; }

    /// <summary>Short human label set at issuance
    /// (e.g. "node-1 (prod-eu-1)").</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Current lifecycle status — see <see cref="TokenStatus" />.</summary>
    public TokenStatus Status { get; init; }

    /// <summary>SHA-256 of the base64url-encoded secret value shown
    /// ONCE on issuance. Plexor.Host never persists the plaintext;
    /// lookups compare hash-against-hash at join time.</summary>
    public string TokenHash { get; init; } = string.Empty;

    /// <summary>Restrict the token to a specific role (single-node
    /// cluster, control-plane node, …). A control-plane token
    /// cannot be used to join a compute role.</summary>
    public NodeRole IntendedRole { get; init; }

    /// <summary>ISO versions the node must be on. Compared at join
    /// time; non-matching versions warn (the operator may be
    /// mid-upgrade).</summary>
    public string MinIsoVersion { get; init; } = string.Empty;

    /// <summary>When the token was issued (UTC). Satisfies the
    /// audit "when was this created" question and is the canonical
    /// timestamp (see <see cref="CreatedAt" />).</summary>
    public DateTimeOffset IssuedAt { get; init; }

    /// <summary>
    ///     Satisfies <see cref="ICreatedAt.CreatedAt" />. Stored as a
    /// dedicated column so the filtering DSL + paged queries can sort /
    /// filter on it without JOINing the audit trail. Set from
    /// <see cref="IssuedAt" /> by the handler at creation time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the token expires (UTC). Default TTL is 7 days.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>When the token was consumed by a successful join, the
    /// node's id is recorded here. Combined with
    /// <see cref="Status" /> = <see cref="TokenStatus.Revoked" />
    /// it makes the join flow audit-clear.</summary>
    public NodeId? RedeemedByNodeId { get; init; }
}
