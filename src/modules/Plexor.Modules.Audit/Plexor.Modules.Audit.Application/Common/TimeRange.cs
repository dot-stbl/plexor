namespace Plexor.Modules.Audit.Application.Common;

/// <summary>
///     Half-open time window used by audit query paths.
///     <see cref="Start" /> is inclusive, <see cref="End" /> is exclusive.
/// </summary>
/// <remarks>
///     <para><b>Why not <see cref="DateTime" />?</b> The Plexor
///     domain uses <see cref="DateTimeOffset" /> for every wall-clock
///     instant — timezone-ambiguous <see cref="DateTime" /> is banned
///     in new code (per the global C# conventions).</para>
///     <para><b>Why not <see cref="TimeSpan" />?</b> Audit windows
///     are absolute wall-clock windows ("between 09:00 and 17:00
///     yesterday"), not durations.</para>
///     <para><b>Validation.</b> Callers are expected to construct
///     <c>new TimeRange(start, end)</c> with <c>start &lt;= end</c>.
///     The record trusts the caller — no runtime guard, per
///     nullable-enable + trust-the-signature.</para>
/// </remarks>
/// <param name="Start">Inclusive lower bound (UTC).</param>
/// <param name="End">Exclusive upper bound (UTC).</param>
public sealed record TimeRange(DateTimeOffset Start, DateTimeOffset End);
