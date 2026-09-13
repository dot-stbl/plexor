// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// EfQuotaAssignmentRepositoryHelpers — file-static helpers pulled out of
// EfQuotaAssignmentRepository.cs to satisfy the no-private-methods
// convention (class-layout-and-tooling.md §1a / §9.1 — Repository /
// port implementation). The repository is a thin façade; the actual
// query / mutation routines live here so the façade stays one-method-
// per-interface-method.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Quotas.Domain;
using Plexor.Modules.Quotas.Domain.Entities;
using Plexor.Modules.Quotas.Infrastructure.Persistence;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     Helpers for <see cref="EfQuotaAssignmentRepository" />. The
///     repository is a façade; each helper carries the per-method
///     logic and the <c>internal static class</c> keeps the no-private-
///     methods convention intact.
/// </summary>
internal static class EfQuotaAssignmentRepositoryHelpers
{
    /// <summary>
    ///     Read all assignments for one (scope, org). Filters at the
    ///     query level — callers in Org X never see Org Y's rows.
    /// </summary>
    /// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
    /// <param name="scopeKind">Org / Team / Folder discriminator.</param>
    /// <param name="scopeId">Id of the matching Realm entity.</param>
    /// <param name="orgId">Tenant boundary.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The matching rows ordered by definition id (stable
    /// ordering keeps the dashboard's row order deterministic).</returns>
    public static async Task<IReadOnlyList<QuotaAssignment>> ListForScopeInternalAsync(
        QuotasDbContext db,
        QuotaScopeKind scopeKind,
        Guid scopeId,
        Guid orgId,
        CancellationToken cancellationToken)
    {
        return await db.QuotaAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.ScopeKind == scopeKind
                && assignment.ScopeId == scopeId
                && assignment.OrgId == orgId)
            .OrderBy(static assignment => assignment.DefinitionId)
            .ThenBy(static assignment => assignment.Period)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    ///     Look up one assignment by id.
    /// </summary>
    /// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
    /// <param name="assignmentId">UUID v7 of the assignment row.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The matching row, or <see langword="null" />.</returns>
    public static async Task<QuotaAssignment?> FindInternalAsync(
        QuotasDbContext db,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        return await db.QuotaAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                assignment => assignment.Id == assignmentId,
                cancellationToken);
    }

    /// <summary>
    ///     Insert or update one assignment matched on the composite
    ///     UNIQUE index <c>(definition_id, scope_kind, scope_id, period)</c>.
    ///     On update: <c>Value</c> + <c>UpdatedAt</c> change; the
    ///     original <c>CreatedAt</c> / <c>CreatedBy</c> are preserved.
    /// </summary>
    /// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
    /// <param name="clock">Injected <see cref="TimeProvider" />.</param>
    /// <param name="definitionId">Catalog row the assignment binds.</param>
    /// <param name="scope">Polymorphic scope (Kind, Id, OrgId).</param>
    /// <param name="value">The new limit value.</param>
    /// <param name="period">Period override.</param>
    /// <param name="createdBy">User id (used for the initial insert;
    /// preserved across updates).</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The persisted row — the post-update snapshot when an
    /// existing row matched, or the freshly-inserted row otherwise.</returns>
    /// <remarks>
    ///     <para><b>Why <c>DbSet.Update</c> instead of
    ///     <c>ExecuteUpdateAsync</c>.</b> <see cref="QuotaAssignment" />
    ///     uses <c>init</c>-only properties — a tracked-entity
    ///     property assignment (<c>existing.Value = ...</c>) won't
    ///     compile. Two viable paths:</para>
    ///     <list type="number">
    ///       <item><c>ExecuteUpdateAsync</c> — generates a SQL SET
    ///       clause per property expression. Works on Postgres but is
    ///       <b>not supported</b> by the EF InMemory provider that the
    ///       unit tests run against (see
    ///       <c>NodeHeartbeatCommandHandlerShould</c> for the same
    ///       gap).</item>
    ///       <item><c>DbSet.Update</c> with a freshly-constructed
    ///       entity carrying the preserved <c>CreatedAt</c> /
    ///       <c>CreatedBy</c> — works on both providers, including
    ///       InMemory.</item>
    ///     </list>
    ///     <para>The helper uses the second path so the upsert is
    ///     testable against the InMemory provider that backs the rest
    ///     of the Quotas unit tests. The Postgres UNIQUE constraint
    ///     backs the read-then-write cycle against a concurrent
    ///     inserter in production.</para>
    /// </remarks>
    public static async Task<QuotaAssignment> UpsertInternalAsync(
        QuotasDbContext db,
        TimeProvider clock,
        Guid definitionId,
        QuotaScope scope,
        decimal value,
        QuotaPeriod period,
        Guid createdBy,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        // Look up first — drives the insert-vs-update branch.
        var existing = await db.QuotaAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                assignment =>
                    assignment.DefinitionId == definitionId
                    && assignment.ScopeKind == scope.Kind
                    && assignment.ScopeId == scope.Id
                    && assignment.Period == period,
                cancellationToken);

        if (existing is null)
        {
            var newRow = new QuotaAssignment
            {
                Id = Guid.NewGuid(),
                DefinitionId = definitionId,
                ScopeKind = scope.Kind,
                ScopeId = scope.Id,
                OrgId = scope.OrgId,
                Value = value,
                Period = period,
                CreatedBy = createdBy,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await db.QuotaAssignments.AddAsync(newRow, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return newRow;
        }

        // Update path — detach any prior tracked instance of this
        // row (left over from a previous UpsertAsync call within the
        // same DbContext scope), then Update with a fresh instance
        // that carries the preserved CreatedAt / CreatedBy + the
        // new value + UpdatedAt. DbSet.Update marks the row as
        // Modified on both Postgres and the InMemory provider.
        var tracked = await db.QuotaAssignments
            .FirstOrDefaultAsync(
                assignment => assignment.Id == existing.Id,
                cancellationToken);
        if (tracked is not null)
        {
            db.Entry(tracked).State = EntityState.Detached;
        }

        var updated = new QuotaAssignment
        {
            Id = existing.Id,
            DefinitionId = existing.DefinitionId,
            ScopeKind = existing.ScopeKind,
            ScopeId = existing.ScopeId,
            OrgId = existing.OrgId,
            Value = value,
            Period = existing.Period,
            CreatedBy = existing.CreatedBy,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = now,
        };
        db.QuotaAssignments.Update(updated);
        await db.SaveChangesAsync(cancellationToken);
        return updated;
    }

    /// <summary>
    ///     Remove the assignment with the given id. No-op when the id
    ///     is not found — assignment removal is idempotent; the
    ///     corresponding <c>QuotaUsage</c> row is left in place so a
    ///     future re-assignment resumes from the same consumption value.
    /// </summary>
    /// <param name="db">Scoped <see cref="QuotasDbContext" />.</param>
    /// <param name="assignmentId">UUID v7 of the assignment row.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    public static async Task DeleteInternalAsync(
        QuotasDbContext db,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var row = await db.QuotaAssignments
            .FirstOrDefaultAsync(
                assignment => assignment.Id == assignmentId,
                cancellationToken);

        if (row is null)
        {
            return;
        }

        db.QuotaAssignments.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
    }
}
