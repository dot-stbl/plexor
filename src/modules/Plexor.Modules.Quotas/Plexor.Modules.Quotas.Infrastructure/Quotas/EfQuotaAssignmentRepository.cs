// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfQuotaAssignmentRepository — EF-backed implementation of
// IQuotaAssignmentRepository. Read paths are exercised by the 4.5.g.2
// GET endpoints; write paths (UpsertAsync / DeleteAsync) are declared
// in 4.5.g.2 but only exercised from 4.5.g.3's PUT / DELETE handlers.
// ============================================================================

using Plexor.Modules.Quotas.Application.Quotas;
using Plexor.Modules.Quotas.Domain;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     Read + write surface over <c>quotas.quota_assignments</c>.
///     Read methods project to <see cref="QuotaAssignment" /> via
///     <c>AsNoTracking()</c>; the write paths load + mutate tracked
///     entities and rely on EF's change tracker for the round-trip.
/// </summary>
/// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the
/// row's <c>CreatedAt</c> / <c>UpdatedAt</c> stamps.</param>
/// <remarks>
///     <para><b>Why scoped, not singleton.</b> The repo shares the
///     caller's per-request DbContext — same lifetime pattern as the
///     enforcer (4.5.b).</para>
///     <para><b>Why a TimeProvider.</b> Tests can substitute a
///     deterministic clock (e.g. <c>FakeTimeProvider</c>) to assert
///     exact stamp values; the production code reads
///     <see cref="TimeProvider.System" /> from the composition root.</para>
/// </remarks>
public sealed class EfQuotaAssignmentRepository(
    QuotasDbContext db,
    TimeProvider clock) : IQuotaAssignmentRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<QuotaAssignment>> ListForScopeAsync(
        QuotaScopeKind scopeKind,
        Guid scopeId,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        return await EfQuotaAssignmentRepositoryHelpers.ListForScopeInternalAsync(
            db,
            scopeKind,
            scopeId,
            orgId,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<QuotaAssignment?> FindAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        return EfQuotaAssignmentRepositoryHelpers.FindInternalAsync(
            db,
            assignmentId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<QuotaAssignment> UpsertAsync(
        Guid definitionId,
        QuotaScope scope,
        decimal value,
        QuotaPeriod period,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        return await EfQuotaAssignmentRepositoryHelpers.UpsertInternalAsync(
            db,
            clock,
            definitionId,
            scope,
            value,
            period,
            createdBy,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        await EfQuotaAssignmentRepositoryHelpers.DeleteInternalAsync(
            db,
            assignmentId,
            cancellationToken);
    }
}
