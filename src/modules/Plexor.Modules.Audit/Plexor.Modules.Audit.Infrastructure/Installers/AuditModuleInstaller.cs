using Microsoft.Extensions.DependencyInjection;
using Plexor.Modules.Audit.Application.Abstractions;
using Plexor.Modules.Audit.Infrastructure.Persistence;

namespace Plexor.Modules.Audit.Infrastructure.Installers;

/// <summary>
///     Single registration entry for the Plexor.Modules.Audit module.
///     Hosts compose it as <c>builder.Services.AddAuditModule()</c>
///     alongside the other module installers (Sigil, Realm, Clusters).
/// </summary>
/// <remarks>
///     <para><b>What this installer registers.</b>
///     <see cref="IAuditStore" /> → <see cref="EfAuditStore" />.
///     The <see cref="AuditDbContext" /> itself is NOT registered here
///     — composition roots (Plexor.Host / Plexor.Migrator) wire the
///     DbContext via <c>AddModuleDbContext&lt;AuditDbContext&gt;</c>
///     once per host, alongside the other module contexts.</para>
///     <para><b>Why Infrastructure (not Application).</b> The
///     installer binds the Application-layer port to an
///     Infrastructure-layer implementation — Infrastructure
///     depends on Application, not the other way around. Putting
///     the binding in Application would require Application to
///     reference Infrastructure, which creates a circular
///     project reference (Infrastructure also references
///     Application for the port type).</para>
///     <para><b>Lifetime.</b> Scoped — the EF store holds a scoped
///     <see cref="AuditDbContext" />; transient would over-allocate,
///     singleton would hold a dead <c>DbContext</c> across requests.</para>
/// </remarks>
public static class AuditModuleInstaller
{
    /// <summary>Register the Plexor.Modules.Audit services.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddAuditModule(this IServiceCollection services)
    {
        services.AddScoped<IAuditStore, EfAuditStore>();
        return services;
    }
}
