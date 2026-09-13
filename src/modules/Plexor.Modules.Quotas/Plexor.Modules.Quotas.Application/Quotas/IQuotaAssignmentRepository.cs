// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// IQuotaAssignmentRepository — read + write surface for QuotaAssignment
// rows. Read paths are exercised by the 4.5.g.2 GET endpoints; the
// write paths land in 4.5.g.3 (PUT / DELETE). Defining the full
// surface now keeps 4.5.g.3 free of interface churn.
// ============================================================================

using Plexor.Modules.Quotas.Domain;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Application.Quotas;

/// <summary>
///     Read + write surface for the polymorphic
///     <see cref="QuotaAssignment" /> table. The 4.5.g.2 controllers
///     depend on this contract — not on the EF-backed implementation
///     in <c>Plexor.Modules.Quotas.Infrastructure</c> — so unit tests
///     can substitute a fake without spinning up Postgres.
/// </summary>
/// <remarks>
///     <para><b>Tenant scoping.</b> Read paths take <c>orgId</c> as a
///     parameter; the implementation filters by it at the query level
///     so callers in Org X can never see Org Y's data. The
///     <c>QuotasController</c> sources the <c>orgId</c> from
///     <c>ICurrentUser.TenantId</c>.</para>
///     <para><b>Upsert semantics.</b> <see cref="UpsertAsync" /> matches
///     on <c>(DefinitionId, ScopeKind, ScopeId, Period)</c> — the
///     composite UNIQUE index on <c>quota_assignments</c>. A new
///     combination inserts; an existing combination updates the value
///     and bumps <c>UpdatedAt</c>. The 4.5.g.3 PUT path uses this to
///     keep the same <c>CreatedAt</c> / <c>CreatedBy</c> on update.</para>
/// </remarks>
public interface IQuotaAssignmentRepository
{
    /// <summary>
    ///     List every assignment at the given scope for the given org.
    ///     Returns an empty list when no rows match — never null.
    /// </summary>
    /// <param name="scopeKind">Org / Team / Folder discriminator.</param>
    /// <param name="scopeId">Id of the matching Realm entity.</param>
    /// <param name="orgId">Tenant boundary — results are filtered to
    /// rows where <c>OrgId = orgId</c>.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<IReadOnlyList<QuotaAssignment>> ListForScopeAsync(
        QuotaScopeKind scopeKind,
        Guid scopeId,
        Guid orgId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Look up a single assignment by id. Returns <see langword="null" />
    ///     when no row matches.
    /// </summary>
    /// <param name="assignmentId">UUID v7 of the assignment row.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task<QuotaAssignment?> FindAsync(
        Guid assignmentId,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Insert a new assignment or update an existing one (matched
    ///     by <c>(DefinitionId, ScopeKind, ScopeId, Period)</c>).
    ///     <para>4.5.g.3 will exercise this from the PUT endpoint;
    ///     4.5.g.2 declares the contract but writes no test for it.</para>
    /// </summary>
    /// <param name="definitionId">Catalog row the assignment binds.</param>
    /// <param name="scope">Polymorphic scope (Kind, Id, OrgId).</param>
    /// <param name="value">The limit value for this scope.</param>
    /// <param name="period">Period override — <see cref="QuotaPeriod.None" />
    /// for absolute limits, <see cref="QuotaPeriod.Hour" /> for
    /// rate-limit overrides.</param>
    /// <param name="createdBy">User id that owns the assignment.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The persisted <see cref="QuotaAssignment" />, with
    /// <see cref="QuotaAssignment.Id" />, <see cref="QuotaAssignment.CreatedAt" />,
    /// and <see cref="QuotaAssignment.UpdatedAt" /> populated by the
    /// store.</returns>
    public Task<QuotaAssignment> UpsertAsync(
        Guid definitionId,
        QuotaScope scope,
        decimal value,
        QuotaPeriod period,
        Guid createdBy,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Remove the assignment with the given id. No-op when the id
    ///     is not found (consistent with the spec — assignment
    ///     removal is idempotent; the corresponding <c>QuotaUsage</c>
    ///     row persists so a future re-assignment resumes from the
    ///     same value).
    /// </summary>
    /// <param name="assignmentId">UUID v7 of the assignment row.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public Task DeleteAsync(
        Guid assignmentId,
        CancellationToken cancellationToken);
}
