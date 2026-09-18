// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// BucketConfiguration — EF Core configuration for the storage.buckets
// row. snake_case column names, bounded string lengths, two indexes:
//   ix_storage_buckets_org_id           (tenant-scoped listing)
//   ix_storage_buckets_name_region      (S3-style global uniqueness)
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Storage.Domain.Entities;

namespace Plexor.Modules.Storage.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + <c>HasMaxLength</c> per
///     <c>.agents/coding/ef-core.md</c>. The UNIQUE on
///     <c>(name, region)</c> matches the S3 bucket naming rule (DNS-
///     label name + region together identify a bucket globally).
/// </summary>
internal sealed class BucketConfiguration : IEntityTypeConfiguration<Bucket>
{
    /// <summary>Bucket name cap. S3 limits DNS-label bucket names to
    /// 63 chars; 64 leaves one char of headroom for future extensions
    /// without a schema migration.</summary>
    private const int NameMaxLength = 64;

    /// <summary>Region label cap. Matches Plexor.Modules.Clusters.Domain.Cluster.Region
    /// style — operator-defined strings, not an enum.</summary>
    private const int RegionMaxLength = 64;

    public void Configure(EntityTypeBuilder<Bucket> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Buckets);

        builder.HasKey(static bucket => bucket.Id);

        builder.Property(static bucket => bucket.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static bucket => bucket.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static bucket => bucket.Name)
            .HasColumnName("name")
            .HasMaxLength(NameMaxLength)
            .IsRequired();

        builder.Property(static bucket => bucket.Region)
            .HasColumnName("region")
            .HasMaxLength(RegionMaxLength)
            .IsRequired();

        builder.Property(static bucket => bucket.SizeBytes)
            .HasColumnName("size_bytes")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(static bucket => bucket.ObjectCount)
            .HasColumnName("object_count")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(static bucket => bucket.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static bucket => bucket.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Tenant-scoped listing — the admin UI lists every bucket
        // for the caller's org.
        builder.HasIndex(static bucket => bucket.OrgId)
            .HasDatabaseName("ix_storage_buckets_org_id");

        // S3-style global uniqueness — a (name, region) tuple
        // identifies a bucket worldwide.
        builder.HasIndex(static bucket => new { bucket.Name, bucket.Region })
            .HasDatabaseName("ix_storage_buckets_name_region")
            .IsUnique();
    }
}
