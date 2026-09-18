// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// Workload EF Core configuration — snake_case columns + HasMaxLength
// per coding/ef-core.md. forge.workloads is the control-plane
// mirror of every workload the operator deployed; per-runtime
// state (libvirt UUID, k3s pod name) lives on the NodeAgent
// and is reported back through LocalId.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;
using Plexor.Shared.Workloads;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence.Configurations;

/// <summary>
///     forge.workloads — the control-plane's record of every
///     workload an operator deployed. The NodeAgent owns local
///     lifecycle (libvirt UUID, container id, k3s pod name) and
///     reports state back through the heartbeat / on-demand poll
///     path; this row exists so the UI / scheduler / drift
///     detector have a durable view.
/// </summary>
internal sealed class WorkloadConfiguration : IEntityTypeConfiguration<Workload>
{
    public void Configure(EntityTypeBuilder<Workload> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.Workloads);

        builder.HasKey(static workload => workload.Id);

        builder.Property(static workload => workload.Id)
            .HasColumnName("id")
            .HasMaxLength(64)
            .HasConversion(
                static id => id.ToString(),
                static raw => IdParse.ParseWorkloadId(raw));

        builder.Property(static workload => workload.ClusterId)
            .HasColumnName("cluster_id")
            .HasMaxLength(64)
            .IsRequired()
            .HasConversion(
                static id => id.ToString(),
                static raw => IdParse.ParseClusterId(raw));

        builder.Property(static workload => workload.AssignedNodeId)
            .HasColumnName("assigned_node_id")
            .HasMaxLength(64)
            .HasConversion<WorkloadAssignedNodeIdConverter>();

        builder.Property(static workload => workload.LocalId)
            .HasColumnName("local_id")
            .HasMaxLength(128);

        builder.Property(static workload => workload.Name)
            .HasColumnName("name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(static workload => workload.Kind)
            .HasColumnName("kind")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(static workload => workload.SpecJson)
            .HasColumnName("spec")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(static workload => workload.State)
            .HasColumnName("state")
            .HasMaxLength(32)
            .IsRequired()
            .HasConversion(
                static state => state.ToString(),
                static raw => Enum.Parse<WorkloadState>(raw));

        // Host-side lifecycle state — driven by Workload.Mark* on
        // every IComputeProvider confirmation. Independent of the
        // agent-reported `state` column above: lifecycle_state is
        // what the host asked the provider to do, state is what
        // the agent reports the runtime is doing. They reconcile
        // in the background; drift between them is a monitoring
        // signal, not a hard error.
        builder.Property(static workload => workload.LifecycleState)
            .HasColumnName("lifecycle_state")
            .HasMaxLength(32)
            .IsRequired()
            .HasConversion(
                static state => state.ToString(),
                static raw => Enum.Parse<WorkloadLifecycleState>(raw));

        // Provider-assigned VM id (libvirt domain UUID, k3s pod
        // UID, …). Null until MarkProvisioning lands the provider's
        // CreateVmAsync handle.
        builder.Property(static workload => workload.ProviderVmId)
            .HasColumnName("provider_vm_id")
            .HasMaxLength(128);

        builder.Property(static workload => workload.LastMessage)
            .HasColumnName("last_message")
            .HasMaxLength(1024);

        builder.Property(static workload => workload.LastReportedAt)
            .HasColumnName("last_reported_at");

        // Lifecycle event trail — eager-loaded by the per-workload
        // detail endpoint. FK declared explicitly (Workload doesn't
        // expose Events as a navigation on the public API surface
        // beyond the property itself, but EF needs the relationship
        // declared so it can emit the FK + cascade). Cascade delete
        // keeps the audit trail with the workload row.
        builder.HasMany(static workload => workload.Events)
            .WithOne()
            .HasForeignKey(static evt => evt.WorkloadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(static creation => creation.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static update => update.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // One workload name per cluster — the operator's mental
        // model is "I have a `wordpress` running in cluster X".
        // Reusing the name across clusters is fine (they're
        // independent fleets).
        builder.HasIndex(static workload => new { workload.ClusterId, workload.Name })
            .IsUnique()
            .HasDatabaseName("ix_workloads_cluster_id_name");

        // The scheduler's hot path: "show me every Ready
        // workload in cluster X, sorted by name".
        builder.HasIndex(static workload => new { workload.ClusterId, workload.State, workload.Name })
            .HasDatabaseName("ix_workloads_cluster_id_state_name");
    }
}
