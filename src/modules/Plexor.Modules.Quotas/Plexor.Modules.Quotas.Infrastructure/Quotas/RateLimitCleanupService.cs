// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RateLimitCleanupService — BackgroundService that trims the
// quotas.rate_limit_events table to keep it bounded. Runs hourly at
// HH:05 UTC; deletes every row older than the retention window (2h —
// one hour past the longest rate-limit window so the cleanup never
// races with an active query).
//
// The service is registered as IHostedService in
// QuotasInfrastructureInstaller.AddQuotasInfrastructureCore. The
// composition root (Plexor.Host / Plexor.Migrator) opts into the
// installer; the migrator runs the service during a one-shot migrate
// cycle, which is harmless — the first sweep waits until the next
// HH:05 boundary.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plexor.Modules.Quotas.Infrastructure.Persistence;

namespace Plexor.Modules.Quotas.Infrastructure.Quotas;

/// <summary>
///     BackgroundService that deletes <c>rate_limit_events</c> rows
///     older than the retention window (2h). Cadence: hourly, anchored
///     to <c>HH:05:00</c> UTC.
/// </summary>
/// <param name="scopeFactory">Application-level scope factory —
/// <see cref="QuotasDbContext" /> is scoped per request; the singleton
/// BackgroundService opens a fresh scope per sweep.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the
/// schedule + the cutoff computation.</param>
/// <param name="logger">Structured logger for the rowcount.</param>
/// <remarks>
///     <para><b>Why <c>HH:05</c>.</b> One minute past the typical
///     API-hour rollover (00:00, 01:00, ...) — most of the
///     trailing-edge events have rolled out of the 1h window by then.
///     Spreading the sweep across the hour keeps the DELETE from
///     batching against a single hot second.</para>
///     <para><b>Why no quota meter.</b> The cleanup is a periodic
///     housekeeping job, not a quota-bound operation. It is exempt
///     from rate limiting by virtue of running in the host, not via
///     an HTTP request.</para>
/// </remarks>
public sealed class RateLimitCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<RateLimitCleanupService> logger) : BackgroundService
{
    /// <summary>2h retention — one hour past the longest rate-limit
    /// window (1h) so the cleanup never races with an active query.</summary>
    private static readonly TimeSpan RetentionWindow = TimeSpan.FromHours(2);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = RateLimitCleanupServiceHelpers.NextSweepTime(clock);
            var delay = next - clock.GetUtcNow();

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await RateLimitCleanupServiceHelpers.SweepAsync(
                    scopeFactory,
                    clock,
                    RetentionWindow,
                    logger,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Sweep failures are non-fatal — the next iteration
                // will retry. Log + continue so a transient DB blip
                // doesn't kill the BackgroundService.
                logger.LogWarning(
                    exception,
                    "RateLimitCleanupService sweep failed; will retry at the next HH:05 boundary.");
            }
        }
    }
}
