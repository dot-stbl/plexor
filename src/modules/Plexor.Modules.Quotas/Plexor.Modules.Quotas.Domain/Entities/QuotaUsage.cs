using Plexor.Shared.Kernel.Common;
using Plexor.Shared.Kernel.Quotas;

namespace Plexor.Modules.Quotas.Domain.Entities;

/// <summary>
///     Current consumption snapshot, one row per
///     <c>(scope_kind, scope_id, definition_id)</c>. Updated atomically
///     with the resource INSERT inside the enforcer's transaction.
/// </summary>
/// <remarks>
///     <para><b>Not filterable.</b> QuotaUsage rows are an internal
///     state table; queries against them come through the 4.5.g
///     endpoints, not the public filter DSL.</para>
///     <para><b>Period semantics.</b> For <see cref="QuotaPeriod.None" />
///     (absolute limits), <see cref="CurrentValue" /> is the lifetime
///     count. For <see cref="QuotaPeriod.Hour" /> (rate-limit),
///     <see cref="CurrentValue" /> resets at the start of each hour
///     bucket — the enforcer upserts a new row keyed on
///     <see cref="PeriodStart" /> rather than mutating in place. The
///     4.5.b enforcer implementation owns that logic.</para>
/// </remarks>
public sealed class QuotaUsage : ICreatedAt, IUpdatedAt
{
    /// <summary>Scope (Org / Team / Folder) the snapshot targets.</summary>
    public QuotaScopeKind ScopeKind { get; init; }

    /// <summary>Id of the matching Realm entity.</summary>
    public Guid ScopeId { get; init; }

    /// <summary>FK to the catalog entry this snapshot measures.</summary>
    public Guid DefinitionId { get; init; }

    /// <summary>Tenant the scope belongs to (denormalized).</summary>
    public Guid OrgId { get; init; }

    /// <summary>Current consumption value. Updated atomically with the
    /// resource INSERT in the enforcer's transaction.</summary>
    public decimal CurrentValue { get; init; }

    /// <summary>Start of the current period bucket (UTC). For
    /// <see cref="QuotaPeriod.None" /> the value is the row's
    /// <see cref="CreatedAt" /> and never changes; for
    /// <see cref="QuotaPeriod.Hour" /> the value is the start of the
    /// current hour.</summary>
    public DateTimeOffset PeriodStart { get; init; }

    /// <summary>Last time the row was reconciled (UTC). The enforcer
    /// bumps this on every update; reads of <c>current_value</c>
    /// against a stale row trigger a re-read at the start of the
    /// next reservation.</summary>
    public DateTimeOffset LastReconciledAt { get; init; }

    /// <summary>Row creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
