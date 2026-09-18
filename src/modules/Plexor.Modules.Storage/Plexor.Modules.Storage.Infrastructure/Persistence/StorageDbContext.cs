// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// StorageDbContext — EF Core context for the Storage module. Owns
// volumes + buckets in the `storage` PostgreSQL schema (architecture
// theme). The schema name + table names live in the module-local
// Plexor.Modules.Storage.Infrastructure.Persistence.DatabaseInformation;
// nothing is hard-coded here.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Modules.Storage.Domain.Entities;
using Plexor.Modules.Storage.Infrastructure.Persistence.Configurations;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Storage.Infrastructure.Persistence;

/// <summary>
///     EF Core context for the Storage module. Persists the
///     <c>storage.volumes</c> + <c>storage.buckets</c> rows in the
///     <c>storage</c> PostgreSQL schema. Composed from the two
///     <see cref="IEntityTypeConfiguration{TEntity}" /> files in
///     <c>Configurations/</c>.
/// </summary>
/// <param name="options">EF Core options bag.</param>
public sealed class StorageDbContext(DbContextOptions<StorageDbContext> options)
    : PlexorDbContext(options)
{
    /// <summary>Volumes (storage.volumes) — one row per disk
    /// volume. Drives the storage.volumes.count and
    /// storage.volumes.gb quota keys.</summary>
    public DbSet<Volume> Volumes => Set<Volume>();

    /// <summary>Buckets (storage.buckets) — one row per object-store
    /// bucket. Not quota-affecting in v0.1; the row exists for
    /// identity + book-keeping.</summary>
    public DbSet<Bucket> Buckets => Set<Bucket>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Storage)
            .ApplyConfiguration(new VolumeConfiguration())
            .ApplyConfiguration(new BucketConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
