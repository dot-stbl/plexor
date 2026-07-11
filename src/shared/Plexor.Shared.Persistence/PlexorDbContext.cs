using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Plexor.Shared.Persistence;

/// <summary>
///     Abstract base for every module's <see cref="DbContext" />. Provides
///     schema-per-module configuration and snake_case naming convention so
///     every DbContext in the Plexor fleet uses identical naming + schema
///     discipline without each module re-implementing it.
/// </summary>
/// <remarks>
///     <para><b>Schema-per-module.</b> Subclasses override
///     <see cref="OnModelCreating" />, set <c>modelBuilder.HasDefaultSchema(...)</c>
///     using one of the constants in <see cref="DatabaseInformation.Schemes" />,
///     then declare their entities. The schema is automatically applied to
///     every <c>ToTable(...)</c> call that doesn't explicitly name a
///     different schema.</para>
///     <para><b>Snake_case.</b> Registered by
///     <see cref="PlexorPersistenceServiceCollectionExtensions.AddModuleDbContext{TContext}" />;
///     runtime safety-net for column + table names that don't carry an
///     explicit <c>HasColumnName(...)</c>. Design-time safety-net is
///     <c>HasColumnName("snake_case")</c> on every entity property.</para>
///     <para><b>Tracking by default.</b> Subclasses use the standard
///     EF Core <c>ChangeTracker</c> — read-only query paths opt out
///     per-query with <c>.AsNoTracking()</c>, never globally.</para>
///     <para><b>Connection string convention.</b> Each subclass carries
///     <c>[ConnectionString("...")]</c> pointing at its module's section
///     in <c>appsettings.json</c>. The host's composition root passes the
///     resolved string into <see cref="PlexorPersistenceServiceCollectionExtensions.AddModuleDbContext{TContext}" />.</para>
/// </remarks>
public abstract class PlexorDbContext : DbContext
{
    private readonly Action<ModelBuilder>? onModelCreatingHook;

    /// <summary>Constructs a DbContext with the supplied options and optional
    /// module-specific OnModelCreating hook (rare — prefer overriding
    /// <see cref="OnModelCreating" /> in the subclass).</summary>
    /// <param name="options">Standard EF Core options bag.</param>
    /// <param name="onModelCreatingHook">Optional hook for tests that need
    /// to inject models programmatically (production code should override
    /// the virtual method).</param>
    protected PlexorDbContext(
        DbContextOptions options,
        Action<ModelBuilder>? onModelCreatingHook = null)
        : base(options)
    {
        this.onModelCreatingHook = onModelCreatingHook;
    }

    /// <summary>
    ///     Apply module-specific model configuration. Subclasses call
    ///     <c>modelBuilder.HasDefaultSchema(...)</c> at the top of this
    ///     override before declaring entities.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (onModelCreatingHook is not null)
        {
            onModelCreatingHook(modelBuilder);
        }

        base.OnModelCreating(modelBuilder);
    }
}
