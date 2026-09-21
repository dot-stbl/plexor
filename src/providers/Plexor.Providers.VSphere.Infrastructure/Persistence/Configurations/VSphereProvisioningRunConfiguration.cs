// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// VSphereProvisioningRunConfiguration — EF Core configuration for the
// provisioning audit-trail row. snake_case + HasMaxLength per
// .agents/coding/ef-core.md.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Plexor.Providers.VSphere.Infrastructure.Persistence.Configurations;

/// <summary>
///     snake_case + bounded string lengths. The
///     <c>(started_at desc)</c> index backs the audit timeline
///     query. The <c>(source_template_moref)</c> index backs
///     "show every clone of this template" — useful when an operator
///     needs to back out a bad template.
/// </summary>
internal sealed class VSphereProvisioningRunConfiguration
    : IEntityTypeConfiguration<VSphereProvisioningRun>
{
    private const int MorefMaxLength = 128;
    private const int NameMaxLength = 256;
    private const int StatusMaxLength = 32;
    private const int ErrorMessageMaxLength = 2048;

    public void Configure(EntityTypeBuilder<VSphereProvisioningRun> builder)
    {
        builder.ToTable(DatabaseInformation.Tables.ProvisioningRuns);

        builder.HasKey(static run => run.Id);

        builder.Property(static run => run.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(static run => run.SourceTemplateMoref)
            .HasColumnName("source_template_moref")
            .HasMaxLength(MorefMaxLength)
            .IsRequired();

        builder.Property(static run => run.RequestedName)
            .HasColumnName("requested_name")
            .HasMaxLength(NameMaxLength)
            .IsRequired();

        builder.Property(static run => run.TargetFolderMoref)
            .HasColumnName("target_folder_moref")
            .HasMaxLength(MorefMaxLength);

        builder.Property(static run => run.ResultVmMoref)
            .HasColumnName("result_vm_moref")
            .HasMaxLength(MorefMaxLength);

        builder.Property(static run => run.Status)
            .HasColumnName("status")
            .HasMaxLength(StatusMaxLength)
            .IsRequired();

        builder.Property(static run => run.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(static run => run.FinishedAt)
            .HasColumnName("finished_at");

        builder.Property(static run => run.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(ErrorMessageMaxLength);

        builder.Property(static run => run.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(static run => run.StartedAt)
            .HasDatabaseName("ix_outpost_vsphere_provisioning_runs_started_at")
            .IsDescending(true);

        builder.HasIndex(static run => run.SourceTemplateMoref)
            .HasDatabaseName("ix_outpost_vsphere_provisioning_runs_source_template_moref");
    }
}
