// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfQuotaUsageReader — EF-backed read-only surface for QuotaUsage rows.
// Pairs the latest consumption snapshot with the effective limit
// resolved by the 4.5.b scope walker on the 4.5.g.2 GET endpoint.
// ============================================================================

using Plexor.Modules.Quotas.Application.Quotas;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     Read-only reader over <c>quotas.quota_usage</c>. Scoped — shares
///     the per-request DbContext with the 4.5.g.2 controller.
/// </summary>
/// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
/// <remarks>
///     <para><b>Why scoped, not singleton.</b> Same lifetime as the
///     DbContext; <c>QuotaUsage</c> rows are mutated by the 4.5.b
///     enforcer inside the resource-create transaction and the reader
///     must see the same per-request snapshot.</para>
///     <para><b>Read-only.</b> Every query is
///     <c>AsNoTracking()</c>; the 4.5.b enforcer is the only writer
///     and lives in its own class. The reader has no mutation surface
///     to confuse with the enforcer's contract.</para>
/// </remarks>
public sealed class EfQuotaUsageReader(QuotasDbContext db) : IQuotaUsageReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuotaUsage>> ListForScopeAsync(
        QuotaScopeKind scopeKind,
        Guid scopeId,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        return await EfQuotaUsageReaderHelpers.ListForScopeInternalAsync(
            db,
            scopeKind,
            scopeId,
            orgId,
            cancellationToken);
    }
}
