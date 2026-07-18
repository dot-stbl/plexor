// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// JoinTokenRepository — per-module subclass of Repository<JoinToken>.
// ==========================================================================

using Plexor.Modules.Clusters.Domain;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence;

/// <summary>
///     Read repository for <see cref="JoinToken" />. The token
///     lookup-by-hash path goes through here (read); rotation +
///     redemption use DbContext directly (write).
/// </summary>
/// <param name="db">The shared <c>forge</c> schema DbContext.</param>
public sealed class JoinTokenRepository(ClusterDbContext db) : Repository<JoinToken>
{
    /// <inheritdoc />
    protected override IQueryable<JoinToken> Query => db.JoinTokens;
}
