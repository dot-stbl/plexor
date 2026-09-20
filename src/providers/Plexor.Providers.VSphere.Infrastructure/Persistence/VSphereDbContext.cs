// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereDbContext — EF Core context for the vSphere provider. Owns
// the cached inventory + provisioning audit trail in the `outpost`
// PostgreSQL schema (architecture theme).
//
// The schema + table names live in DatabaseInformation; nothing is
// hard-coded here. Migrations are emitted under
// Migrations/ and applied by the Plexor.Migrator CLI in FK-dependent
// order (outpost runs after realm/sigil/atlas).
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Providers.VSphere.Infrastructure.Persistence.Configurations;
using Plexor.Shared.Persistence;

namespace Plexor.Providers.VSphere.Infrastructure.Persistence;

/// <summary>
///     EF Core context for the vSphere provider. Persists cached
///     vCenter inventory (snapshot + per-cluster / per-host / per-VM
///     rows) + the provisioning audit trail.
/// </summary>
/// <param name="options">EF Core options bag.</param>
public sealed class VSphereDbContext(DbContextOptions<VSphereDbContext> options)
    : PlexorDbContext(options)
{
    /// <summary>Snapshot header rows — one per inventory refresh.</summary>
    public DbSet<VSphereInventorySnapshot> InventorySnapshots => Set<VSphereInventorySnapshot>();

    /// <summary>Cached vSphere cluster rows.</summary>
    public DbSet<VSphereCluster> Clusters => Set<VSphereCluster>();

    /// <summary>Cached vSphere host rows.</summary>
    public DbSet<VSphereHost> Hosts => Set<VSphereHost>();

    /// <summary>Cached vSphere VM rows.</summary>
    public DbSet<VSphereVirtualMachine> VirtualMachines => Set<VSphereVirtualMachine>();

    /// <summary>Provisioning audit-trail rows.</summary>
    public DbSet<VSphereProvisioningRun> ProvisioningRuns => Set<VSphereProvisioningRun>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.VSphere)
            .ApplyConfiguration(new VSphereInventorySnapshotConfiguration())
            .ApplyConfiguration(new VSphereClusterConfiguration())
            .ApplyConfiguration(new VSphereHostConfiguration())
            .ApplyConfiguration(new VSphereVirtualMachineConfiguration())
            .ApplyConfiguration(new VSphereProvisioningRunConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
