// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// AuditRetentionService — BackgroundService that trims the
// atlas.audit_entries table to keep it bounded. Default retention is
// 90 days (configurable via AuditOptions.RetentionDays); the sweep
// fires once per AuditOptions.CleanupInterval (default 24h), anchored
// to AuditOptions.SweepHourUtc (default 05:00 UTC).
//
// The service is registered as IHostedService in
// AuditInfrastructureInstaller.AddAuditInfrastructureCore. The
// composition root (Plexor.Host / Plexor.Migrator) opts into the
// installer; the migrator runs the service during a one-shot migrate
// cycle, which is harmless — the first sweep waits until the next
// configured SweepHourUtc boundary.
//
// The actual DELETE lives in AuditRetentionServiceHelpers so the
// BackgroundService stays free of private methods
// (class-layout-and-tooling.md §1a / §9.3) and so unit tests can
// exercise the sweep body without standing up a Host.
// ============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Plexor.Modules.Audit.Application.Audit;

namespace Plexor.Modules.Audit.Infrastructure.Audit;

/// <summary>
///     BackgroundService that deletes <c>audit_entries</c> rows older
///     than the retention window. Cadence: <see cref="AuditOptions.CleanupInterval" />
///     (default 24h), anchored to <see cref="AuditOptions.SweepHourUtc" />
///     (default 05:00 UTC).
/// </summary>
/// <param name="scopeFactory">Application-level scope factory —
/// <see cref="Persistence.AuditDbContext" /> is scoped per request;
/// the singleton BackgroundService opens a fresh scope per sweep.</param>
/// <param name="optionsMonitor">
///     <see cref="IOptionsMonitor{TOptions}" /> for the bound
///     <see cref="AuditOptions" />. <c>CurrentValue</c> is read on
///     every wake so a runtime config reload picks up a new retention
///     window without a host restart.</param>
/// <param name="clock">Injected <see cref="TimeProvider" /> for the
/// schedule + the cutoff computation.</param>
/// <param name="logger">Structured logger for the sweep rowcount.</param>
/// <remarks>
///     <para><b>Why <c>SweepHourUtc:05:00</c>.</b> 05:00 UTC is the
///     operator-quiet window in the typical Plexor deployment — most
///     nightly jobs have rolled out by then, and the next API-hour
///     rollover is hours away. Spreading the sweep across the hour
///     keeps the DELETE from batching against a single hot second.</para>
///     <para><b>Why no quota meter.</b> The cleanup is a periodic
///     housekeeping job, not a quota-bound operation. It is exempt
///     from rate limiting by virtue of running in the host, not via
///     an HTTP request.</para>
/// </remarks>
public sealed class AuditRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AuditOptions> optionsMonitor,
    TimeProvider clock,
    ILogger<AuditRetentionService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = optionsMonitor.CurrentValue;
            var next = AuditRetentionServiceHelpers.NextSweepTime(clock, options.SweepHourUtc);
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
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<Persistence.AuditDbContext>();
                await AuditRetentionServiceHelpers.SweepAsync(
                    db,
                    clock,
                    optionsMonitor.CurrentValue,
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
                    "AuditRetentionService sweep failed; will retry at the next iteration.");
            }
        }
    }
}
