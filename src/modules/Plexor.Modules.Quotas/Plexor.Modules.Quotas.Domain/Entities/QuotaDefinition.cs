using Plexor.Shared.Filtering.Registry;
using Plexor.Shared.Kernel.Common;

namespace Plexor.Modules.Quotas.Domain.Entities;

/// <summary>
///     One entry in the global quota catalog — a stable identifier
///     (<c>Key</c>, e.g. <c>"compute.vms.count"</c>) plus the unit,
///     period, and built-in default value the enforcer falls back to
///     when no <see cref="QuotaAssignment" /> exists for the requested
///     scope.
/// </summary>
/// <remarks>
///     <para><b>Append-mostly.</b> v1 only inserts rows at startup
///     (the catalog seed in 4.5.a); updates are restricted to the
///     <see cref="Description" />, <see cref="DefaultValue" />, and
///     the <see cref="EffectiveFrom" /> / <see cref="EffectiveUntil" />
///     pair. Renaming a <see cref="Key" /> is a breaking change —
///     callers branch on the string, not on the row id.</para>
///     <para><b>Filterable.</b> Properties are exposed to the filter
///     DSL via <see cref="IFilterableEntity" /> so the catalog
///     endpoint can be filtered / sorted (4.5.g).</para>
/// </remarks>
public sealed class QuotaDefinition : IFilterableEntity, ICreatedAt, IUpdatedAt
{
    /// <summary>Unique identifier (UUID v7).</summary>
    public Guid Id { get; init; }

    /// <summary>Stable catalog identifier (<c>"compute.vms.count"</c>,
    /// <c>"compute.vms.ram_gb"</c>, ...). Unique across the catalog.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Human-readable label, e.g. <c>"Number of VMs"</c>.
    /// Nullable — older catalog rows may not have a description.</summary>
    public string? Description { get; init; }

    /// <summary>Unit of measurement (count / GiB / vCPU / req-per-hour).</summary>
    public QuotaUnit Unit { get; init; }

    /// <summary>Time window the value is measured over. v1 uses
    /// <see cref="QuotaPeriod.None" /> (absolute) and
    /// <see cref="QuotaPeriod.Hour" /> (rate limit).</summary>
    public QuotaPeriod Period { get; init; }

    /// <summary>Built-in default value when no <see cref="QuotaAssignment" />
    /// exists for the requested scope. <c>null</c> means
    /// <em>unlimited</em> — no quota is enforced for this key.</summary>
    public decimal? DefaultValue { get; init; }

    /// <summary>Whether this row is shipped with the platform
    /// (<c>true</c>) or added by an operator at runtime.</summary>
    public bool Builtin { get; init; } = true;

    /// <summary>Inclusive start of the window during which this
    /// <see cref="Key" /> is the active definition (UTC).
    /// <c>null</c> means "currently effective from the start of time".</summary>
    public DateTimeOffset? EffectiveFrom { get; init; }

    /// <summary>Inclusive end of the window during which this
    /// <see cref="Key" /> is the active definition (UTC).
    /// <c>null</c> means "no scheduled retirement".</summary>
    public DateTimeOffset? EffectiveUntil { get; init; }

    /// <summary>Row creation time (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
