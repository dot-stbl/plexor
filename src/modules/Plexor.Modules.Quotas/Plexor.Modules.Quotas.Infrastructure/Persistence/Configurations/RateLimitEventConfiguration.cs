using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Quotas.Domain.Entities;

namespace Plexor.Modules.Quotas.Infrastructure.Persistence.Configurations;

/// <summary>
///     Append-only event log. Two composite indexes cover the read
///     paths: <c>(principal_id, occurred_at)</c> for the per-user
///     sliding-window SELECT, and <c>(org_id, occurred_at)</c> for
///     the org-aggregate sliding-window SELECT.
/// </summary>
internal sealed class RateLimitEventConfiguration : IEntityTypeConfiguration<RateLimitEvent>
{
    public void Configure(EntityTypeBuilder<RateLimitEvent> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.RateLimitEvents);

        builder.HasKey(static ev => ev.Id);

        builder.Property(static ev => ev.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static ev => ev.PrincipalId)
            .HasColumnName("principal_id")
            .HasColumnType("uuid")
            .IsRequired();

        // Principal kind stored as string so a future kind (service
        // account, OAuth client) doesn't require a migration.
        builder.Property(static ev => ev.PrincipalKind)
            .HasColumnName("principal_kind")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(static ev => ev.OrgId)
            .HasColumnName("org_id")
            .HasColumnType("uuid")
            .IsRequired();

        // Endpoint is bounded cardinality (the API routes), not free
        // form text — PathString maximum on ASP.NET is ~4 KB but the
        // common case is well under 256.
        builder.Property(static ev => ev.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(static ev => ev.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(static ev => ev.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Per-principal sliding window — the limiter's hot path.
        builder.HasIndex(static ev => new { ev.PrincipalId, ev.OccurredAt })
            .HasDatabaseName("ix_quotas_rate_limit_events_principal_id_occurred_at");

        // Per-org aggregate sliding window — the org-aggregate check
        // in IRateLimiter (4.5.e).
        builder.HasIndex(static ev => new { ev.OrgId, ev.OccurredAt })
            .HasDatabaseName("ix_quotas_rate_limit_events_org_id_occurred_at");

        // Cleanup job (4.5.e) — find events older than the max period.
        builder.HasIndex(static ev => ev.OccurredAt)
            .HasDatabaseName("ix_quotas_rate_limit_events_occurred_at");
    }
}
