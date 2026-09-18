// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// WorkloadLifecycleEventConfiguration — forge.workload_lifecycle_events.
// Append-only audit trail of every successful Workload lifecycle
// transition. One row per Mark* call. Indexed by workload id +
// occurred_at for the per-workload timeline query.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence.Configurations;

/// <summary>
///     forge.workload_lifecycle_events — one row per Workload
///     transition. The aggregate's Mark* methods append rows in
///     the same transaction as the parent workload UPDATE so the
///     audit trail is consistent with the state. Read-only at the
///     application layer (no handlers mutate lifecycle events
///     outside the aggregate).
/// </summary>
internal sealed class WorkloadLifecycleEventConfiguration : IEntityTypeConfiguration<WorkloadLifecycleEvent>
{
    public void Configure(EntityTypeBuilder<WorkloadLifecycleEvent> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.WorkloadLifecycleEvents);

        builder.HasKey(static evt => evt.Id);

        builder.Property(static evt => evt.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static evt => evt.WorkloadId)
            .HasColumnName("workload_id")
            .HasMaxLength(64)
            .IsRequired()
            .HasConversion(
                static id => id.ToString(),
                static raw => IdParse.ParseWorkloadId(raw));

        builder.Property(static evt => evt.FromState)
            .HasColumnName("from_state")
            .HasMaxLength(32)
            .IsRequired()
            .HasConversion(
                static state => state.ToString(),
                static raw => Enum.Parse<WorkloadLifecycleState>(raw));

        builder.Property(static evt => evt.ToState)
            .HasColumnName("to_state")
            .HasMaxLength(32)
            .IsRequired()
            .HasConversion(
                static state => state.ToString(),
                static raw => Enum.Parse<WorkloadLifecycleState>(raw));

        builder.Property(static evt => evt.ProviderVmId)
            .HasColumnName("provider_vm_id")
            .HasMaxLength(128);

        builder.Property(static evt => evt.Reason)
            .HasColumnName("reason")
            .HasMaxLength(1024);

        builder.Property(static evt => evt.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        // Cascade-delete through the workload FK: deleting the
        // parent workload row wipes its lifecycle audit trail too.
        // The handlers add events explicitly via the DbSet (not via
        // a navigation property) so this relationship is the sole
        // FK declaration on the event side.
        builder.HasOne<Workload>()
            .WithMany()
            .HasForeignKey(static evt => evt.WorkloadId)
            .OnDelete(DeleteBehavior.Cascade);

        // Timeline query: "show me every transition for this workload
        // in creation order". The Id (UUIDv7) is monotonic in time
        // so ORDER BY id is a valid substitute for ORDER BY occurred_at
        // when paginating by id.
        builder.HasIndex(static evt => new { evt.WorkloadId, evt.Id })
            .HasDatabaseName("ix_workload_lifecycle_events_workload_id_id");
    }
}
