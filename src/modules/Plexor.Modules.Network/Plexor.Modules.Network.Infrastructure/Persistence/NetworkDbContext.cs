// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NetworkDbContext — EF Core context for the Network module. Owns
// floating_ips + load_balancers in the `network` PostgreSQL schema
// (architecture theme). The schema name + table names live in the
// module-local Plexor.Modules.Network.Infrastructure.Persistence.DatabaseInformation;
// nothing is hard-coded here.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Network.Domain.Entities;
using Plexor.Modules.Network.Infrastructure.Persistence.Configurations;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Network.Infrastructure.Persistence;

/// <summary>
///     EF Core context for the Network module. Persists the
///     <c>network.floating_ips</c> + <c>network.load_balancers</c>
///     rows in the <c>network</c> PostgreSQL schema. Composed from
///     two <see cref="IEntityTypeConfiguration{TEntity}" /> files in
///     <c>Configurations/</c>.
/// </summary>
/// <param name="options">EF Core options bag.</param>
public sealed class NetworkDbContext(DbContextOptions<NetworkDbContext> options)
    : PlexorDbContext(options)
{
    /// <summary>Floating IPs (network.floating_ips) — one row per
    /// floating IP. Drives the network.floating_ips.count quota
    /// key.</summary>
    public DbSet<FloatingIp> FloatingIps => Set<FloatingIp>();

    /// <summary>Load balancers (network.load_balancers) — one row
    /// per LB. Drives the network.load_balancers.count quota key.</summary>
    public DbSet<LoadBalancer> LoadBalancers => Set<LoadBalancer>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Network)
            .ApplyConfiguration(new FloatingIpConfiguration())
            .ApplyConfiguration(new LoadBalancerConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
