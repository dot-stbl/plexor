// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IQuotaUsageReader — read-only surface for QuotaUsage rows. The 4.5.g.2
// GET /api/v1/quotas/usage endpoint resolves the effective limit via
// IQuotaScopeResolver and pairs it with the latest consumption snapshot
// from this reader.
// ============================================================================

using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Application.Quotas;

/// <summary>
///     Read-only queries against the <see cref="QuotaUsage" />
///     snapshot table. The 4.5.g.2 usage endpoint pairs each row with
///     the resolved effective limit from <see cref="IQuotaScopeResolver" />.
/// </summary>
/// <remarks>
///     <para><b>Why a separate reader.</b> QuotaUsage rows are mutated
///     by the 4.5.b enforcer inside its transaction; the read path
///     does not need the same locking. A dedicated reader keeps the
///     enforcer's contract narrow (write-only via CheckAndReserveAsync)
///     and the read surface testable in isolation against the
///     InMemory provider.</para>
///     <para><b>Tenant scoping.</b> The reader filters by
///     <c>OrgId</c> at the query level — callers in Org X never see
///     Org Y's snapshots.</para>
/// </remarks>
public interface IQuotaUsageReader
{
    /// <summary>
    ///     List every usage row for the given scope + org. Returns an
    ///     empty list when no snapshots exist — never null.
    /// </summary>
    /// <param name="scopeKind">Org / Team / Folder discriminator.</param>
    /// <param name="scopeId">Id of the matching Realm entity.</param>
    /// <param name="orgId">Tenant boundary — results are filtered to
    /// rows where <c>OrgId = orgId</c>.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<IReadOnlyList<QuotaUsage>> ListForScopeAsync(
        QuotaScopeKind scopeKind,
        Guid scopeId,
        Guid orgId,
        CancellationToken cancellationToken);
}
