// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// NodeCommandConfiguration — EF Core mapping for the per-node
// command queue table. Lives in Plexor.Modules.Clusters because
// the table holds node-targeted workloads; the cluster module
// owns the node fleet. snake_case column names per
// coding/ef-core.md; HasMaxLength on every string per the
// string-property discipline.
// ==========================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Plexor.Modules.Clusters.Domain.Entities;
using Plexor.Shared.Identifiers;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Clusters.Infrastructure.Persistence.Configurations;

/// <summary>
///     EF Core mapping for <see cref="NodeCommand" />. Persisted
///     in the <c>forge.commands</c> table. The agent's long-poll
///     query is one row-scan filtered by <c>node_id</c> +
///     <c>status = 'pending'</c> ordered by <c>created_at</c>;
///     the covering index on those three columns keeps the poll
///     cheap at thousands of pending commands per node.
/// </summary>
public sealed class NodeCommandConfiguration : IEntityTypeConfiguration<NodeCommand>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NodeCommand> builder)
    {
        builder.ToTable("commands", DatabaseInformation.Schemes.Clusters);

        builder.HasKey(static command => command.Id);
        builder.Property(static command => command.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(static command => command.NodeId)
            .HasColumnName("node_id")
            .HasConversion(
                static id => id.ToString(),
                static raw => IdParse.ParseNodeId(raw))
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static command => command.CommandId)
            .HasColumnName("command_id")
            .IsRequired();

        builder.Property(static command => command.Type)
            .HasColumnName("type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(static command => command.PayloadJson)
            .HasColumnName("payload_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(static command => command.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(static command => command.ResultJson)
            .HasColumnName("result_json")
            .HasColumnType("jsonb");

        builder.Property(static command => command.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(static command => command.CompletedAt)
            .HasColumnName("completed_at");

        // The agent's poll query filters by (node_id, status) and
        // orders by created_at. The unique index on (command_id)
        // supports idempotent retries — the agent re-posts the
        // result after a network blip, and the control plane
        // upserts on command_id.
        builder.HasIndex(static command => command.NodeId)
            .HasDatabaseName("ix_commands_node_id_status_created_at");

        builder.HasIndex(static command => command.CommandId)
            .HasDatabaseName("uk_commands_command_id")
            .IsUnique();
    }
}
