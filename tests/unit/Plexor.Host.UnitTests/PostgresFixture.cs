// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// PostgresFixture — shared class fixture that connects to the
// docker-composed Postgres (port 47100) and provisions a fresh
// IdentityDbContext per test. Reads the connection string from the
// MIGRATOR_CONNECTION env var (same convention as Plexor.Migrator).
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Sigil.Infrastructure.Persistence;
using Xunit;

namespace Plexor.Host.UnitTests;

/// <summary>
///     xUnit class fixture — one instance per test class. Hands out
///     short-lived <see cref="IdentityDbContext" />s to tests via
///     <see cref="NewDbContextAsync" />.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>
    ///     Connection string used for every context in this fixture.
    ///     Sourced from <c>MIGRATOR_CONNECTION</c>; defaults to the
    ///     dev-compose string when the variable is unset (CI/dev only —
    ///     production test runs pass the env var explicitly).
    /// </summary>
    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("MIGRATOR_CONNECTION")
        ?? "Host=localhost;Port=47100;Database=plexor;Username=plexor;Password=plexor";

    /// <summary>
    ///     Allocate a fresh <see cref="IdentityDbContext" /> for a
    ///     single test. Caller is responsible for disposal (use
    ///     <c>await using</c>).
    /// </summary>
    public Task<IdentityDbContext> NewDbContextAsync()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return Task.FromResult(new IdentityDbContext(options));
    }

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
