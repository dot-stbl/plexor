// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// JoinTokenSpecifications — reusable specs for JoinToken reads.
// ==========================================================================

using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence.Specifications;

/// <summary>
///     Find an active join token by its SHA-256 hash. Identity
///     projection (entity rows) — caller checks <c>Status</c> +
///     <c>ExpiresAt</c> after fetch.
/// </summary>
public sealed class JoinTokenByHashSpec : Specification<JoinToken, JoinToken>
{
    /// <summary>Construct from the filter parameters.</summary>
    /// <summary>Construct from the filter parameters.</summary>
    /// <param name="tokenHash">SHA-256 hex of the secret.</param>
    public JoinTokenByHashSpec(string tokenHash) : base(projection: null)
    {
        WithWhere(jt => jt.TokenHash == tokenHash);
    }
}
