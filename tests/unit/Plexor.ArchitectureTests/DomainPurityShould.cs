// SPDX-License-Identifier: Apache-2.0
// ============================================================================
// DomainPurityShould — enforce Law 2 of the Plexor modular-monolith
// architecture: domain projects (one per module) reference only the
// shared kernel — no EF Core, no ASP.NET, no HTTP, no JSON, no
// serialization. Domain is the innermost ring; framework/IO deps would
// invert the dependency direction and make domain logic untestable in
// isolation.
//
// One assertion per module's Domain assembly. Fails fast with the
// offender's name so the next contributor sees exactly which module
// leaked framework code into the wrong layer.
// ============================================================================

using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Plexor.ArchitectureTests;

/// <summary>
///     Architecture tests that pin the boundary between module Domain
///     projects and the rest of the runtime (Law 2): Domain code has
///     zero framework/IO dependencies.
/// </summary>
public sealed class DomainPurityShould
{
    /// <summary>
    ///     Given every module's <c>*.Domain</c> assembly, when scanned
    ///     by NetArchTest for framework dependencies, then none
    ///     reference <c>Microsoft.EntityFrameworkCore</c> or
    ///     <c>Microsoft.AspNetCore</c>. Domain may only depend on
    ///     <c>Plexor.Shared.*</c>.
    /// </summary>
    [Fact(DisplayName = "Given all module Domain assemblies, when scanned for framework deps, then no reference EntityFramework or ASP.NET")]
    public void Domain_projects_reference_only_Plexor_Shared()
    {
        var domains = new[]
        {
            "Plexor.Modules.Realm.Domain",
            "Plexor.Modules.Sigil.Domain",
            "Plexor.Modules.Clusters.Domain",
            "Plexor.Modules.Quotas.Domain",
            "Plexor.Modules.Branding.Domain",
            "Plexor.Modules.Audit.Domain",
        };

        foreach (var domain in domains)
        {
            var asm = TestAssemblies.Load(domain);
            var noEf = Types
                .InAssembly(asm)
                .ShouldNot()
                .HaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult();
            var noAsp = Types
                .InAssembly(asm)
                .ShouldNot()
                .HaveDependencyOn("Microsoft.AspNetCore")
                .GetResult();

            (noEf.IsSuccessful && noAsp.IsSuccessful).ShouldBeTrue(
                $"{domain} must reference only Plexor.Shared.* (no framework/IO deps).");
        }
    }
}
