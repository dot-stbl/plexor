// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// RevokedCertsDbContext — minimal EF Core context for the
// forge.revoked_certs table. Schema is shared with the Clusters
// module so the cascade-revoke path on cluster / node delete can
// insert the serial in the same transaction.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Plexor.Shared.Mtls.Entities;
using Plexor.Shared.Mtls.Persistence.Configurations;
using Plexor.Shared.Persistence;

namespace Plexor.Shared.Mtls.Persistence;

/// <summary>
///     Tracks revoked X.509 client certs. Reads feed the in-memory
///     cache that <c>MtlsAuthMiddleware</c> consults on every
///     mTLS handshake; writes cascade from cluster / node delete.
/// </summary>
public sealed class RevokedCertsDbContext(DbContextOptions<RevokedCertsDbContext> options) : PlexorDbContext(options)
{
    /// <summary>forge.revoked_certs rows (one per revoked cert serial).</summary>
    public DbSet<RevokedCert> RevokedCerts => Set<RevokedCert>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Clusters)
            .ApplyConfiguration(new RevokedCertConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}