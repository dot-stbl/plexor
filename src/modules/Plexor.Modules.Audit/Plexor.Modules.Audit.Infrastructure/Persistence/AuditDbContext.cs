using Microsoft.EntityFrameworkCore;
using Plexor.Shared.Persistence;

namespace Plexor.Modules.Audit.Infrastructure.Persistence;

/// <summary>
///     EF Core context for the Audit module. Owns the
///     <c>atlas.audit_entries</c> table (one row per audited
///     action). Append-only by design — UPDATE / DELETE are REVOKEd
///     at the database level by the <c>AddAuditSchema</c>
///     migration.
/// </summary>
/// <remarks>
///     <para><b>Schema-per-module.</b> Sets
///     <c>modelBuilder.HasDefaultSchema("atlas")</c> via the
///     <see cref="DatabaseInformation.Schemes.Audit" /> constant.
///     Every <c>ToTable(...)</c> in
///     <see cref="Configurations.AuditEntryConfiguration" /> resolves
///     against <c>atlas</c>.</para>
///     <para><b>Registration.</b> Registered centrally by the
///     composition root (Plexor.Host / Plexor.Migrator) via
///     <c>AddModuleDbContext&lt;AuditDbContext&gt;</c>. Re-registering
///     here would double-register and break scopes.</para>
/// </remarks>
/// <param name="options">Standard EF Core options bag.</param>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options)
    : PlexorDbContext(options)
{
    /// <summary>
    ///     Audit entries (atlas.audit_entries). One row per audited
    ///     action; append-only by contract + REVOKE.
    /// </summary>
    public DbSet<AuditEntryRecord> AuditEntries => Set<AuditEntryRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseInformation.Schemes.Audit)
            .ApplyConfiguration(new Configurations.AuditEntryConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
