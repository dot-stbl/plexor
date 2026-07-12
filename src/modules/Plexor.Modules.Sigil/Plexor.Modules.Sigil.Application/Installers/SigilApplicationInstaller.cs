// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// SigilApplicationInstaller — single registration entry for the Sigil
// (Identity) Application layer. Hosts compose it as
//   builder.Services.AddSigilApplicationCore(builder.Configuration);
// Currently empty — Application services (auth, CQRS handlers) get
// added in 3.2+ as they're built.
// ============================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Plexor.Modules.Sigil.Application.Installers;

/// <summary>
///     Registers Application-layer services for the Sigil (Identity)
///     module. Reserved for Application-only registrations: validators,
///     pipeline behaviors, command/query handlers. Today this is a
///     no-op; the method exists so the call-site chain
///     (<c>AddSharedInfrastructureCore().AddSigilApplicationCore()</c>)
///     stays stable as services are added.
/// </summary>
/// <remarks>
///     <para><b>Why the empty method lives here.</b>
///     Per <c>di-installer.md</c>, each Plexor module exposes one
///     <c>Add&lt;Module&gt;Core</c> extension. Splitting into "Add*Service*"
///     extensions leads to a noisy Program.cs and obscures the
///     module's composition surface. Single entry per module, even if
///     empty today — the chain shows the modules shipped, not the
///     internal DI sub-graph.</para>
///     <para><b>Future additions (3.2+).</b>
///     <list type="bullet">
///       <item>Validators via <c>AddValidatorsFromAssembly</c> for
///       <c>LoginRequestValidator</c>, etc.</item>
///       <item>OpenTelemetry module registration
///       (<c>AddModuleTelemetry("Sigil")</c>).</item>
///     </list></para>
/// </remarks>
public static class SigilApplicationInstaller
{
    /// <summary>Register Sigil Application-layer services.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configuration">
    ///     Reserved for Options binding (3.2+ may add
    ///     <c>AuthOptions</c> for refresh-token lifetime etc.).
    /// </param>
    /// <returns>The same <paramref name="services" /> for chaining.</returns>
    public static IServiceCollection AddSigilApplicationCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = configuration;
        return services;
    }
}
