namespace Plexor.Modules.Quotas.Domain;

/// <summary>
///     Time window over which a quota's value is measured.
/// </summary>
/// <remarks>
///     <para><b>v1 only emits <see cref="None" /> and <see cref="Hour" />.</b>
///     <see cref="Day" /> and <see cref="Month" /> are reserved for the
///     future billing-style windows described in
///     <c>openspec/changes/phase-4-5-quotas/design.md</c> §"Period
///     semantics". They exist in the enum so the catalog seed and the
///     resolver don't need a code change to ship them.</para>
/// </remarks>
public enum QuotaPeriod
{
    /// <summary>Absolute limit (lifetime count or cumulative capacity).</summary>
    None = 0,

    /// <summary>Sliding-window per hour (rate-limit per hour).</summary>
    Hour = 1,

    /// <summary>Sliding-window per day (reserved for Phase 5+ billing windows).</summary>
    Day = 2,

    /// <summary>Calendar month (reserved for Phase 5+ billing windows).</summary>
    Month = 3,
}
