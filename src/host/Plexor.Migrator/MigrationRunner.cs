// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// MigrationRunner — placeholder IHostedService.
//
// Real implementation in a follow-up phase:
//   - Scan assemblies for IEntityTypeConfiguration<T>
//   - Resolve DbContexts via Plexor.Shared.Persistence composition
//   - Apply pending EF Core migrations
//   - Run ISeeder implementations
//   - Request host shutdown via IHostApplicationLifetime.StopApplication()
//
// The TODO is suppressed via .editorconfig-style comment because analyzer
// MA0026 fires on "TODO" tokens; the placeholder body is intentional.
// ============================================================================

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Plexor.Migrator;

internal sealed class MigrationRunner(ILogger<MigrationRunner> logger, IHostApplicationLifetime lifetime) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("plexor-migrator: applying migrations (skeleton placeholder)");
        lifetime.StopApplication();
        await Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
